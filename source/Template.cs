using System.Numerics;

enum SHAPE { RECT, SPHERE, CYLINDER, CUBOID, CAPSULE, CONE, NONE };

class Template
{
    public string name, displayName;
    public Vector3 size, color;
    public float speed, maxhp, visionCos, hearingRad;
    public bool invisible, blocksVision, passable; ///passable - чтобы слишком маленькие объекты не препятствовали движению, как в ProtectTheChild
    public SHAPE shape;
    public int textureID;

    public List<(string name, Script script, int memoryIndex, bool visual, bool required)> lets, sensors;
    public List<OP> functions;

    public Env<TYPE> env;
    public Expression epolicy, esub;
    public Script policy, onCollision, onUse, onFrame, onDeath; ///onMove;

    public Expression[] patterns;
    public string[] storageNames;

    public Template(string name)
    {
        this.name = name; ///when a Template is originally created, only its name is set, and nothing else. This is needed because templates can cross-reference each other
    }

    public string Load(Expr xtemplate, Game game, Vector3 color)
    {
        displayName = name;
        size = Vector3.One;
        this.color = color;
        speed = 1f;
        maxhp = 1f;
        shape = SHAPE.CUBOID;
        textureID = -1;

        env = new();
        lets = []; ///разница в том, что сенсоры перевычисляются каждый ход, а ячейка памяти как правило сохраняются от хода к ходу, и только иногда переписываются
        sensors = [];
        functions = [];

        visionCos = -1f; ///-1 means that everything is visible
        hearingRad = 0.75f; ///objects inside the hearing radius are visible even if they are outside of the cone of vision

        for (int n = 1; n < xtemplate.children.Length; n++) ///start with 1 because name is already known
        {
            Expr xtop = xtemplate.children[n];

            if (xtop.head == "let" || xtop.head == "sensor")
            {
                if (xtop.children.Length < 2) return $"wrong number {xtop.children.Length} of sensor/let arguments";

                bool visual = false, required = false;
                for (int i = 2; i < xtop.children.Length; i++)
                {
                    Expr flag = xtop.children[i];
                    if (!flag.isWord || flag.children.Length != 0) return $"invalid sensor/let keyword {flag}";
                    if (flag.head == "visual") visual = true;
                    else if (flag.head == "required") required = true;
                    else return $"unknown sensor/let keyword {flag.head}";
                }

                string varname = xtop.children[0].head;
                Expression e = Expression.Create(xtop.children[1], env, game, []);
                Script script = Compiler.Run(e);
                if (script.error != null) return script.error;

                int memoryIndex = env.Define(varname, script.type);
                if (memoryIndex < 0) return $"sensor/let {varname} is already defined";

                var list = xtop.head == "let" ? lets : sensors;
                list.Add((varname, script, memoryIndex, visual, required));
            }
            else if (xtop.head == "functions")
            {
                foreach (var child in xtop.children)
                {
                    string funcname = child.head;
                    Record record = Record.Find(funcname);
                    if (record == null) return $"unknown function <{funcname}>";
                    functions.Add(record.op);
                }
            }
            else if (xtop.head == "name") displayName = xtop.children[0].head;
            else if (xtop.head == "sphere")
            {
                float diameter = float.Parse(xtop.children[0].head);
                size.X = diameter;
                size.Y = diameter;
                size.Z = diameter;
                shape = SHAPE.SPHERE;
            }
            else if (xtop.head == "cylinder")
            {
                size.X = float.Parse(xtop.children[0].head);
                size.Y = size.X;
                size.Z = float.Parse(xtop.children[1].head);
                shape = SHAPE.CYLINDER;
            }
            else if (xtop.head == "capsule")
            {
                size.X = float.Parse(xtop.children[0].head);
                size.Y = size.X;
                size.Z = float.Parse(xtop.children[1].head);
                shape = SHAPE.CAPSULE;
            }
            else if (xtop.head == "cuboid")
            {
                size.X = float.Parse(xtop.children[0].head);
                size.Y = float.Parse(xtop.children[1].head);
                size.Z = float.Parse(xtop.children[2].head);
                shape = SHAPE.CUBOID;
            }
            else if (xtop.head == "color")
            {
                string head = xtop.children[0].head;
                if (xtop.children[0].children.Length > 0) throw new Exception("color scripts are deprecated");
                //{
                //    Expr xcolor = xtop.children[0];
                //    temp.colorScript = Compiler.Run(xcolor, tenv, game);
                //    if (temp.colorScript.error != null) return Error($"error in color script: <{temp.colorScript.error}>");
                //}
                else
                {
                    if (head.Length == 6 || head.Length == 7) this.color = CH.VecColorFromHex(head);
                    else throw new Exception($"unrecognized color format {head}");
                }
            }
            else if (xtop.head == "speed") speed = float.Parse(xtop.children[0].head);
            else if (xtop.head == "seq" || xtop.head == "if" || xtop.head == "turn" || xtop.head == "go" || xtop.head == "goto" || xtop.head == "SUB")
            {
                if (epolicy != null) CS.WriteLine($"WARNING: Overwriting policy shape for {name}", ConsoleColor.DarkYellow);
                epolicy = Expression.Create(xtop, env, game, []);
                if (epolicy.error != null) return epolicy.error;
            }
            else if (xtop.head == "sub")
            {
                esub = Expression.Create(xtop.children[0], env, game, []);
                if (esub.error != null) return esub.error;
            }
            else if (xtop.head == "onCollision")
            {
                onCollision = Compiler.Run(xtop.children[0], env, game, [TYPE.OBJECT]);
                if (onCollision.error != null) return $"error compiling onCollision: <{onCollision.error}>";
            }
            else if (xtop.head == "onDeath")
            {
                onDeath = Compiler.Run(xtop.children[0], env, game, []);
                if (onDeath.error != null) return $"error compiling onDeath: <{onDeath.error}>";
            }
            else if (xtop.head == "onUse")
            {
                onUse = Compiler.Run(xtop.children[0], env, game, []);
                if (onUse.error != null) return $"error compiling onUse: <{onUse.error}>";
            }
            else if (xtop.head == "onFrame")
            {
                onFrame = Compiler.Run(xtop.children[0], env, game, []);
                if (onFrame.error != null) return $"error compiling onFrame: <{onFrame.error}>";
            }
            /*else if (xtop.head == "onMove")
            {
                onMove = Compiler.Run(xtop.children[0], env, game, []);
                if (onMove.error != null) return $"error compiling onMove: <{onMove.error}>";
            }*/
            else if (xtop.head == "passable") passable = true;
            else if (xtop.head == "visionCos") visionCos = float.Parse(xtop.children[0].head);
            else if (xtop.head == "blocksVision") blocksVision = true;
            else if (xtop.head == "invisible") invisible = true;
            //else if (xtop.head == "action") actions.Add(new Action(xtop));
            //else if (xtop.head == "onDrop") onDrop = new Trigger(xtop, game);
            //else if (xtop.head == "onEquip") onEquip = new Trigger(xtop, game);
            else if (xtop.head == "maxhp") maxhp = int.Parse(xtop.children[0].head);
            else if ((xtop.head.Length == 1 || xtop.head.Length == 2) && xtop.children.Length == 0)
            {
                if (!game.emojiSymbols.Contains(xtop.head)) game.emojiSymbols.Add(xtop.head);
                textureID = game.emojiSymbols.IndexOf(xtop.head);
            }
            else if (xtop.head == "patterns")
            {
                patterns = new Expression[xtop.children.Length];
                for (int i = 0; i < xtop.children.Length; i++)
                {
                    Expr xpattern = xtop.children[i];
                    Expression e = Expression.Create(xpattern, env, game, []);
                    if (e.error != null) return $"cannot load pattern {xpattern}: {e.error}";
                    patterns[i] = e;
                }
            }
            else if (xtop.head == "ignore") { }
            else throw new Exception($"unknown head <{xtop.head}> of length {xtop.head.Length} in template: {xtop}");
        }

        if (esub != null) ///epolicy просто означает, что в темплейте задана политика
        {
            policy = Compiler.Run(epolicy);
            if (policy.error != null) return $"error compiling {name} policy: <{policy.error}>";
        }
        else if (epolicy != null)
        {
            policy = Compiler.Run(epolicy);
            CS.WriteLine($"{name} policy = {policy}", ConsoleColor.Cyan);
        }
        if (patterns == null) patterns = [];
        storageNames = env.data.Select(p => p.name).ToArray();
        return null;
    }

    public override string ToString() => name;

    public List<string> Policies()
    {
        List<string> result = [];
        result.Add("(go (dir self))");
        result.Add("(turn (dir self))");
        result.Add("(go (left (dir self)))");
        if (functions.Exists(op => op == OP.RAYDIST)) result.Add("(go (maxDirection (raydist #0)))");
        foreach ((string sname, Script script, _, _, _) in sensors)
        {
            if (script.type == TYPE.OBJECT)
            {
                result.Add($"(goto {sname})");
                result.Add($"(go (from {sname}))");
            }
            else if (script.type == TYPE.BOOL)
            {
                result.Add($"(if {sname} (...) (...))");
            }
        }
        return result;
    }
}
