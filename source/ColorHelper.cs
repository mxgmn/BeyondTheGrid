using System.Numerics;

static class CH
{
    public static int IntFromRGB(byte r, byte g, byte b) => (255 << 24) + (r << 16) + (g << 8) + b;

    public static int IntFromString(string hex) => (255 << 24) + Convert.ToInt32(hex, 16);

    public static (byte, byte, byte) BytesFromInt(int argb)
    {
        byte r = (byte)((argb & 0xff0000) >> 16);
        byte g = (byte)((argb & 0xff00) >> 8);
        byte b = (byte)(argb & 0xff);
        return (r, g, b);
    }

    public static (byte, byte, byte, byte) AllBytesFromInt(int i)
    {
        byte r = (byte)((i & 0xff0000) >> 16);
        byte g = (byte)((i & 0xff00) >> 8);
        byte b = (byte)(i & 0xff);
        return (r, g, b, (byte)i);
    }

    public static Vector3 VecColorFromHex(string hex)
    {
        if (hex.Length == 7) hex = hex[1..];
        if (hex.Length != 6) throw new Exception($"color <{hex}> should be 6 chars long");
        byte r = Convert.ToByte(hex[0..2], 16);
        byte g = Convert.ToByte(hex[2..4], 16);
        byte b = Convert.ToByte(hex[4..6], 16);
        return new Vector3(r / 255f, g / 255f, b / 255f);
    }

    public static Vector3 VecColorFromInt(int argb)
    {
        (byte r, byte g, byte b) = BytesFromInt(argb);
        return new Vector3(r / 256.0f, g / 256.0f, b / 256.0f);
    }

    //public const int BLACK = 255 << 24;
    //public const int ALMOSTWHITE = (255 << 24) + (254 << 16) + (254 << 8) + 254;
    //public const int RED = (255 << 24) + (255 << 16);
    //public const int GRAY = (255 << 24) + (194 << 16) + (195 << 8) + 199;

    public const int PICO_B = (255 << 24) + (0 << 16) + (0 << 8) + 0; // #000000
    public const int PICO_I = (255 << 24) + (29 << 16) + (43 << 8) + 83; // #1D2B53
    public const int PICO_P = (255 << 24) + (126 << 16) + (37 << 8) + 83; // #7E2553
    public const int PICO_E = (255 << 24) + (0 << 16) + (135 << 8) + 81; // #008751
    public const int PICO_N = (255 << 24) + (171 << 16) + (82 << 8) + 54; // #AB5236
    public const int PICO_D = (255 << 24) + (95 << 16) + (87 << 8) + 79; // #5F574F
    public const int PICO_A = (255 << 24) + (194 << 16) + (195 << 8) + 199; // #C2C3C7
    public const int PICO_W = (255 << 24) + (255 << 16) + (241 << 8) + 232; // #FFF1E8
    public const int PICO_R = (255 << 24) + (255 << 16) + (0 << 8) + 77; // #FF004D
    public const int PICO_O = (255 << 24) + (255 << 16) + (163 << 8) + 0; // #FFA300
    public const int PICO_Y = (255 << 24) + (255 << 16) + (236 << 8) + 39; // #FFEC27
    public const int PICO_G = (255 << 24) + (0 << 16) + (228 << 8) + 54; // #00E436
    public const int PICO_U = (255 << 24) + (41 << 16) + (173 << 8) + 255; // #29ADFF
    public const int PICO_C = (255 << 24) + (131 << 16) + (118 << 8) + 156; // #83769C
    public const int PICO_K = (255 << 24) + (255 << 16) + (119 << 8) + 168; // #FF77A8
    public const int PICO_F = (255 << 24) + (255 << 16) + (204 << 8) + 170; // #FFCCAA

    public static int[] PICO = [PICO_B, PICO_I, PICO_P, PICO_E, PICO_N, PICO_D, PICO_A, PICO_W, PICO_R, PICO_O, PICO_Y, PICO_G, PICO_U, PICO_C, PICO_K, PICO_F];
    public static int[] PICO_NOBLACK = [PICO_I, PICO_P, PICO_E, PICO_N, PICO_D, PICO_A, PICO_W, PICO_R, PICO_O, PICO_Y, PICO_G, PICO_U, PICO_C, PICO_K, PICO_F];

    public static Dictionary<char, int> picoCodes = new()
    {
        ['B'] = PICO_B,
        ['I'] = PICO_I,
        ['P'] = PICO_P,
        ['E'] = PICO_E,
        ['N'] = PICO_N,
        ['D'] = PICO_D,
        ['A'] = PICO_A,
        ['W'] = PICO_W,
        ['R'] = PICO_R,
        ['O'] = PICO_O,
        ['Y'] = PICO_Y,
        ['G'] = PICO_G,
        ['U'] = PICO_U,
        ['C'] = PICO_C,
        ['K'] = PICO_K,
        ['F'] = PICO_F,
    };

    public static Vector3 PICORotated(int i) => VecColorFromInt(PICO_NOBLACK[(i + 2) % PICO_NOBLACK.Length]);
}
