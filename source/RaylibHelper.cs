using System.Numerics;
using Raylib_cs;

static class RaylibHelper
{
    static string EmojiPath(string emojiSymbol) => $"resources/emoji/{string.Join("-", emojiSymbol.EnumerateRunes().Select(r => r.Value.ToString("x")))}.png";

    static string ResolveEmojiPath(string emojiSymbol)
    {
        string exactPath = EmojiPath(emojiSymbol);
        if (File.Exists(exactPath)) return exactPath;

        string normalized = emojiSymbol.Replace("\uFE0F", "").Replace("\uFE0E", "");
        string normalizedPath = EmojiPath(normalized);
        if (normalized != emojiSymbol && File.Exists(normalizedPath)) return normalizedPath;

        throw new Exception($"ERROR: emoji <{exactPath}> does not exist");
    }

    public static Texture2D EmojiAtlas(List<string> emojiSymbols)
    {
        const int TX = 72, TY = 72;
        if (emojiSymbols.Count == 0)
        {
            Image empty = Raylib.GenImageColor(1, 1, Color.Blank);
            Texture2D emptyTexture = Raylib.LoadTextureFromImage(empty);
            Raylib.SetTextureFilter(emptyTexture, TextureFilter.Point);
            Raylib.UnloadImage(empty);
            return emptyTexture;
        }

        Image atlas = Raylib.GenImageColor(emojiSymbols.Count * TX, TY, Color.Blank);
        for (int i = 0; i < emojiSymbols.Count; i++)
        {
            string emojiPath = ResolveEmojiPath(emojiSymbols[i]);
            Image image = Raylib.LoadImage(emojiPath);
            if (image.Width == 0) throw new Exception($"ERROR: emoji <{emojiPath}> does not exist");

            Raylib.ImageFlipHorizontal(ref image);
            Raylib.ImageDraw(ref atlas, image, new Rectangle(0, 0, TX, TY), new Rectangle(i * TX, 0, TX, TY), Color.White);
            Raylib.UnloadImage(image);
        }
        Texture2D result = Raylib.LoadTextureFromImage(atlas);
        Raylib.SetTextureFilter(result, TextureFilter.Point);
        Raylib.UnloadImage(atlas);
        return result;
    }

    public static void SetInt(this Shader shader, string varname, int i)
    {
        int location = Raylib.GetShaderLocation(shader, varname);
        Raylib.SetShaderValue(shader, location, i, ShaderUniformDataType.Int);
    }
    public static void SetVec2(this Shader shader, string varname, Vector2 v)
    {
        int location = Raylib.GetShaderLocation(shader, varname);
        Raylib.SetShaderValue(shader, location, v, ShaderUniformDataType.Vec2);
    }
    public static void SetVec3(this Shader shader, string varname, Vector3 v)
    {
        int location = Raylib.GetShaderLocation(shader, varname);
        Raylib.SetShaderValue(shader, location, v, ShaderUniformDataType.Vec3);
    }

    public static void PrintCenter(string s, float y, Font font, Color color, int WIDTH)
    {
        float swidth = Raylib.MeasureTextEx(font, s, font.BaseSize, 0).X;
        Vector2 p = new((WIDTH - swidth) / 2, y);
        Raylib.DrawTextEx(font, s, p, font.BaseSize, 0, color);
    }
}
