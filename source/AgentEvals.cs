using System.Text;

static partial class Evals
{
    public static void SearchSketch(Expr xgame, Expr request)
    {
        foreach (Expr option in request.children)
        {
            if (option.head != "shape" && option.head != "subtype" && option.head != "length" && option.head != "antiPatterns")
                throw new ArgumentException($"Unknown search option: {option.head}");
            if (request.children.Count(c => c.head == option.head) > 1) throw new ArgumentException($"Duplicate search option: {option.head}");
            if (option.head != "antiPatterns" && option.children.Length != 1) throw new ArgumentException($"{option.head} requires one argument");
        }
        Expr shapeOption = request.Child("shape");
        if (shapeOption == null || shapeOption.children.Length != 1)
            throw new ArgumentException("Search requires (shape <policy containing SUB>)");
        Expr subtype = request.Child("subtype");
        string typeName = subtype == null ? "ACTION" : subtype.children.Length == 1 ? subtype.children[0].head : "";
        TYPE type = typeName switch
        {
            "ACTION" => TYPE.TUPLE,
            "BOOL" => TYPE.BOOL,
            _ => throw new ArgumentException("Search subtype must be ACTION or BOOL")
        };
        Expr xshape = shapeOption.children[0];
        if (!xshape.Contains("SUB")) throw new ArgumentException("Search shape must contain SUB");
        if (xshape.Contains("reward", "putOn", "destroy", "position", "setsize", "drop", "create"))
            throw new ArgumentException("Policies should not contain functions with side effects");
        string gameName = xgame.children[0].head;
        Game game = Game.LoadFromFile(gameName).game;
        if (game.error != null) throw new ArgumentException(game.error);
        Template agent = game.templates[game.agentTemplate];
        Expression shape = Expression.Create(xshape, agent.env, game, [], type);
        if (shape.error != null) throw new ArgumentException($"SHAPE ERROR: {shape.error}");
        Expression[] patterns = (request.Child("antiPatterns")?.children ?? []).Select(p => Expression.Create(p, agent.env, game, [TYPE.VECTOR])).ToArray();
        foreach (Expression pattern in patterns)
            if (pattern.error != null) throw new ArgumentException($"ANTIPATTERN ERROR: {pattern.error}");
        int length = request.GetInt("length", xgame.GetInt("length", 10));
        int numSeeds = xgame.GetInt("numSeeds", 5), steps = xgame.GetInt("steps", 3600);
        Console.WriteLine($"Searching {typeName} expressions for SUB, length <= {length}, {numSeeds} seeds, {steps} steps");
        Console.WriteLine($"Shape: {shape.ToString(agent.storageNames, game.templates)}");
        GameReport result = Search.BottomUp(game, 1, length, Record.Grammar(agent, game.templates), numSeeds, steps, patterns, shape);
        Report report = new();
        report.Add(result);
        report.Save();
    }

    static string Prompt(Game game, int numSeeds, int length, bool tool)
    {
        Template agent = game.templates[game.agentTemplate];
        StringBuilder prompt = new();
        prompt.AppendLine($"Write a policy for {agent.name} in {game.name} that maximizes the minimum score across {numSeeds} random seeds.");
        prompt.AppendLine($"Read {game.name}.lisp for the game rules, OP.cs (Record.Grammar) and Expression.cs for the DSL, and Script.cs for execution semantics.");
        prompt.AppendLine("Use only operations from Record.Grammar, the agent's sensors, and its listed functions. Do not modify the game rules or award rewards directly.");
        prompt.AppendLine($"Use 'constant' (which evaluates to {Game.constant}) rather than arbitrary numeric constants.");
        prompt.AppendLine("Example policies from this game:");
        foreach (string policy in agent.Policies()) prompt.AppendLine(policy);
        prompt.AppendLine("Evaluate complete policies with one or several quoted arguments, for example:");
        prompt.AppendLine(".\\BeyondTheGrid.exe \"(go (left (dir self)))\" \"(go (right (dir self)))\"");
        prompt.AppendLine("The output gives the score on each seed and the minimum score. Large batches can take significant time.");
        if (tool)
        {
            prompt.AppendLine("You also have a bottom-up search tool that fills a typed SUB hole in a policy sketch. Start with a broad root search:");
            prompt.AppendLine(".\\BeyondTheGrid.exe \"(search (subtype ACTION) (shape SUB))\"");
            prompt.AppendLine($"The default maximum SUB expression length is {length}; override it with (length N). Length counts expression nodes, not the size of the surrounding sketch.");
            prompt.AppendLine("For a conditional action sketch, replace a branch with SUB and use (subtype ACTION). For a Boolean condition, use (subtype BOOL) with (shape (if SUB <action> <action>)). Replace the angle-bracketed parts with actual policies.");
            prompt.AppendLine("Optional (antiPatterns expr1 expr2 ...) excludes those subexpressions from generated fragments. Conditions already known in the hole's branch are useful antipatterns. These exclusions do not apply to the fixed sketch.");
            prompt.AppendLine("The tool returns the best fragment and completed policy for each size bound, with scores from executing the completed policy. Use this feedback to revise the sketch, raise the bound, or change the antipatterns.");
            prompt.AppendLine("Run searches sequentially: each search uses all CPU cores and shares discovery files with direct evaluation.");
        }
        prompt.AppendLine($"The experiment has a {Settings.AGENT_TIMEOUT:0}-minute wall-clock budget starting with the first evaluation. Stop at the deadline; the caller must enforce the deadline for an already running command.");
        prompt.AppendLine("Do not read, list, search, or reference anything outside this folder. The only file you should edit is REPORT.md; write a few paragraphs about your approach and discoveries. The executable writes its own logs and results.");
        return prompt.ToString();
    }

    public static void Prepare(Expr xgame)
    {
        bool tool = xgame.children.Any(c => c.head == "tool");
        string gameName = xgame.children[0].head;
        int length = xgame.GetInt("length", 10), steps = xgame.GetInt("steps", 3600), numSeeds = xgame.GetInt("numSeeds", 5);
        if (length < 1 || steps < 1 || numSeeds < 1) throw new ArgumentException("length, steps and numSeeds must be positive");
        (Game game, string gameCode) = Game.LoadFromFile(gameName);
        if (game.error != null) throw new ArgumentException(game.error);
        string binaries = Path.GetDirectoryName(typeof(Evals).Assembly.Location);
        string source = Directory.Exists(Path.Combine(binaries, "dsl")) ? Path.Combine(binaries, "dsl") : Path.GetFullPath("source");
        if (!File.Exists(Path.Combine(source, "OP.cs"))) throw new IOException("Cannot find DSL source files for the experiment folder. Rebuild the project first.");
        string folder = Path.Combine("evals", $"{DateTime.Now:yyyy-MM-dd-HHmmss-fffffff}-{gameName}-{(tool ? "tool" : "simple")}");
        Directory.CreateDirectory(folder);
        foreach (string file in Directory.GetFiles(binaries))
            if (Path.GetExtension(file) is ".dll" or ".exe" || file.EndsWith(".deps.json") || file.EndsWith(".runtimeconfig.json"))
                File.Copy(file, Path.Combine(folder, Path.GetFileName(file)));
        string runtimes = Path.Combine(binaries, "runtimes");
        if (Directory.Exists(runtimes))
            foreach (string file in Directory.GetFiles(runtimes, "*", SearchOption.AllDirectories))
            {
                string destination = Path.Combine(folder, Path.GetRelativePath(binaries, file));
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                File.Copy(file, destination);
            }
        foreach (string file in Directory.GetFiles(source, "*.cs")) File.Copy(file, Path.Combine(folder, Path.GetFileName(file)));
        File.WriteAllText(Path.Combine(folder, $"{gameName}.lisp"), gameCode + Environment.NewLine);
        File.WriteAllText(Path.Combine(folder, "games.lisp"), $"(test \"{gameName}\" (length {length}) (numSeeds {numSeeds}) (steps {steps}) (timeLimitMinutes {Settings.AGENT_TIMEOUT:0}))" + Environment.NewLine);
        File.WriteAllText(Path.Combine(folder, "prompt.txt"), Prompt(game, numSeeds, length, tool));
        Console.WriteLine($"created {Path.GetFullPath(folder)}");
    }
}
