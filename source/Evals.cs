static partial class Evals
{
    public static void Test(Expr xgame, Expr[] args)
    {
        string gamename = xgame.children[0].head;
        int numSeeds = xgame.GetInt("numSeeds", 5);
        int steps = xgame.GetInt("steps", 3600);
        if (numSeeds < 1 || steps < 1) throw new ArgumentException("numSeeds and steps must be positive");

        Console.WriteLine($"loading game {gamename} ...");
        Game game = Game.LoadFromFile(gamename).game; //first it needs to look in the outside folder
        if (game.error != null)
        {
            Console.WriteLine($"ERROR: {game.error}");
            Environment.ExitCode = 1;
            return;
        }

        Template agent = game.templates[game.agentTemplate];
        if (args == null)
        {
            Console.WriteLine($"testing policy \"{agent.epolicy}\"");

            float[] scores = Search.Score(game, agent.policy, numSeeds, steps);
            float minscore = scores.Min();
            string join = Expression.ScoreString(scores);

            Console.WriteLine($"policy \"{agent.epolicy}\" of length {agent.epolicy.Length()} scores \"{join}\" in {numSeeds} start configurations with the minscore {minscore:0.##}");
            Console.WriteLine();
        }
        else
        {
            //(Codon, TYPE)[] grammar = Record.Grammar(agent, game.templates);
            foreach (Expr arg in args)
            {
                /*if (Settings.GRAMMAR_CHECK)
                {
                    string valid = arg.IsValid(grammar);
                    if (valid != null)
                    {
                        Console.WriteLine($"ERROR: function {valid} is not valid in this game");
                        continue;
                    }
                }
                else
                {*/
                    if (arg.Contains("reward", "putOn", "destroy", "position", "setsize", "drop", "create"))
                    {
                        Console.WriteLine("ERROR: policies should not contain functions with side effects");
                        Environment.ExitCode = 1;
                        continue;
                    }
                //}

                agent.esub = Expression.Create(arg, agent.env, game, []);
                if (agent.esub.error != null)
                {
                    Console.WriteLine($"SYNTAX ERROR: {agent.esub.error}");
                    Environment.ExitCode = 1;
                    continue;
                }
                Console.WriteLine($"testing policy \"{agent.esub.ToString(agent.storageNames, game.templates)}\"");

                agent.policy = Compiler.Run(agent.esub);
                if (agent.policy.error != null)
                {
                    Console.WriteLine($"COMPILE ERROR: {agent.policy.error}");
                    Environment.ExitCode = 1;
                    return;
                }
                Console.WriteLine("compiled successfully");

                float[] scores = Search.Score(game, agent.policy, numSeeds, steps);
                float minScore = scores.Min();
                string scoreJoin = string.Join(' ', scores.Select(j => j.ToString("0.##")));

                int length = agent.esub.Length();
                UpdateDiscoveries(gamename, minScore, length, scoreJoin, agent.esub.ToString(agent.storageNames, game.templates), 1);

                ///тут всегда подразумевается, что тестируется целиковая политика, насколько я понимаю
                Console.WriteLine($"policy \"{agent.esub.ToString(agent.storageNames, game.templates)}\" of length {length} scores \"{scoreJoin}\" in {numSeeds} start configurations with minScore {minScore:0.##}");
                Console.WriteLine();
            }
        }
    }

    public static void UpdateDiscoveries(string gameName, float minScore, int length, string join, string policyText, int incr)
    {
        float bestScoreSoFar = float.NegativeInfinity;
        int runsSoFar = 0;
        string DISCNAME = $"disc-{gameName}.lisp";
        bool discExists = File.Exists(DISCNAME);
        if (discExists)
        {
            string discText = File.ReadAllText(DISCNAME);
            Expr xdisk = Expr.Load($"(list {discText})").expr;
            runsSoFar = int.Parse(xdisk.children[0].head);
            bestScoreSoFar = float.Parse(xdisk.children[^1].children[1].children[0].head);
        }

        //Console.WriteLine($"runsSoFar (without this one) {runsSoFar}, bestScoreSoFar = {bestScoreSoFar}, newScore = {minScore}");
        
        if (minScore > bestScoreSoFar)
        {
            if (discExists)
            {
                List<string> lines = File.ReadAllLines(DISCNAME).ToList();
                lines[0] = (runsSoFar + incr).ToString();
                lines.Add($"(discovery {runsSoFar + incr} (score {minScore}) (length {length}) (scores {join}) {policyText})");
                File.WriteAllLines(DISCNAME, lines);
            }
            else File.WriteAllText(DISCNAME, $"{incr}\r\n(discovery {incr} (score {minScore}) (length {length}) (scores {join}) {policyText})");
        }
        else
        {
            string[] lines = File.ReadAllLines(DISCNAME);
            lines[0] = (runsSoFar + incr).ToString();
            File.WriteAllLines(DISCNAME, lines);
        }
    }
}
