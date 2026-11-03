enum OP
{
    IF, FOR, SEQ, ///not an instruction, cannot occur in a script

    DBVAR, DECREMENT, POP, DUP, STORE, JUMP, JUMP_IF_FALSE_B, JUMP_IF_FALSE_F, ///F-версия используется для for
    RETURN,

    FLOAT, STRING, TEMPLATE, LOAD,
    FOREACH, RANDOM, DAMAGE, SPEED, MARKOV, ///special
    PUTON, DROP, DESTROY, THROW, POSITION_FF, POSITION_V, STUN, REWARD, CREATE, VOX, RECT, PAR, SETSIZE, ///return Void
    TURN, GO, BACK, GOTO, USE, IF_U, ///return Tuple
    FALSE, TRUE, COIN, NOT, EEQ_O, EEQ_T, EEQ_F, NEAR, HAS_VELOCITY, AND, OR, LESS, LESSEQ, ALIGNED, SEES, APPLY, CHANGE_TILE_TO, IS_RESOURCE, IS_CREATURE, PROXIMITY_SENSOR, EXISTS, ///return Bool
    
    AMOUNT, DOTPRODUCT, ALIGNMENT_VV, OPP_F, PLUS_FF, MINUS_FF, MULT_FF, DIV_FF, POWER, SQUARE, UNIFORM, AREA, X, Y, MASS, LENGTH, DISTANCE_OO, PROJDIST, RUNDIST, RAYDIST, GAP, CONSTANT, X2_F, X2_V, TILE, ///return Float

    SELF, NONE, EQUIPPED, HOLDER, CLOSEST_T, CLOSEST, BEAMCAST, ///return Object
    VEC, DIR, LEFT, RIGHT, TO, FROM, PLUS_VV, MINUS_VV, MULT_FV, MAX_DIRECTION, MAX_TILE, AVERAGE_DIRECTION, RANDOM_DIRECTION, SCENE_MIN, SCENE_SIZE, MIN, MAX, SIZE, CONE_CLAMP, UNIT, PATH, ///return Vector2
    GET_TEMPLATE, NOTHING, ///return Template
    ALL, ALL_VISIBLE, ///return List
    TILE_VALUE, ///return Tile
};

class Record
{
    public OP op;
    public TYPE otype, etype;
    public TYPE[] itypes;
    public string name;

    Record(OP op, TYPE[] itypes, TYPE otype, string name)
    {
        this.op = op;
        this.name = name;
        etype = TYPE.VOID;
        this.otype = otype;
        this.itypes = itypes;
    }
    Record(OP op, string istr, char ostr, string name)
    {
        this.op = op;
        this.name = name;
        etype = TYPE.VOID;
        otype = TH.dict[ostr];
        itypes = istr.Select(c => TH.dict[c]).ToArray();
    }
    Record(OP op, string istr, char ostr, string name, TYPE etype)
    {
        this.op = op;
        this.name = name;
        this.etype = etype;
        otype = TH.dict[ostr];
        itypes = istr.Select(c => TH.dict[c]).ToArray();
    }

    static bool Same(TYPE[] a1, TYPE[] a2)
    {
        if (a1.Length != a2.Length) return false;
        for (int i = 0; i < a1.Length; i++) if (a1[i] != a2[i]) return false;
        return true;
    }
    public static Record Find(string name, TYPE[] itypes = null)
    {
        if (itypes == null)
        {
            foreach (Record node in library) if (node.name == name) return node;
        }
        else
        {
            foreach (Record node in library) if (node.name == name && Same(node.itypes, itypes)) return node;
        }
        return null;
    }

    public static Dictionary<OP, Record> dict = [];
    static Record() { foreach (Record r in library) dict.TryAdd(r.op, r); } ///overloads share an OP; keep the first, as the old lookup did
    static Record[] library =
    [
        ///return Void
        new Record(OP.PUTON, "oo", '0', "putOn"), ///заметим, что это не является действием. Но в грамматике всё равно такого нет!
        new Record(OP.DAMAGE, "ofo", '0', "damage"),
        new Record(OP.DESTROY, "o", '0', "destroy"),
        new Record(OP.DROP, "", '0', "drop"),
        new Record(OP.THROW, "ov", '0', "throw"),
        new Record(OP.REWARD, "f", '0', "reward"),
        new Record(OP.IF, "b00", '0', "if"),
        new Record(OP.FOR, "f0", '0', "for"),
        new Record(OP.FOR, "fo", '0', "for"), ///если будет слишком много вариантов for, то можно его сделать специальным случаем
        new Record(OP.FOR, "fb", '0', "for"),
        new Record(OP.FOREACH, "t0", '0', "foreach"),
        new Record(OP.MARKOV, [], TYPE.VOID, "markov"),
        new Record(OP.POSITION_FF, "off", '0', "position"),
        new Record(OP.POSITION_V, "ov", '0', "position"),

        new Record(OP.VOX, "ffff", '0', "vox"),
        new Record(OP.RECT, "ffffff", '0', "rect"),
        new Record(OP.PAR, "TT", '0', "par"),
        new Record(OP.SETSIZE, "ff", '0', "setsize"),

        ///return Tuple
        new Record(OP.TURN, "v", 'u', "turn"),
        new Record(OP.GO, "v", 'u', "go"),
        new Record(OP.GOTO, "o", 'u', "goto"),
        new Record(OP.USE, "", 'u', "use"),
        new Record(OP.IF_U, "buu", 'u', "if"),

        ///return Bool
        new Record(OP.FALSE, "", 'b', "false"),
        new Record(OP.TRUE, "", 'b', "true"),
        new Record(OP.COIN, "", 'b', "coin"),
        new Record(OP.NOT, "b", 'b', "not"),
        new Record(OP.AND, "bb", 'b', "and"),
        new Record(OP.OR, "bb", 'b', "or"),
        new Record(OP.EEQ_O, "oo", 'b', "=="),
        new Record(OP.EEQ_T, "tt", 'b', "=="),
        new Record(OP.EEQ_F, "ff", 'b', "=="),
        //new Record(OP.NEQ_O, "oo", 'b', "!="),
        new Record(OP.NEAR, "oo", 'b', "near"),
        new Record(OP.LESS, "ff", 'b', "<"), ///opvariant
        new Record(OP.ALIGNED, "vv", 'b', "aligned"),
        new Record(OP.SEES, "oo", 'b', "sees"),
        new Record(OP.APPLY, "ss", 'b', "apply"),
        new Record(OP.CHANGE_TILE_TO, "T", 'b', "changeTileTo"), ///returns success if the tile has changed
        new Record(OP.TILE, "v", 'b', "tile"),
        new Record(OP.EXISTS, "Fl", 'b', "exists"),

        ///return Float
        new Record(OP.FLOAT, "", 'f', "FLOAT"),
        new Record(OP.AMOUNT, "t", 'f', "amount"),
        new Record(OP.DOTPRODUCT, "vv", 'f', "dotproduct"),
        new Record(OP.ALIGNMENT_VV, "vv", 'f', "alignment"),
        //new Record(OP.ALIGNMENT_VT, "vt", 'f', "alignment"),
        new Record(OP.UNIFORM, "ff", 'f', "uniform"),
        new Record(OP.LENGTH, "v", 'f', "length"),
        new Record(OP.DISTANCE_OO, "oo", 'f', "distance"),
        new Record(OP.PROJDIST, "vt", 'f', "projdist"),
        new Record(OP.RUNDIST, "vt", 'f', "rundist"),
        new Record(OP.RAYDIST, "v", 'f', "raydist"),
        new Record(OP.OPP_F, "f", 'f', "-"),
        new Record(OP.PLUS_FF, "ff", 'f', "+"),
        new Record(OP.MINUS_FF, "ff", 'f', "-"),
        new Record(OP.MULT_FF, "ff", 'f', "*"),
        new Record(OP.DIV_FF, "ff", 'f', "/"),
        new Record(OP.POWER, "ff", 'f', "^"),
        new Record(OP.SQUARE, "f", 'f', "square"),
        new Record(OP.GAP, "v", 'f', "gap"),
        new Record(OP.CONSTANT, "", 'f', "constant"),
        new Record(OP.X2_F, "f", 'f', "x2"),

        ///return Object
        new Record(OP.SELF, "", 'o', "self"),
        new Record(OP.NONE, "", 'o', "none"),
        new Record(OP.CREATE, "t", 'o', "create"),
        new Record(OP.BEAMCAST, "v", 'o', "beamcast"),
        new Record(OP.EQUIPPED, "o", 'o', "equipped"),
        new Record(OP.HOLDER, "o", 'o', "holder"),
        new Record(OP.CLOSEST_T, "otl", 'o', "closest"),
        new Record(OP.CLOSEST, "oFl", 'o', "closest"), ///F means func, l means list (right now there are only 2 possible lists). And this shows the problem: we have only one func type.

        ///return Vector2
        new Record(OP.DIR, "o", 'v', "dir"),
        new Record(OP.LEFT, "v", 'v', "left"),
        new Record(OP.RIGHT, "v", 'v', "right"),
        new Record(OP.X2_V, "v", 'v', "x2"),
        new Record(OP.TO, "o", 'v', "to"),
        new Record(OP.FROM, "o", 'v', "from"),
        new Record(OP.RANDOM_DIRECTION, "", 'v', "randomDirection"),
        new Record(OP.PLUS_VV, "vv", 'v', "+"),
        new Record(OP.MINUS_VV, "vv", 'v', "-"),
        new Record(OP.MULT_FV, "fv", 'v', "*"),
        new Record(OP.CONE_CLAMP, "vvf", 'v', "coneClamp"),
        new Record(OP.UNIT, "f", 'v', "unit"),
        new Record(OP.PATH, "o", 'v', "path"),
        new Record(OP.MAX_DIRECTION, "f", 'v', "maxDirection", TYPE.VECTOR),
        new Record(OP.MAX_TILE, "f", 'v', "maxTile", TYPE.VECTOR),
        new Record(OP.AVERAGE_DIRECTION, "f", 'v', "averageDirection", TYPE.VECTOR),
        
        ///return Template
        new Record(OP.TEMPLATE, "", 't', "TEMPLATE"),
        new Record(OP.GET_TEMPLATE, "o", 't', "getTemplate"),
        new Record(OP.NOTHING, "", 't', "nothing"),

        ///return List
        new Record(OP.ALL, "", 'l', "all"),
        new Record(OP.ALL_VISIBLE, "", 'l', "allVisible"),

        ///return String
        new Record(OP.STRING, "", 's', "STRING"),

        ///return Tile
        new Record(OP.TILE_VALUE, "", 'T', "TILE"), ///ну да, у нас же есть FLOAT

        ///return Unknown
        new Record(OP.LOAD, "", '!', "LOAD"),
        new Record(OP.DBVAR, "", '!', "DBVAR"),
    ];

    public static string ToString(OP op, int opvariant)
    {
        if (op == OP.LOAD) return "LOAD-" + opvariant;
        else if (op == OP.DBVAR) return "#" + opvariant;
        else if (op == OP.SEQ) return "SEQ";
        else
        {
            Record record = dict[op];
            return opvariant < 0 ? record.name : $"{record.name}-{opvariant}";
        }
    }
    public static string ToString(OP op, int opvariant, string[] storageNames)
    {
        if (op == OP.LOAD) return storageNames[opvariant];
        else if (op == OP.DBVAR) return "#" + opvariant;
        else if (op == OP.SEQ) return "SEQ";
        else
        {
            Record record = dict[op];
            return opvariant < 0 ? record.name : $"{record.name}-{opvariant}";
        }
    }

    public static (Codon, TYPE optype)[] Grammar(Template agentTemplate, List<Template> templates)
    {
        if (agentTemplate == null) throw new Exception("agentTemplate should not be null");

        OP[] basics = [
            OP.GO,
            OP.GOTO, //для больших игр типа футбола это не нужно. Для маленьких можно добавить в функции. Нужно понять, как запрещать открытые паттерны

            OP.EEQ_O,
            OP.LESS,

            OP.MULT_FF,
            OP.SQUARE,
            OP.DISTANCE_OO,
            OP.ALIGNMENT_VV,
            OP.CONSTANT,
            OP.X2_F,

            OP.SELF,
            OP.NONE,

            OP.DIR,
            OP.LEFT,
            OP.RIGHT,
            OP.TO,
            OP.FROM,
            OP.MINUS_VV,
            OP.MAX_DIRECTION,

            OP.FALSE,
            OP.TRUE,
            OP.IF_U,
        ];

        List<(Codon, TYPE)> result = [(new Codon(OP.DBVAR, 0), TYPE.VECTOR)];
        if (agentTemplate.onUse != null) result.Add((new(OP.USE), TYPE.TUPLE));
        foreach (OP op in basics) result.Add((new(op), dict[op].otype));
        foreach (OP op in agentTemplate.functions) result.Add((new(op), dict[op].otype));

        foreach ((string sensorName, Script sensorScript, int memoryIndex, _, _) in agentTemplate.sensors) ///для сенсоров мы немного задолбавемся делать LOAD_VEC, LOAD_OBJ
        {
            result.Add((new Codon(OP.LOAD, memoryIndex), sensorScript.type));
        }

        for (byte i = 0; i < templates.Count; i++)
        {
            string tname = templates[i].name;
            if (tname != agentTemplate.name) result.Add((new Codon(OP.TEMPLATE, i), TYPE.TEMPLATE));
        }
        return result.ToArray();
    }
}
