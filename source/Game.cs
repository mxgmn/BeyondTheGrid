using System.Numerics;

class Game : Error
{
    public Vector2 size;
    public List<Template> templates;
    public int agentTemplate;
    public Script init, onFrame;

    public List<string> emojiSymbols;
    public string name;
    public const float constant = 2.5f; ///мы держим это тут, потому что генератор промпта тоже ссылается на эту константу
    public bool PHYSICS;

    Game() { }

    static Game Error(string message)
    {
        Game result = new();
        result.error = message;
        return result;
    }

    static Game Load(Expr root, string name)
    {
        Game game = new();

        game.name = name;
        game.size = new Vector2(8, 8);
        game.templates = [];

        game.emojiSymbols = [];
        game.PHYSICS = root.Contains("throw", "drop");

        Env<TYPE> env = new();

        Expr[] xtemplates = root.children.Where(x => x.head == "template").ToArray();
        for (byte i = 0; i < xtemplates.Length; i++)
        {
            string templateName = xtemplates[i].children[0].head;
            int index = game.templates.FindIndex(t => t.name == templateName);
            if (index >= 0) return Error($"template <{templateName}> is declared multiple times");
            else game.templates.Add(new Template(templateName));
        }
        for (int i = 0; i < xtemplates.Length; i++)
        {
            string error = game.templates[i].Load(xtemplates[i], game, CH.PICORotated(game.templates.Count));
            if (error != null) return Error(error);
        }

        List<Expr> xinits = [];
        for (int i = 0; i < root.children.Length; i++)
        {
            Expr child = root.children[i];
            string head = child.head;

            if (head == "template") { }
            else if (head == "store" || head == "create" || head == "for" || head == "position" || head == "foreach" || head == "apply" || head == "markov" || head == "par" || head == "vox" || head == "rect" || head == "load") xinits.Add(child);
            else if (head == "setsize")
            {
                Script sizeX = Compiler.Run(child.children[0], env, game, []);
                Script sizeY = Compiler.Run(child.children[1], env, game, []);
                MyStack temp = new(8);
                Value vx = sizeX.Execute(null, -1, null, [], stack: temp);
                Value vy = sizeY.Execute(null, -1, null, [], stack: temp);
                game.size = new Vector2(vx.v.X, vy.v.X);
            }
            else if (head == "onFrame")
            {
                game.onFrame = Compiler.Run(child.children[0], env, game, []);
                if (game.onFrame.error != null) throw new Exception($"syntax error in onFrame: <{game.onFrame.error}>");
            }
            else if (head == "player")
            {
                string playerName = child.children[0].head;
                game.agentTemplate = game.templates.FindIndex(t => t.name == playerName);
                if (game.agentTemplate < 0) throw new Exception($"player template {playerName} not found");
            }
            else throw new Exception($"unknown game constructor head <{head}>");
        }

        Expr xinit = new("seq", xinits.ToArray());
        game.init = Compiler.Run(xinit, env, game, []);
        if (game.init.error != null) throw new Exception($"syntax error in init code: <{game.init.error}>");

        return game;
    }

    public static (Game game, string gamecode) LoadFromFile(string gamename)
    {
        string filename = $"{gamename}.lisp", filepath = null;
        if (File.Exists($"../{filename}")) filepath = $"../{filename}";
        else if (File.Exists(filename)) filepath = filename;
        else if (File.Exists($"games/{gamename}.lisp")) filepath = $"games/{gamename}.lisp";
        else return (Error($"no such game <{gamename}>"), null);

        string filetext = File.ReadAllText(filepath);
        (Expr gameroot, string error) = Expr.Load($"(game {filetext})");
        if (error != null) return (Error(error), null);

        Game game = Load(gameroot, gamename);
        return (game, gameroot.GameCode().TailString());
    }

    public float SceneSDF(Vector2 p) => MathF.Min(MathF.Min(0.5f * size.X + p.X, 0.5f * size.X - p.X), MathF.Min(0.5f * size.Y + p.Y, 0.5f * size.Y - p.Y)); ///это всего лишь расстояние от точки до границы сцены

    public (int x, int y) TileFromPoint(Vector2 v) => (Helper.Round(v.X + 0.5f * size.X), Helper.Round(v.Y + 0.5f * size.Y));
    public (int x, int y) TileFromPoint(Vector3 v) => (Helper.Round(v.X + 0.5f * size.X), Helper.Round(v.Y + 0.5f * size.Y));
    public Vector2 PointFromTile(int X, int Y) => new(X + 0.5f - 0.5f * size.X, Y + 0.5f - 0.5f * size.Y);
}
