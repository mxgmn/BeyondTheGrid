static class CS
{
    static ConsoleColor[] debugColors = null;
    //static ConsoleColor[] debugColors = [ConsoleColor.Red, ConsoleColor.DarkRed, ConsoleColor.Magenta, ConsoleColor.DarkMagenta, ConsoleColor.Cyan, ConsoleColor.DarkCyan];
    //static ConsoleColor[] debugColors = [ConsoleColor.Red, ConsoleColor.Yellow];

    public static void Write(string s, ConsoleColor color)
    {
        if (debugColors == null || debugColors.Contains(color))
        {
            Console.ForegroundColor = color;
            Console.Write(s);
            Console.ResetColor();
        }
    }
    public static void WriteLine(string s, ConsoleColor color)
    {
        if (debugColors == null || debugColors.Contains(color))
        {
            Console.ForegroundColor = color;
            Console.WriteLine(s);
            Console.ResetColor();
        }
    }
}
