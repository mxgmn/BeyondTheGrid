static class Parser
{
    public static (Expr, string error) Parse(Token[] tokens)
    {
        Stack<ListExpr> stack = new();
        for (int index = 0; index < tokens.Length; )
        {
            Token token = tokens[index];
            index++;

            if (token.type == TOKEN.L)
            {
                if (index >= tokens.Length) return (null, "missing head, text ended on '('");
                Token head = tokens[index];
                if (head.type == TOKEN.WORD || head.type == TOKEN.STRING)
                {
                    index++;
                    stack.Push(new ListExpr(head.lexeme, head.type == TOKEN.WORD));
                }
                else return (null, "missing head");
            }
            else if (token.type == TOKEN.R)
            {
                if (stack.Count > 0)
                {
                    Expr expr = stack.Pop().ToExpr();
                    if (stack.Count == 0)
                    {
                        if (index < tokens.Length) return(null, "extra tokens found (probably multiple root-level parens)");
                        return (expr, null);
                    }
                    else stack.Peek().children.Add(expr);
                }
                else return (null, "closing parenthesis without being open");
            }
            else
            {
                Expr leaf = new ListExpr(token.lexeme, token.type == TOKEN.WORD).ToExpr();
                if (stack.Count == 0)
                {
                    if (index < tokens.Length) return(null, "extra tokens found");
                    else return (leaf, null);
                }
                else stack.Peek().children.Add(leaf);
            }
        }
        return (null, "code ended without the closing parenthesis");
    }

    class ListExpr ///если бы в Expr можно было по-одному добавлять детей, то этот класс был бы не нужен
    {
        string head;
        bool isWord;
        public List<Expr> children = [];

        public ListExpr(string head, bool isWord)
        {
            this.head = head;
            this.isWord = isWord;
        }

        public Expr ToExpr() => new(head, children.ToArray()) { isWord = isWord };
    }
}

#if false
//good idea to keep a recursive parser as well
class Parser
{
    int index;
    readonly Token[] tokens; //чтобы не передавать весь текст в каждой рекурсии

    public Parser(Token[] tokens)
    {
        this.tokens = tokens;
        index = 0;
    }

    public Expr Parse()
    {
        Token token = tokens[index];
        index++;

        if (token.type == TOKEN.L)
        {
            Token head = tokens[index];
            if (head.type != TOKEN.WORD && head.type != TOKEN.STRING) throw new Exception("missing head");
            index++;

            List<Expr> children = [];
            while (true)
            {
                if (index >= tokens.Length) throw new Exception("code ended without the closing parenthesis");
                if (tokens[index].type == TOKEN.R) break;
                children.Add(Parse());
            }
            index++;
            return new Expr(head.lexeme, children.ToArray());
        }
        else return new Expr(token.lexeme, []);
    }
}
#endif
