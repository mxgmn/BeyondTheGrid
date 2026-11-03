using System.Numerics;

enum TYPE { VOID, BOOL, FLOAT, OBJECT, TEMPLATE, VECTOR, TUPLE, LIST, TILE, FUNC, STRING, UNKNOWN };

static class TH
{
    public static string[] typeNames = Enum.GetNames<TYPE>();
    public static string Name(this TYPE t) => typeNames[(int)t];
    public static int T = typeNames.Length;

    public static char[] symbols = ['0', 'b', 'f', 'o', 't', 'v', 'u', 'l', 'T', 'F', 's', '!']; //NEXT: думаю таки сделать общий класс BiDict, как-то слишком хаотично пока
    public static Dictionary<char, TYPE> dict;
    static TH()
    {
        if (symbols.Length != Enum.GetNames<TYPE>().Length) throw new Exception($"missing type symbols");
        dict = [];
        for (int i = 0; i < symbols.Length; i++) dict[symbols[i]] = (TYPE)i;
    }

    public static string ObsVecString(this Value[] a)
    {
        string result = "[";
        for (int i = 0; i < a.Length - 1; i++) result += a[i].ToString() + ", ";
        return result + a[^1].ToString() + "]";
    }
}

struct Value
{
    public TYPE type;

    public int i;
    public Vector2 v; ///vectors are actually more common than floats

    Value(TYPE type, int i, Vector2 v)
    {
        this.type = type;
        this.i = i;
        this.v = v;
    }

    public static Value TupleValue(int bnf, bool use, Vector2 v) => new(TYPE.TUPLE, bnf + (use ? 4 : 1), v);
    public static Value ObjectValue(int uid) => new(TYPE.OBJECT, uid, Vector2.Zero);
    public static Value FloatValue(float f) => new(TYPE.FLOAT, -1, new Vector2(f, 0f));
    public static Value TileValue(byte t) => new(TYPE.TILE, t, Vector2.Zero);
    public static Value TemplateValue(int t) => new(TYPE.TEMPLATE, t, Vector2.Zero);
    public static Value VectorValue(Vector2 v) => new(TYPE.VECTOR, -1, v);
    public static Value StringValue(int i) => new(TYPE.STRING, i, Vector2.Zero);

    public static Value ActionValue(KeyboardState keyboard, Object o)
    {
        int bnf = 0;
        if (keyboard.UP) bnf = 1;
        else if (keyboard.DOWN) bnf = -1;

        Vector2 v = o.dir;
        float rotspeed = Settings.ROTSPEED * o.speed;

        if (keyboard.RIGHT) v = v.Rotated(-rotspeed);
        if (keyboard.LEFT) v = v.Rotated(rotspeed);

        return TupleValue(bnf, keyboard.USE, v);        
    }

    public static Value PASS = TupleValue(0, false, Vector2.Zero),
                        NONE = new(TYPE.OBJECT, -1, Vector2.Zero),
                        NOTHING = new(TYPE.TEMPLATE, -1, Vector2.Zero),
                        FALSE = new(TYPE.BOOL, 0, Vector2.Zero),
                        TRUE = new(TYPE.BOOL, 1, Vector2.Zero),
                        ZEROVEC = VectorValue(Vector2.Zero),
                        ALL = new(TYPE.LIST, 0, Vector2.Zero),
                        ALL_VISIBLE = new(TYPE.LIST, 1, Vector2.Zero),
                        VOID = new(TYPE.VOID, -1, Vector2.Zero);

    public static bool Same(Value v1, Value v2) => v1.type == v2.type && v1.i == v2.i && v1.v == v2.v;

    public readonly Object GetObject(State state)
    {
#if DEBUG
        if (type != TYPE.OBJECT) throw new Exception($"{type} is not {TYPE.OBJECT}");
#endif
        return i < 0 ? null : state.array[i];
    }

    public override readonly string ToString()
    {
        if (type == TYPE.TUPLE)
        {
            string result;
            if (i == 0) result = "BACK ";
            else if (i == 1) result = "TURN";
            else if (i == 2) result = "FORWARD";
            else if (i == 3) result = "BACK+USE";
            else if (i == 4) result = "USE";
            else if (i == 5) result = "FORWARD+USE";
            else throw new Exception($"unknown action {i}");
            return result + " " + v.ToString("0.00");
        }
        else if (type == TYPE.BOOL) return i > 0 ? "true" : "false";
        else if (type == TYPE.OBJECT) return $"o{i}";
        else if (type == TYPE.FLOAT) return v.X.ToString("0.##");
        else if (type == TYPE.VOID) return "VOID";
        else if (type == TYPE.VECTOR) return v.ToString("0.00");
        else if (type == TYPE.LIST) return i > 0 ? "allVisible" : "all";
        else if (type == TYPE.TEMPLATE) return i < 0 ? "NOTHING" : $"TEMPLATE-{i}";
        else if (type == TYPE.FUNC) return $"FUNC-{i}";
        else throw new Exception($"unknown type {type}");
    }
}
