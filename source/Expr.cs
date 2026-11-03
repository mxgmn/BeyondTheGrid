class Expr
{
    public string head;
    public bool isWord; ///означает, что это слово языка, а не строка в кавычках
    public Expr[] children;

    public Expr(string head, Expr[] children)
    {
        this.head = head;
        isWord = true;
        this.children = children;
    }

    public static (Expr expr, string error) Load(string text) => Parser.Parse(Lexer.Run(text));

    public Expr Tail() => new(children[0].head, children[1..]);

    public string TallString(int indent)
    {
        string result = new string(' ', 2 * indent) + head + Environment.NewLine;
        foreach (Expr child in children) result += child.TallString(indent + 1);
        return result;
    }
    public override string ToString() => children.Length == 0 ? (isWord ? head : $"\"{head}\"") : $"({head} {string.Join(' ', children.Select(c => c.ToString()))})";
    public string TailString() => string.Join(' ', children.Select(c => c.ToString()));
    public Expr GameCode()
    {
        if (head == "emoji" || head == "color") return null;

        else if (children.Length == 0) return this;
        else
        {
            List<Expr> result = [];
            for (int i = 0; i < children.Length; i++)
            {
                Expr c = children[i].GameCode();
                if (c != null) result.Add(c);
            }
            return new Expr(head, result.ToArray());
        }
    }

    public int Length()
    {
        int sum = 1;
        for (int i = 0; i < children.Length; i++) sum += children[i].Length();
        return sum;
    }

    public bool IsFlatList()
    {
        for (int i = 0; i < children.Length; i++) if (children[i].children.Length > 0) return false;
        return true;
    }

    public Expr Child(string head)
    {
        for (int c = 0; c < children.Length; c++)
        {
            Expr child = children[c];
            if (child.head == head) return child;
        }
        return null;
    }
    public Expr[] Children(params string[] heads)
    {
        List<Expr> result = [];
        for (int c = 0; c < children.Length; c++)
        {
            Expr child = children[c];
            if (heads.Contains(child.head)) result.Add(child);
        }
        return result.ToArray();
    }

    public List<Expr> Descendants(params string[] heads)
    {
        List<Expr> result = [];
        for (int c = 0; c < children.Length; c++)
        {
            Expr child = children[c];
            if (heads.Contains(child.head)) result.Add(child);
            List<Expr> childList = child.Descendants(heads);
            for (int i = 0; i < childList.Count; i++) result.Add(childList[i]);
        }
        return result;
    }

    public bool Contains(string h)
    {
        if (head == h) return true; ///return this? Проблема в том, что это будет не родитель
        for (int i = 0; i < children.Length; i++) if (children[i].Contains(h)) return true;
        return false;
    }
    public bool Contains(params string[] hs)
    {
        if (hs.Contains(head)) return true;
        for (int i = 0; i < children.Length; i++) if (children[i].Contains(hs)) return true;
        return false;
    }
    public bool ContainsString()
    {
        if (!isWord) return true;
        for (int i = 0; i < children.Length; i++) if (children[i].ContainsString()) return true;
        return false;
    }

    public Expr GetExpr(string name)
    {
        Expr child = Child(name);
        return child == null ? null : child.children[0];
    }
    public string GetString(string name, string dflt)
    {
        Expr child = Child(name);
        return child == null ? dflt : child.children[0].head;
    }
    public int GetInt(string name, int dflt)
    {
        Expr child = Child(name);
        return child == null ? dflt : int.Parse(child.children[0].head);
    }
}
