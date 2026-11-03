enum TOKEN { L, R, WORD, STRING }

class Token
{
    public TOKEN type;
    public string lexeme;
    public int line;

    public Token(TOKEN type, string lexeme, int line)
    {
        this.type = type;
        this.lexeme = lexeme[0] == '"' && lexeme[^1] == '"' ? lexeme[1..^1] : lexeme;
        this.line = line;
    }
}

class Lexer
{
    readonly string source;
    readonly List<Token> tokens;
    int start, current, line;

    Lexer(string source)
    {
        this.source = source;
        tokens = [];
    }
    public static Token[] Run(string source)
    {
        Lexer lexer = new(source);
        while (lexer.current < source.Length)
        {
            lexer.start = lexer.current;
            lexer.ScanToken();
        }
        return lexer.tokens.ToArray();
    }

    void ScanToken()
    {
        char c = Advance();

        if (c == '(') AddToken(TOKEN.L);
        else if (c == ')') AddToken(TOKEN.R);
        else if (c == ';')
        {
            while (Peek() != '\n' && current < source.Length) Advance();
        }
        else if (c == ' ' || c == '\r' || c == '\t') { }
        else if (c == '\n') line++;
        else if (c == '"') ScanString();
        else if (IsWordChar(c)) ScanWord();
        else throw new Exception($"LEXER ERROR: unexpected character '{c}', source = {source}");
    }

    char Advance() ///возвращает чар и сдвигает на единичку. При этом start указатель остается в начале читаемого слова
    {
        char result = source[current];
        current++;
        return result;
    }
    char Peek() => current < source.Length ? source[current] : '\0';

    void ScanString()
    {
        while (Peek() != '"' && current < source.Length)
        {
            if (Peek() == '\n') line++;
            Advance();
        }
        if (current >= source.Length) throw new Exception("unterminated string");
        Advance(); //the closing "
        AddToken(TOKEN.STRING);
    }
    void ScanWord()
    {
        while (IsWordChar(Peek())) Advance();
        AddToken(TOKEN.WORD);
    }

    static bool IsWordChar(char c) => c != '\0' && !char.IsWhiteSpace(c) && c != '(' && c != ')' && c != ';' && c != '"';

    void AddToken(TOKEN type)
    {
        string lexeme = source[start..current];
        //if (type == TOKEN.STRING) CS.WriteLine($"lexed a string {lexeme}", ConsoleColor.Red);
        tokens.Add(new Token(type, lexeme, line));
    }
}
