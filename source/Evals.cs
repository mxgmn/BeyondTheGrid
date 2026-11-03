using System.Text;

static class Evals
{
    static string Prompt(Game game, int numSeeds, int length, bool tool)
    {
        StringBuilder prompt = new();
        Template agent = game.templates[game.agentTemplate];
        if (tool)
        {
            prompt.AppendLine($"Your goal is to reach the highest possible minscore in the game \"{game.name}\" (best minScore across {numSeeds} initializations).");
            prompt.AppendLine($"Specifically, you need to write a policy program for the {agent.name} that performs the best in this game, in a special LISP-like DSL.");
            prompt.AppendLine("You can learn this DSL by looking at Node.cs and Script.cs. Here are also some example policies, so you would understand the DSL syntax:");
            foreach (string policy in agent.Policies()) prompt.AppendLine(policy);
            prompt.AppendLine("You're only allowed to use functions from Node.Grammar + sensors + creature functions.");
            prompt.AppendLine($"Notice that you cannot use arbitrary constants in this DSL, you can only use 'constant', which evaluates to {Game.constant}.");
            prompt.AppendLine("To know your score, run BeyondTheGrid.exe with one or several policies as command line arguments. For example, BeyondTheGrid.exe \"(go (left (dir self)))\" \"(go (right (dir self)))\". Then the score will be printed into the console.");
            prompt.AppendLine($"To aid you in your search, there is a powerful search tool. To use this tool, call something like BeyondTheGrid.exe \"(search (subtype ACTION) (shape (if (< constant (distance self cat)) SUB (go (maxDirection (rundist #0 Cat))))) (length 15))\", and then the tool will search all expressions up to length 15 and substitute them in place of SUB. What this means is that you can take a guess about a general shape of the policy, and let the search tool fill the gaps / unknown slots.");
            prompt.AppendLine("In fact, the first action you should take (after familiarizing yourself with the DSL) is to do a broad search from the root: BeyondTheGrid.exe \"(search (subtype ACTION) (shape SUB))\", which will prints out the best expressions of each length. If you're unsure about the length, just don't specify it - the default parameter is going to be used.");
            prompt.AppendLine("In addition to the action subexpressions, you can also search for BOOL subexpressions: BeyondTheGrid.exe \"(search (subtype BOOL) (shape (if SUB (go (from cat)) (go (maxDirection (rundist #0 Cat))))))\".");
            prompt.AppendLine("Another useful tool is antiPatterns. For example, in the search (search (subtype ACTION) (shape (if empty (if amAttacker (goto target) SUB) use)) (antiPatterns empty amAttacker) (length 5)), the SUB is under amAttacker and empty conditions - and we don't want SUB to include these expressions. AntiPatterns can be any expression that you don't want to see in SUB. Using antiPatterns makes the search space smaller.");
            prompt.AppendLine("You don't need to 'verify' search results, you can trust them, search uses the same evaluators as basic policy tests.");
            prompt.AppendLine("However, you cannot run multiple searches in parallel, as each search uses all CPU cores, and writes to the same file. You should only run searches sequentially.");
            prompt.AppendLine($"Remember that you are maximizing the minimum score across {numSeeds} seeds, and not the average score.");
            prompt.AppendLine("Do not read, list, search, or reference anything outside the current working folder.");
            prompt.AppendLine("After the task is done, document your approach / strategy and search history / discoveries in REPORT.md, just a few paragraphs. This report file is the ONLY file you're allowed to modify.");
        }
        else// if (Settings.GRAMMAR_CHECK)
        {
            prompt.AppendLine($"Your goal is to reach the highest possible minscore in the game \"{game.name}\" (best minScore across {numSeeds} initializations).");
            prompt.AppendLine($"Specifically, you need to write a policy program for the {agent.name} that performs the best in this game, in a special LISP-like DSL.");
            prompt.AppendLine("You can learn this DSL by looking at Node.cs and Script.cs. Here are also some example policies, so you would understand the DSL syntax:");
            foreach (string policy in agent.Policies()) prompt.AppendLine(policy);
            prompt.AppendLine("You're only allowed to use functions from Node.Grammar + sensors + creature functions.");
            prompt.AppendLine($"Notice that you cannot use arbitrary constants in this DSL, you can only use 'constant', which evaluates to {Game.constant}.");
            prompt.AppendLine("To know your score, run BeyondTheGrid.exe with one or several policies as command line arguments. For example, BeyondTheGrid.exe \"(go (left (dir self)))\" \"(go (right (dir self)))\". Then the score will be printed into the console.");
            prompt.AppendLine($"Please note that policy evaluation requires {numSeeds} rollouts, and that takes time - a common failure mode in this task is to give BeyondTheGrid too many policies to evaluate, and it never finishes.");
            prompt.AppendLine($"Remember that you are maximizing the minimum score across {numSeeds} seeds, and not the average score.");
            prompt.AppendLine("Do not read, list, search, or reference anything outside the current working folder.");
            prompt.AppendLine("After the task is done, document your approach / strategy and search history / discoveries in REPORT.md, just a few paragraphs. This report file is the ONLY file you're allowed to modify.");
        }
        /*else //not sure what is this for
        {
            prompt.AppendLine($"Your goal is to reach the highest possible minscore in the game \"{game.name}\" (best minScore across {numSeeds} initializations).");
            prompt.AppendLine($"Specifically, you need to write a policy program for the {game.agentTemplate.name} that performs the best in this game, in a special LISP-like DSL.");
            prompt.AppendLine("You can learn this DSL by looking at Node.cs and Script.cs. Here are also some example policies, so you would understand the DSL syntax:");
            foreach (string policy in game.agentTemplate.Policies()) prompt.AppendLine(policy);
            prompt.AppendLine($"You can also check previously discovered best policies in disc-{game.name}.lisp. Don't forget that you can define values using 'let'.");
            prompt.AppendLine("To know your score, run BeyondTheGrid.exe with one or several policies as command line arguments. For example, BeyondTheGrid.exe \"(go (left (dir self)))\" \"(go (right (dir self)))\". Then the score will be printed into the console.");
            prompt.AppendLine($"Please note that policy evaluation requires {numSeeds} rollouts, and that takes time - a common failure mode in this task is to give BeyondTheGrid too many policies to evaluate, and it never finishes.");
            prompt.AppendLine($"Remember that you are maximizing the minimum score across {numSeeds} seeds, and not the average score.");
            prompt.AppendLine("Do not read, list, search, or reference anything outside the current working folder.");
            prompt.AppendLine("After the task is done, document your approach / strategy and search history / discoveries in REPORT.md, just a few paragraphs. This report file is the ONLY file you're allowed to modify.");
        }*/
        return prompt.ToString();
    }

    public static void Prepare(Expr xgame)
    {
        bool tool = xgame.children[^1].head == "tool";
        string date = DateTime.Now.ToString("yyyy-MM-dd");

        string gameName = xgame.children[0].head;
        int length = xgame.GetInt("length", 11);
        int steps = xgame.GetInt("steps", 3600);
        int numSeeds = xgame.GetInt("numSeeds", 5);
        
        string foldername = $"evals/{date}-{gameName}-";
        foldername += tool ? "tool" : "simple";
        Directory.CreateDirectory(foldername);

        string[] fs = ["BeyondTheGrid.exe", "BeyondTheGrid.dll", "BeyondTheGrid.runtimeconfig.json", "../Node.cs", "../Script.cs"];
        foreach (string f in fs) File.Copy(f, $"{foldername}/{Path.GetFileName(f)}", overwrite: true);

        (Game game, string gamecode) = Game.LoadFromFile(gameName);
        if (game.error != null) throw new Exception($"failed to load game: {game.error}");
        File.WriteAllText($"{foldername}/{gameName}.lisp", gamecode + Environment.NewLine);
        File.WriteAllText($"{foldername}/games.lisp", $"(test \"{gameName}\" (length {length}) (numSeeds {numSeeds}) (steps {steps}))" + Environment.NewLine);

        File.WriteAllText($"{foldername}/prompt.txt", Prompt(game, numSeeds, length, tool));
        Console.WriteLine($"created {foldername}");
    }

    public static void Test(Expr xgame, Expr[] args)
    {
        string gamename = xgame.children[0].head;
        int numSeeds = xgame.GetInt("numSeeds", 5);
        int steps = xgame.GetInt("steps", 3600);

        Console.WriteLine($"loading game {gamename} ...");
        Game game = Game.LoadFromFile(gamename).game; //first it needs to look in the outside folder
        if (game.error != null)
        {
            Console.WriteLine($"ERROR: {game.error}");
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
                        continue;
                    }
                //}

                agent.esub = Expression.Create(arg, agent.env, game, []);
                if (agent.esub.error != null)
                {
                    Console.WriteLine($"SYNTAX ERROR: {agent.esub.error}");
                    continue;
                }
                Console.WriteLine($"testing subpolicy \"{agent.esub.ToString(null, game.templates)}\"");

                agent.policy = Compiler.Run(agent.esub);
                if (agent.policy.error != null)
                {
                    Console.WriteLine($"COMPILE ERROR: {agent.policy.error}");
                    return;
                }
                Console.WriteLine("compiled successfully");

                float[] scores = Search.Score(game, agent.policy, numSeeds, steps);
                float minScore = scores.Min();
                string scoreJoin = string.Join(' ', scores.Select(j => j.ToString("0.##")));

                int length = agent.esub.Length();
                UpdateDiscoveries(gamename, minScore, length, scoreJoin, agent.esub.ToString(agent.storageNames, game.templates), 1);

                ///тут всегда подразумевается, что тестируется целиковая политика, насколько я понимаю
                Console.WriteLine($"policy \"{agent.esub.ToString(null, game.templates)}\" of length {length} scores \"{scoreJoin}\" in {numSeeds} start configurations with minScore {minScore:0.##}");
                Console.WriteLine();
            }
        }
    }

    public static void UpdateDiscoveries(string gameName, float minScore, int length, string join, string policyText, int incr)
    {
        float bestScoreSoFar = -1000000f;
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
            else File.AppendAllText(DISCNAME, $"1\r\n(discovery 1 (score {minScore}) (length {length}) (scores {join}) {policyText})");
        }
        else
        {
            string[] lines = File.ReadAllLines(DISCNAME);
            lines[0] = (runsSoFar + incr).ToString();
            File.WriteAllLines(DISCNAME, lines);
        }
    }
}
