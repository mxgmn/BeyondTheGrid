class Store
{
    (Codon, TYPE)[] constants, nonConstants;

    public Expression expressions = new();
    public List<int>[][] bags; ///node indices grouped by type, then length - 1

    TYPE targetType;
    int maxlength;

    Expression[] badPatterns;
    int maxPatternLength;

    public Store((Codon codon, TYPE otype)[] grammar, TYPE targetType, int maxlength, Expression[] badPatterns, List<Template> templates)
    {
        this.targetType = targetType;
        this.maxlength = maxlength >= 0 ? maxlength : 50;

        bags = new List<int>[TH.T][];
        for (int t = 0; t < TH.T; t++)
        {
            bags[t] = new List<int>[this.maxlength];
            for (int l = 0; l < this.maxlength; l++) bags[t][l] = [];
        }

        constants = grammar.Where(p => Record.dict[p.codon.op].itypes.Length == 0).ToArray();
        nonConstants = grammar.Where(p => Record.dict[p.codon.op].itypes.Length > 0).ToArray();

        this.badPatterns = badPatterns;
        maxPatternLength = badPatterns.Length == 0 ? 0 : badPatterns.Max(e => e.Length());

        if (Settings.PRINT_EXPRESSIONS) Console.WriteLine("--- expressions of length 1 ---");
        foreach ((Codon codon, TYPE otype) in constants)
        {
            Node node = new(codon.op, codon.opvariant, []) { otype = otype };
            if (codon.op == OP.DBVAR) node.dbtype = TYPE.VECTOR; ///в общем случае это будет не так, но пока что все db-аргументы являются векторами
            Add(node, templates);
        }
    }

    void Add(Node node, List<Template> templates)
    {
        if (!expressions.IsGood(node, badPatterns, maxPatternLength)) return;
        int id = expressions.nodes.Count;
        expressions.nodes.Add(node);
        List<int> bag = bags[(int)node.otype][node.length - 1];
        bag.Add(id);
        if (Settings.PRINT_EXPRESSIONS && node.length <= Settings.MAX_PRINTED_EXPRESSION_LENGTH && (expressions.Contains(id, Settings.DEBUG_OP) || Settings.DEBUG_OP == OP.SEQ) && (node.otype == TYPE.TUPLE || !Settings.PRINT_ONLY_POLICIES))
        {
            var color = node.dbtype == TYPE.VOID ? ConsoleColor.Gray : ConsoleColor.DarkMagenta;
            CS.Write($"{TH.symbols[(int)node.otype]}-{node.length}-{bag.Count - 1} ", color);
            CS.WriteLine(expressions.ToString(id, null, templates), ConsoleColor.Yellow);
        }
    }

    public void EnumerateExpressions(List<Template> templates)
    {
        for (int length = 2; length <= maxlength; length++)
        {
            if (Settings.PRINT_EXPRESSIONS) Console.WriteLine($"\n--- expressions of length {length} ---");
            int numBefore = expressions.nodes.Count;
            for (int i = 0; i < nonConstants.Length; i++)
            {
                (Codon codon, TYPE otype) = nonConstants[i];
                //Record node = Record.Find(codon.op);
                //if (length == maxLength - 1 && node.otype != (int)targetType) continue; //странно, почему это приводит к багам?
                if (otype == TYPE.TUPLE && targetType == TYPE.BOOL) continue; ///потому что действия никогда не будут конвертированы в були
                EnumerateExpressions(codon.op, length, templates);
            }
            if (Settings.PRINT_EXPRESSIONS && length > Settings.MAX_PRINTED_EXPRESSION_LENGTH) CS.WriteLine($"{expressions.nodes.Count - numBefore} expressions", ConsoleColor.Yellow);
        }
    }

    bool IsAnyBagEmpty(TYPE[] itypes, int[] partition)
    {
        for (int i = 0; i < itypes.Length; i++) if (bags[(int)itypes[i]][partition[i] - 1].Count == 0) return true;
        return false;
    }

    void EnumerateExpressions(OP op, int length, List<Template> templates)
    {
        Record record = Record.dict[op];
        List<int[]> partitions = Helper.Partitions(length - 1, record.itypes.Length);
        for (int p = 0; p < partitions.Count; p++)
        {
            int[] partition = partitions[p];
            if (IsAnyBagEmpty(record.itypes, partition)) continue;
            int[] tuple = new int[record.itypes.Length];
            while (true)
            {
                int[] children = new int[tuple.Length];

                bool hasDB = false;
                for (int i = 0; i < tuple.Length; i++)
                {
                    children[i] = bags[(int)record.itypes[i]][partition[i] - 1][tuple[i]];
                    if (expressions.nodes[children[i]].dbtype != TYPE.VOID) hasDB = true;
                }
                Node node = new(op, -1, children) { length = length };
                ///сначала нужно заассайнить node правильный dbtype, а потом уже думать, добавлять или нет

                if (record.etype == TYPE.VOID && hasDB) node.dbtype = TYPE.VECTOR; ///тоже в общем случае неверно

                Add(node, templates);

                int index = tuple.Length - 1;
                while (index >= 0 && tuple[index] == bags[(int)record.itypes[index]][partition[index] - 1].Count - 1) index--;
                if (index < 0) break;

                tuple[index]++;
                for (int i = index + 1; i < tuple.Length; i++) tuple[i] = 0; ///reset all elements to the right
            }
        }
    }
}
