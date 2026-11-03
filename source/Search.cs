using System.Collections.Concurrent;
using System.Diagnostics;

class PolicyResult
{
    public Expression expression;
    public int root;
    public float[] scores;
    public float minScore;
    public Expression sketch;
    public int length => expression.nodes[root].length;

    public PolicyResult(Expression expression, int root, Expression sketch = null)
    {
        this.expression = expression;
        this.root = root;
        this.sketch = sketch;
    }

    public Script Compile() => sketch == null ? Compiler.Run(expression, root) : Compiler.Run(sketch.Substitute(expression, root));
    public string Fragment(string[] storageNames, List<Template> templates) => expression.ToString(root, storageNames, templates);
    public string ToString(string[] storageNames, List<Template> templates) => sketch == null ? Fragment(storageNames, templates) : sketch.Substitute(expression, root).ToString(storageNames, templates);
    public override string ToString() => ToString(null, null);
    public string Join() => string.Join(' ', scores.Select(score => score.ToString("0.##")));
}

static class Search
{
    public static GameReport BottomUp(Game game, int minLength, int maxLength, (Codon, TYPE)[] grammar, int numSeeds, int steps, Expression[] patterns, Expression sketch = null)
    {
        if (minLength < 1 || maxLength < minLength || numSeeds < 1 || steps < 1)
            throw new ArgumentException("Search requires 1 <= minLength <= length, numSeeds >= 1, and steps >= 1");
        Template template = game.templates[game.agentTemplate];
        if (sketch == null && template.policy == null) throw new Exception("no baseline policy");
        TYPE targetType = TYPE.TUPLE;
        if (sketch != null)
        {
            if (sketch.error != null) throw new ArgumentException(sketch.error);
            Node[] holes = sketch.nodes.Where(n => n.op == OP.SUB).ToArray();
            if (holes.Length == 0) throw new ArgumentException("Search shape must contain SUB");
            targetType = holes[0].otype;
            if (holes.Any(n => n.otype != targetType) || (targetType != TYPE.TUPLE && targetType != TYPE.BOOL))
                throw new ArgumentException("SUB must have type ACTION or BOOL");
            if (sketch.GetType(sketch.nodes.Count - 1) != TYPE.TUPLE)
                throw new ArgumentException("The completed shape must return an action");
        }
        patterns = patterns.Concat(template.patterns).ToArray();

        Stopwatch enumerationWatch = Stopwatch.StartNew();
        Store store = new(grammar, targetType, maxLength, patterns, game.templates);

        store.EnumerateExpressions(game.templates);
        if (Settings.PRINT_EXPRESSIONS) Console.WriteLine($"bottom-up enumeration time = {enumerationWatch.ElapsedMilliseconds / 1000f:0.00} seconds");
        Expression expressions = store.expressions;
        bool InShape(OP op, int? variant = null) => sketch != null && (variant.HasValue
            ? sketch.Contains(sketch.nodes.Count - 1, op, variant.Value) : sketch.Contains(sketch.nodes.Count - 1, op));
        bool GoodCandidate(int id)
        {
            if (expressions.nodes[id].length < minLength || expressions.nodes[id].dbtype != TYPE.VOID) return false;
            // Apply whole-policy requirements to the completed sketch, not just its hole.
            if (!(InShape(OP.GO) || InShape(OP.GOTO) || expressions.Contains(id, OP.GO) || expressions.Contains(id, OP.GOTO))) return false;
            if (template.onUse != null && !InShape(OP.USE) && !expressions.Contains(id, OP.USE)) return false;
            return template.sensors.All(s => !s.required || InShape(OP.LOAD, s.memoryIndex) || expressions.Contains(id, OP.LOAD, s.memoryIndex));
        }
        PolicyResult[] flat = store.bags[(int)targetType].SelectMany(bag => bag).Where(GoodCandidate).Select(id => new PolicyResult(expressions, id, sketch)).ToArray();
        PolicyResult[] bestPolicies = new PolicyResult[maxLength];

        PolicyResult baseline = null;
        if (sketch == null)
        {
            baseline = new(template.epolicy, template.epolicy.nodes.Count - 1);
            baseline.scores = Score(game, template.policy, numSeeds, steps);
            baseline.minScore = baseline.scores.Min();
            Console.WriteLine($"baseline {baseline.ToString(template.storageNames, game.templates)} has length {baseline.length} and minscore {baseline.minScore}");
            for (int l = baseline.length - 1; l < bestPolicies.Length; l++) bestPolicies[l] = baseline;
        }

        CS.WriteLine($"Simulating {flat.Length} policies of length up to {maxLength}:", ConsoleColor.Red);

        const int THRESHOLDS = 10;
        int completed = 0, progress = 0;
        object sync = new();

        float[] seedScores = new float[numSeeds];
        Array.Fill(seedScores, -1000f);
        int[] seedTests = new int[numSeeds];
        int[] range = Enumerable.Range(0, numSeeds).ToArray();
        Comparison<int> compareSeeds = (a, b) =>
        {
            int comparison = seedScores[a].CompareTo(seedScores[b]);
            return comparison != 0 ? comparison : a.CompareTo(b);
        };

        int numRollouts = 0;
        long totalFrames = 0;
        State[] initialStates = new State[numSeeds];
        for (int seed = 0; seed < numSeeds; seed++) initialStates[seed] = new(game, Settings.SEED + (uint)seed);
        Stopwatch sw = Stopwatch.StartNew();
        Parallel.ForEach(Partitioner.Create(flat, EnumerablePartitionerOptions.NoBuffering), () => (int[])range.Clone(), (e, _, sorted) =>
        {
            Script policy = e.Compile();
            if (policy.error != null) throw new InvalidOperationException(policy.error);
            e.scores = new float[numSeeds];
            Array.Fill(e.scores, float.NegativeInfinity);
            lock (sync) Array.Sort(sorted, compareSeeds);

            for (int k = 0; k < numSeeds; k++)
            {
                int seed = sorted[k];
                State state = initialStates[seed].Copy();

                for (int t = 0; t < steps; t++)
                {
                    bool gameover = state.Step(Value.VOID, policy);
                    if (gameover) break;
                }
                e.scores[seed] = state.score;

                lock (sync)
                {
                    int prevSeedTests = seedTests[seed]++;
                    seedScores[seed] = (seedScores[seed] * prevSeedTests + state.score) / (prevSeedTests + 1f);
                    numRollouts++;
                    totalFrames += state.currentFrame;
                    if (bestPolicies[e.length - 1] != null && state.score <= bestPolicies[e.length - 1].minScore) break;
                }
            }

            e.minScore = e.scores.Min(); // Untested seeds keep a rejected policy's score at -Infinity.

            lock (sync)
            {
                PolicyResult bestTotalPolicy = bestPolicies[^1];
                bool improved = bestTotalPolicy == null || e.minScore > bestTotalPolicy.minScore;
                for (int l = e.length - 1; l < bestPolicies.Length; l++) if (bestPolicies[l] == null || e.minScore > bestPolicies[l].minScore) bestPolicies[l] = e;
                if (improved && (baseline == null || e.minScore > baseline.minScore)) Console.WriteLine($"{e.ToString(template.storageNames, game.templates)} scores {e.minScore:0.##} {Expression.ScoreString(e.scores)}");

                int nextProgress = (int)(++completed * (long)THRESHOLDS / flat.Length);
                if (nextProgress > progress)
                {
                    progress = nextProgress;
                    Console.WriteLine($"{progress * (100 / THRESHOLDS)}% done");
                }
            }
            return sorted;
        }, _ => { });
        sw.Stop();
        CS.WriteLine($"simulation time = {sw.ElapsedMilliseconds / 1000f:0.0} seconds", ConsoleColor.DarkRed);

        if (Settings.PRINT_EXPRESSIONS)
        {
            int[] finalSorted = range.OrderBy(n => seedScores[n]).ToArray();
            Console.WriteLine();
            Console.WriteLine("Levels in the order of decreasing difficulty:");
            for (int i = 0; i < finalSorted.Length; i++)
            {
                int seed = finalSorted[i];
                Console.Write($"{seed}[{seedScores[seed]:0.00}]");
                Console.Write(i < finalSorted.Length - 1 ? ", " : Environment.NewLine);
            }
            Console.WriteLine($"numRollouts = {numRollouts} for {flat.Length} policies, so every policy is tested {(float)numRollouts / flat.Length:0.00} times");
        }
        
        Console.WriteLine();
        Console.WriteLine($"Best policies of each {(sketch == null ? "" : "SUB ")}length up to length {maxLength}:");
        for (int l = 0; l < maxLength; l++)
        {
            PolicyResult e = bestPolicies[l];
            if (e != null) Console.WriteLine($"{l+1}. {e.ToString(template.storageNames, game.templates)} scores {e.minScore:0.##}");
            if (e?.sketch != null) Console.WriteLine($"   SUB = {e.Fragment(template.storageNames, game.templates)} (length {e.length})");
        }

        int lastIndex = -1;
        float bestScore = float.NegativeInfinity;
        for (int i = 0; i < flat.Length; i++)
        {
            PolicyResult e = flat[i];
            if (e.minScore > bestScore)
            {
                Evals.UpdateDiscoveries(game.name, e.minScore, e.length, e.Join(), e.ToString(template.storageNames, game.templates), i - lastIndex);
                bestScore = e.minScore;
                lastIndex = i;
            }            
        }

        // Count candidates after the final improvement too; they were evaluated as well.
        if (lastIndex >= 0 && lastIndex < flat.Length - 1)
        {
            PolicyResult last = flat[lastIndex];
            Evals.UpdateDiscoveries(game.name, last.minScore, last.length, last.Join(), last.ToString(template.storageNames, game.templates), flat.Length - 1 - lastIndex);
        }

        return new GameReport(game.name, flat, bestPolicies, sw.ElapsedMilliseconds, numSeeds, steps, numRollouts, totalFrames, template.storageNames, game.templates, sketch);
    }

    ///what would help loop detection a lot: comparing with the previous step, via a built-in a return if the Step() is trivial
    public static float[] Score(Game game, Script policy, int numRuns, int steps)
    {
        float[] result = new float[numRuns];
        //if (!game.isStatic)
        //{
        for (int k = 0; k < result.Length; k++)
        {
            State hare = new (game, Settings.SEED + (uint)k);
            for (int t = 0; t < steps; t++)
            {
                bool gameover = hare.Step(Value.VOID, policy);
                if (gameover) break;
            }
            result[k] = hare.score;
        }
        /*}
        else
        {
            for (int k = 0; k < result.Length; k++)
            {
                State hare = State.InitialState(game, Settings.SEED + (uint)k);
                State tortoise = State.InitialState(game, Settings.SEED + (uint)k);
                for (int t = 0; t < steps; t++)
                {
                    hare.Step(KeyboardState.None, agentTemplate, policy, log: false, autoPlayer: true);
                    if (t % 2 == 1)
                    {
                        tortoise.Step(KeyboardState.None, agentTemplate, policy, log: false, autoPlayer: true);
                        if (State.Same(hare, tortoise)) break; //CS.WriteLine($"FOUND A CYCLE AT STEP {t} FOR {policy}", ConsoleColor.Cyan);
                        //I actually think about having a custom Same() function. For example, in the Pathfinding game we only need to check the position and the direction of the agent, we don't need to check walls. We can just compare a custom set of agent sensors for this. More than that, if we only neeed to check a few sensors, we can save all previous sensor data, and an exact check 100 frames back.
                    }
                }
                result[k] = hare.Score(agentTemplate);
            }
        }*/
        return result;
    }
}
