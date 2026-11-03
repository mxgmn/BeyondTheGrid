struct Node
{
    public OP op;
    public int opvariant;
    public int[] ch;
    public TYPE otype, dbtype; ///otype is the result type; dbtype is used by enumeration to track free inputs
    public int length;

    public Node(OP op, int opvariant, int[] ch, TYPE dbtype = TYPE.VOID)
    {
        this.op = op;
        this.opvariant = opvariant;
        this.ch = ch;
        this.dbtype = dbtype;
        otype = op == OP.STORE || op == OP.SEQ ? TYPE.VOID : Record.dict[op].otype;
        length = 1;
    }
}

partial class Expression : Error
{
    public List<Node> nodes;
    public List<float> floats;
    public List<string> strings;

    public Expression()
    {
        nodes = [];
        floats = [];
        strings = [];
    }

    public TYPE GetType(int index) => nodes[index].otype;

    public int Add(Node node)
    {
        node.length = 1;
        for (int i = 0; i < node.ch.Length; i++) node.length += nodes[node.ch[i]].length;
        nodes.Add(node);
        return nodes.Count - 1;
    }

    public static Expression Create(Expr expr, Env<TYPE> env, Game game, TYPE[] db)
    {
        Expression result = new();
        result.Load(expr, env, game, db);
        return result;
    }

    int Error(string message)
    {
        error = message;
        return -1;
    }

    int Load(Expr expr, Env<TYPE> env, Game game, TYPE[] db)
    {
        (int storageIndex, TYPE has) = env.Has(expr.head);
        int templateIndex = game.templates.FindIndex(t => t.name == expr.head);

        if (expr.head == "=")
        {
            if (expr.children.Length != 2) return Error($"wrong let syntax: <{expr}>");
            if (expr.children[0].children.Length > 0) return Error($"first argument <{expr.children[0]}> in let should just be a variable name");
            string varname = expr.children[0].head;

            int child = Load(expr.children[1], env, game, db);
            if (error != null) return -1;
            int memoryIndex = env.Set(varname, nodes[child].otype);
            if (memoryIndex < 0) return Error($"cannot define a new variable <{varname}> with =, should use let");

            return Add(new(OP.STORE, memoryIndex, [child]));
        }
        else if (expr.head == "if")
        {
            if (expr.children.Length == 2 || expr.children.Length == 3)
            {
                int[] ch = new int[expr.children.Length];
                for (int i = 0; i < ch.Length; i++)
                {
                    ch[i] = Load(expr.children[i], env, game, db);
                    if (error != null) return -1;
                }
                if (nodes[ch[0]].otype != TYPE.BOOL) return Error($"the first arg to if in <{expr}> should be bool");
                if (nodes[ch[1]].otype != TYPE.TUPLE && nodes[ch[1]].otype != TYPE.VOID && nodes[ch[1]].otype != TYPE.OBJECT) return Error($"the second arg to if in <{expr}> should be action/tuple, not {nodes[ch[1]].otype}");
                if (ch.Length >= 3 && nodes[ch[2]].otype != TYPE.TUPLE && nodes[ch[2]].otype != TYPE.VOID) return Error($"the third arg {ToString(ch[2], null, game.templates)} in <{expr}> should be action/tuple, not {nodes[ch[2]].otype}");
                return Add(new(OP.IF, -1, ch));
            }
            else if (expr.children.Length >= 4)
            {
                Expr reduced = new("if", [expr.children[0], expr.children[1], new Expr("if", expr.children[2..])]);
                return Load(reduced, env, game, db);
            }
            else return Error($"wrong number {expr.children.Length} of arguments in {expr.head}");
        }
        else if (expr.head == "and" || expr.head == "or")
        {
            if (expr.children.Length == 0) return Error($"wrong number {expr.children.Length} of arguments in {expr.head}");
            else if (expr.children.Length == 1) return Load(expr.children[0], env, game, db);
            else if (expr.children.Length == 2)
            {
                int[] ch = new int[expr.children.Length];
                TYPE[] itypes = new TYPE[expr.children.Length];
                for (int i = 0; i < ch.Length; i++)
                {
                    ch[i] = Load(expr.children[i], env, game, db);
                    if (error != null) return -1;
                    itypes[i] = nodes[ch[i]].otype;
                }
                return Add(new(expr.head == "and" ? OP.AND : OP.OR, -1, ch));
            }
            else if (expr.children.Length >= 3)
            {
                Expr reduced = new(expr.head, [expr.children[0], new Expr(expr.head, expr.children[1..])]);
                return Load(reduced, env, game, db);
            }
            else throw new Exception("cannot happen");
        }
        else if (expr.head == "-=" || expr.head == "+=")
        {
            int[] ch = new int[expr.children.Length];
            for (int i = 0; i < ch.Length; i++)
            {
                ch[i] = Load(expr.children[i], env, game, db);
                if (error != null) return -1;
            }
            int e1 = Add(new(expr.head == "-=" ? OP.MINUS_FF : OP.PLUS_FF, -1, [ch[0], ch[1]]));
            return Add(new(OP.STORE, nodes[ch[0]].opvariant, [e1]));
        }
        else if (expr.head == "!=")
        {
            int e0 = Load(expr.children[0], env, game, db);
            if (error != null) return -1;
            int e1 = Load(expr.children[1], env, game, db);
            if (error != null) return -1;
            if (nodes[e0].otype != TYPE.OBJECT || nodes[e1].otype != TYPE.OBJECT) return Error($"!= can only be applied to objects");
            return Add(new(OP.NOT, -1, [Add(new(OP.EEQ_O, -1, [e0, e1]))]));
        }
        else if (expr.head == "seq")
        {
            if (expr.children.Length == 0) throw new Exception($"seq should have at least 2 arguments {expr}");
            else if (expr.children.Length == 1) return Load(expr.children[0], env, game, db);
            else
            {
                int[] ch = new int[expr.children.Length];
                for (int i = 0; i < ch.Length; i++)
                {
                    ch[i] = Load(expr.children[i], env, game, db);
                    if (error != null) return -1;
                }
                return Add(new(OP.SEQ, -1, ch));
            }
        }
        else if (expr.head == "markov")
        {
            if (expr.children.Length == 0) return Error($"wrong number {expr.children.Length} of arguments in {expr.head}");

            int[] ch = new int[expr.children.Length];
            for (int i = 0; i < ch.Length; i++)
            {
                ch[i] = Load(expr.children[i], env, game, db);
                if (error != null) return -1;
                if (nodes[ch[i]].op != OP.APPLY) return Error($"children of markov should be apply rules: {expr.children[i]}");
            }
            return Add(new(OP.MARKOV, -1, ch));
        }
        else if (expr.head == "dir")
        {
            if (expr.children.Length == 0) return Add(new(OP.DIR, -1, [Add(new(OP.SELF, -1, []))]));
            else if (expr.children.Length == 1)
            {
                int child = Load(expr.children[0], env, game, db);
                if (error != null) return -1;
                if (nodes[child].otype != TYPE.OBJECT) return Error($"wrong type of arguments in {expr}");
                return Add(new(OP.DIR, -1, [child]));
            }
            else return Error($"wrong number {expr.children.Length} of children in {expr.head}");
        }
        else if ((expr.head == "left" || expr.head == "right") && expr.children.Length == 0)
        {
            OP op = expr.head == "left" ? OP.LEFT : OP.RIGHT;
            return Add(new(op, -1, [Add(new(OP.DIR, -1, [Add(new(OP.SELF, -1, []))]))]));
        }
        else if (expr.head == "equipped")
        {
            if (expr.children.Length == 0) return Add(new(OP.EQUIPPED, -1, [Add(new(OP.SELF, -1, []))]));
            else if (expr.children.Length == 1)
            {
                int child = Load(expr.children[0], env, game, db);
                if (error != null) return -1;
                if (nodes[child].otype != TYPE.OBJECT) return Error($"wrong type of arguments in {expr}");
                return Add(new(OP.EQUIPPED, -1, [child]));
            }
            else return Error($"wrong number {expr.children.Length} of children in {expr.head}");
        }
        else if (expr.head == "exists")
        {
            int e0 = Load(expr.children[0], env, game, [.. db, TYPE.OBJECT]);
            if (error != null) return -1;
            if (nodes[e0].otype != TYPE.BOOL) return Error($"second argument in exists should be bool (predicate), not {nodes[e0].otype}: {expr}");

            int e1 = Load(expr.children[1], env, game, db);
            if (error != null) return -1;
            if (nodes[e1].otype != TYPE.LIST) return Error($"third argument in exists should be list, not {nodes[e1].otype}: {expr}");

            return Add(new(OP.EXISTS, -1, [e0, e1]));
        }
        else if (expr.head == "closest")
        {
            if (expr.children.Length != 3) return Error($"wrong number {expr.children.Length} of arguments in {expr}");
            if (expr.children[1].Contains("#0", "#1", "#2", "#3", "#4"))
            {
                int e0 = Load(expr.children[0], env, game, db);
                if (error != null) return -1;
                if (nodes[e0].otype != TYPE.OBJECT) return Error($"first argument in closest should be object, not {nodes[e0].otype}: {expr}");

                int e1 = Load(expr.children[1], env, game, [.. db, TYPE.OBJECT]);
                if (error != null) return -1;
                if (nodes[e1].otype != TYPE.BOOL) return Error($"second argument in closest should be bool (predicate), not {nodes[e1].otype}: {expr}");

                int e2 = Load(expr.children[2], env, game, db);
                if (error != null) return -1;
                if (nodes[e2].otype != TYPE.LIST) return Error($"third argument in closest should be list, not {nodes[e2].otype}: {expr}");

                return Add(new(OP.CLOSEST, -1, [e0, e1, e2]));
            }
            else
            {
                int e0 = Load(expr.children[0], env, game, db);
                if (error != null) return -1;
                if (nodes[e0].otype != TYPE.OBJECT) return Error($"first argument in CLOSEST_T should be object, not {nodes[e0].otype}: {expr}");

                int e1 = Load(expr.children[1], env, game, db);
                if (error != null) return -1;
                if (nodes[e1].otype != TYPE.TEMPLATE) return Error($"second argument in CLOSEST_T should be template, not {nodes[e1].otype}: {expr}");

                int e2 = Load(expr.children[2], env, game, db);
                if (error != null) return -1;
                if (nodes[e2].otype != TYPE.LIST) return Error($"third argument in CLOSEST_T should be list, not {nodes[e2].otype}: {expr}");

                return Add(new(OP.CLOSEST_T, -1, [e0, e1, e2]));
            }
        }
        else if (expr.head == "maxDirection" || expr.head == "averageDirection" || expr.head == "maxTile")
        {
            if (expr.children.Length != 1) return Error($"wrong number {expr.children.Length} of arguments in {expr}");

            OP op = expr.head switch
            {
                "maxDirection" => OP.MAX_DIRECTION,
                "averageDirection" => OP.AVERAGE_DIRECTION,
                "maxTile" => OP.MAX_TILE,
                _ => OP.PATH
            };

            int e0 = Load(expr.children[0], env, game, [.. db, TYPE.VECTOR]);
            if (error != null) return -1;
            if (nodes[e0].otype != TYPE.FLOAT) return Error($"the argument in {expr.head} should be float, not {nodes[e0].otype} in {expr}");
            if (!Contains(e0, OP.DBVAR)) return Error($"the argument in {expr.head} should be a float func, not a plain float");

            return Add(new(op, -1, [e0]));
        }
        else if (expr.head == "foreach")
        {
            if (expr.children.Length != 2) return Error($"wrong number {expr.children.Length} of arguments in {expr}");

            int e0 = Load(expr.children[0], env, game, db);
            if (error != null) return -1;
            if (nodes[e0].otype != TYPE.TEMPLATE) return Error($"first argument in foreach should be template, not {nodes[e0].otype}: {ToString(e0, null, game.templates)}");

            int e1 = Load(expr.children[1], env, game, [.. db, TYPE.OBJECT]);
            if (error != null) return -1;
            if (nodes[e1].otype != TYPE.VOID && nodes[e1].otype != TYPE.OBJECT) return Error($"second argument in foreach should be procedure, not {nodes[e1].otype}: {ToString(e1, null, game.templates)}");

            return Add(new(OP.FOREACH, -1, [e0, e1]));
        }
        else if (expr.head[0] == '#')
        {
            if (expr.children.Length > 0) return Error($"de Bruijn variables should not have children {expr}");

            bool success = byte.TryParse(expr.head[1..], out byte opvariant);
            if (!success) return Error($"{expr} is not a proper de Bruijn variable");
            if (opvariant >= db.Length) return Error("too big db-variable, not sure when it could happen");

            return Add(new(OP.DBVAR, opvariant, []) { otype = db[opvariant] });
        }
        else if (templateIndex >= 0)
        {
            if (expr.children.Length == 0) return Add(new(OP.TEMPLATE, (byte)templateIndex, []));
            else if (expr.children.Length == 1) ///(Citizen #0)
            {
                int child = Load(expr.children[0], env, game, db);
                if (error != null) return -1;
                return Add(new(OP.EEQ_T, -1, [Add(new(OP.TEMPLATE, (byte)templateIndex, [])), Add(new(OP.GET_TEMPLATE, -1, [child]))]));
            }
            else return Error($"too many children in a template expression {expr}");
        }
        else if (storageIndex >= 0)
        {
            if (expr.children.Length > 0) return Error($"var expression should not have children: {expr}");
            return Add(new(OP.LOAD, (byte)storageIndex, []) { otype = has });
        }
        else if (!expr.isWord)
        {
            if (expr.head.Length == 1) return Add(new(OP.TILE_VALUE, MJ.dict[expr.head[0]], []));
            strings.Add(expr.head);
            return Add(new(OP.STRING, strings.Count - 1, []));
        }
        else if (float.TryParse(expr.head, out float f))
        {
            if (expr.children.Length > 0) return Error($"constants cannot have arguments {expr}");
            floats.Add(f);
            return Add(new(OP.FLOAT, floats.Count - 1, []));
        }
        else
        {
            int[] ch = new int[expr.children.Length];
            TYPE[] itypes = new TYPE[expr.children.Length];
            for (int i = 0; i < ch.Length; i++)
            {
                int child = Load(expr.children[i], env, game, db);
                if (error != null) return -1;
                ch[i] = child;
                itypes[i] = nodes[child].otype;
            }

            Record record = Record.Find(expr.head, itypes);
            if (record == null)
            {
#if DEBUG
                throw new Exception($"cannot match <{expr}>");
#endif
                return Error($"cannot match <{expr}>");
            }
            return Add(new(record.op, -1, ch));
        }
    }

    public bool ContainsAnyAndFactor(int haystack, int condition)
    {
        Node node = nodes[condition];
        if (node.op == OP.AND) return ContainsAnyAndFactor(haystack, node.ch[0]) || ContainsAnyAndFactor(haystack, node.ch[1]);
        return Contains(haystack, condition);
    }
    public bool Contains(int id, int target)
    {
        if (id == target) return true;
        int[] children = nodes[id].ch;
        for (int i = 0; i < children.Length; i++) if (Contains(children[i], target)) return true;
        return false;
    }
    public bool Contains(int id, OP op)
    {
        Node node = nodes[id];
        if (node.op == op) return true;
        for (int i = 0; i < node.ch.Length; i++) if (Contains(node.ch[i], op)) return true;
        return false;
    }
    public bool Contains(int id, OP op, int variant)
    {
        Node node = nodes[id];
        if (node.op == op && node.opvariant == variant) return true;
        for (int i = 0; i < node.ch.Length; i++) if (Contains(node.ch[i], op, variant)) return true;
        return false;
    }

    public string ToString(int id, string[] storageNames, List<Template> templates)
    {
        Node node = nodes[id];
        // Keep the existing printed form of literals; their operands now index constant arrays.
        int variant = node.op == OP.FLOAT || node.op == OP.STRING ? 0xff : node.opvariant;
        string name = node.op == OP.TEMPLATE && templates != null ? templates[variant].name : storageNames == null ? Record.ToString(node.op, variant) : Record.ToString(node.op, variant, storageNames);
        return node.ch.Length == 0 ? name : $"({name} {string.Join(' ', node.ch.Select(c => ToString(c, storageNames, templates)))})";
    }
    public string ToString(int id) => ToString(id, null, null);
    public string ToString(string[] storageNames, List<Template> templates) => ToString(nodes.Count - 1, storageNames, templates);
    public override string ToString() => ToString(nodes.Count - 1, null, null);
    public int Length() => nodes[^1].length;

    static bool Same(Expression a, Node x, Expression b, Node y)
    {
        if (x.op != y.op || x.ch.Length != y.ch.Length) return false;
        if (x.op == OP.FLOAT)
        {
            if (a.floats[x.opvariant] != b.floats[y.opvariant]) return false;
        }
        else if (x.op != OP.STRING && x.opvariant != y.opvariant) return false;
        for (int i = 0; i < x.ch.Length; i++) if (!Same(a, a.nodes[x.ch[i]], b, b.nodes[y.ch[i]])) return false;
        return true;
    }
    public bool IsContained(Node node, Expression[] patterns)
    {
        for (int i = 0; i < patterns.Length; i++) if (Same(patterns[i], patterns[i].nodes[^1], this, node)) return true;
        return false;
    }

    public static string ScoreString(float[] scores)
    {
        string result = "[";
        for (int i = 0; i < scores.Length; i++)
        {
            result += scores[i].ToString("0.##");
            if (i % 10 == 0 && i > 0) result += "|" + i.ToString();
            if (i < scores.Length - 1) result += ", ";
        }
        return result + "]";
    }

}

abstract class Error
{
    public string error;
}
