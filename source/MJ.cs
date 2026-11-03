class Rule
{
    public int MX, MY, MZ;
    public byte[] input, output; ///тут можно либо ввести маски, либо специальную опцию для звёздочек

    Rule(byte[] input, byte[] output, int MX, int MY, int MZ)
    {
        if (input.Length != output.Length || input.Length != MX * MY * MZ) throw new Exception("length mismatch");
        
        this.input = input;
        this.output = output;
        this.MX = MX;
        this.MY = MY;
        this.MZ = MZ;
    }

    public static Rule Load(string istr, string ostr)
    {
        string[] isplit = istr.Split(' ');
        string[] osplit = ostr.Split(' ');

        if (isplit.Length != osplit.Length) throw new Exception("mismatched rule size");
        string[] irows = isplit[0].Split('/');
        string[] orows = osplit[0].Split('/');
        if (irows.Length != orows.Length || irows[0].Length != orows[0].Length) throw new Exception("mismatched rule size");

        int MX = irows[0].Length;
        int MY = irows.Length;
        int MZ = isplit.Length;
        for (int z = 0; z < MZ; z++)
        {
            irows = isplit[z].Split('/');
            orows = osplit[z].Split('/');
            if (irows.Length != MY || orows.Length != MY) throw new Exception("mismatched rule size");
            for (int y = 0; y < MY; y++) if (irows[y].Length != MX || orows[y].Length != MX) throw new Exception("mismatched rule size");
        }

        return new(MJ.ToArray(istr), MJ.ToArray(ostr), MX, MY, MZ);
    }

    public Rule ZRotated()
    {
        byte[] newinput = new byte[input.Length];
        byte[] newoutput = new byte[output.Length];
        for (int dz = 0; dz < MZ; dz++)
        {
            int DZ = dz * MX * MY;
            for (int dy = 0; dy < MX; dy++) for (int dx = 0; dx < MY; dx++)
            {
                newinput[dx + dy * MY + DZ] = input[MX - 1 - dy + dx * MX + DZ];
                newoutput[dx + dy * MY + DZ] = output[MX - 1 - dy + dx * MX + DZ];
            }
        }
        return new Rule(newinput, newoutput, MY, MX, MZ);
    }

    public Rule Reflected()
    {
        byte[] newinput = new byte[input.Length];
        byte[] newoutput = new byte[output.Length];
        for (int dz = 0; dz < MZ; dz++)
        {
            int DZ = dz * MX * MY;
            for (int dy = 0; dy < MY; dy++) for (int dx = 0; dx < MX; dx++)
            {
                newinput[dx + dy * MX + DZ] = input[MX - 1 - dx + dy * MX + DZ];
                newoutput[dx + dy * MX + DZ] = output[MX - 1 - dx + dy * MX + DZ];
            }
        }
        return new Rule(newinput, newoutput, MX, MY, MZ);
    }

    public static bool Same(Rule r1, Rule r2)
    {
        if (r1.MX != r2.MX || r1.MY != r2.MY || r1.MZ != r2.MZ) return false;
        for (int i = 0; i < r1.input.Length; i++) if (r1.input[i] != r2.input[i] || r1.output[i] != r2.output[i]) return false;        
        return true;
    }
}

static class MJ
{
    static bool Matches(Rule rule, int x, int y, int z, byte[] data, int MX, int MY)
    {
        for (int dz = 0; dz < rule.MZ; dz++) for (int dy = 0; dy < rule.MY; dy++) for (int dx = 0; dx < rule.MX; dx++)
            if (data[x + dx + (y + dy) * MX + (z + dz) * MX * MY] != rule.input[dx + dy * rule.MX + dz * rule.MX * rule.MY]) return false;
        return true;
    }
    public static void Apply(Rule rule, int x, int y, int z, byte[] data, int MX, int MY)
    {
        for (int dz = 0; dz < rule.MZ; dz++) for (int dy = 0; dy < rule.MY; dy++) for (int dx = 0; dx < rule.MX; dx++)
            data[x + dx + (y + dy) * MX + (z + dz) * MX * MY] = rule.output[dx + dy * rule.MX + dz * rule.MX * rule.MY];
    }

    public static (Rule, int, int, int)[] Matches(Rule originalRule, byte[] data, int MX, int MY, int MZ)
    {
        //TODO: по хорошему все симметрии должны быть созданы при инициализации, а не на каждом матчинге. Тут просто нужно продумать, как это согласовано с типами данных нашего языка
        Rule[] rules = SymmetryHelper.SquareSymmetries(originalRule, r => r.ZRotated(), r => r.Reflected(), Rule.Same);
        List<(Rule, int, int, int)> result = [];
        for (int r = 0; r < rules.Length; r++)
        {
            Rule rule = rules[r];
            for (int z = State.SURFACE_TERRAIN_LAYER; z < MZ + 1 - rule.MZ; z++) for (int y = 0; y < MY + 1 - rule.MY; y++) for (int x = 0; x < MX + 1 - rule.MX; x++)
                if (Matches(rule, x, y, z, data, MX, MY)) result.Add((rule, x, y, z));
        }
        return result.ToArray();
    }

    public static byte[] ToArray(string str)
    {
        List<byte> result = new(str.Length / 2);
        for (int i = 0; i < str.Length; i++)
        {
            char c = str[i];
            if (c != ' ' && c != '/') result.Add(dict[c]);
        }
        return result.ToArray();
    }

    public static Dictionary<char, byte> dict = new()
    {
        ['A'] = 0,
        ['E'] = 1,
        ['G'] = 2,
        ['W'] = 3,
        ['S'] = 4,
    };
}

static class SymmetryHelper
{
    public static T[] SquareSymmetries<T>(T thing, Func<T, T> rotation, Func<T, T> reflection, Func<T, T, bool> same)
    {
        T[] things = new T[8];

        things[0] = thing;                  // e
        things[1] = reflection(things[0]);  // b
        things[2] = rotation(things[0]);    // a
        things[3] = reflection(things[2]);  // ba
        things[4] = rotation(things[2]);    // a2
        things[5] = reflection(things[4]);  // ba2
        things[6] = rotation(things[4]);    // a3
        things[7] = reflection(things[6]);  // ba3

        List<T> result = new();
        for (int i = 0; i < 8; i++)
        {
            bool found = false;
            for (int j = 0; j < result.Count; j++) if (same(result[j], things[i]))
                {
                    found = true;
                    break;
                }
            if (!found) result.Add(things[i]);
        }
        return result.ToArray();
    }
}
