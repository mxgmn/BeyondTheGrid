class Compiler
{
    List<Codon> opList;
    List<float> floatList;
    List<string> stringList;
    List<(int body, int instructionIndex)> functions;

    Compiler()
    {
        opList = [];
        floatList = [];
        stringList = [];
        functions = [];
    }

    static Script Error(string message)
    {
        Script result = new(null, null, null, TYPE.VOID);
        result.error = message;
        return result;
    }

    public static Script Run(Expression e) => Run(e, e.nodes.Count - 1);

    public static Script Run(Expression e, int root)
    {
        if (e.error != null) return Error(e.error);
        if (e.Contains(root, OP.SUB)) return Error("Cannot compile an unfilled SUB; supply a replacement expression first");
        Compiler compiler = new();
        compiler.Compile(e, root);
        compiler.Emit(OP.RETURN);
        for (int i = 0; i < compiler.functions.Count; i++)
        {
            (int body, int instructionIndex) = compiler.functions[i];
            compiler.opList[instructionIndex] = new Codon(compiler.opList[instructionIndex].op, compiler.opList.Count);
            compiler.Compile(e, body);
            compiler.Emit(OP.RETURN);
        }
        Script result = new(compiler.opList, compiler.floatList, compiler.stringList, e.GetType(root));
        return result;
    }
    public static Script Run(Expr expr, Env<TYPE> env, Game game, TYPE[] inputs)
    {
        Expression e = Expression.Create(expr, env, game, inputs);
        return Run(e);
    }

    private void Compile(Expression e, int root)
    {
        Node node = e.nodes[root];
        OP op = node.op;

        if (op == OP.IF || op == OP.IF_U)
        {
            Compile(e, node.ch[0]); ///compile condition

            int jumpFalseIndex = Emit(OP.JUMP_IF_FALSE_B); ///emit "Jump if False" (target patched below)
            Compile(e, node.ch[1]); ///compile "Then" block
            int jumpEndIndex = Emit(OP.JUMP); ///emit "Jump" (To skip the Else block)

            PatchJump(jumpFalseIndex, opList.Count); ///patch: now we know where the Else block starts
            if (node.ch.Length > 2) Compile(e, node.ch[2]); ///compile "Else" block
            else if (op == OP.IF_U) Emit(OP.PASS);
            PatchJump(jumpEndIndex, opList.Count); ///patch: now we know where the End is
        }
        else if (op == OP.MARKOV) //нужно когда-то написать комменты, как это работает. Каких-то вещей я до сих пор не понимаю: например, можно ли тут сделать автоматическую оптимизацию матчинга
        {
            int startLabel = opList.Count;
            int jumpNextIndex = -1;
            for (int i = 0; i < node.ch.Length; i++)
            {
                if (jumpNextIndex >= 0) PatchJump(jumpNextIndex, opList.Count);

                Compile(e, node.ch[i]);
                jumpNextIndex = Emit(OP.JUMP_IF_FALSE_B);

                Emit(OP.JUMP, startLabel - opList.Count);
            }
            if (jumpNextIndex >= 0) PatchJump(jumpNextIndex, opList.Count);
        }
        else if (op == OP.FOR)
        {
            Compile(e, node.ch[0]); ///compile the loop count, e.g. 40 or a variable n

            int startLabel = opList.Count;
            Emit(OP.DUP);
            int jumpToEndIndex = Emit(OP.JUMP_IF_FALSE_F);

            Compile(e, node.ch[1]); ///could be a void or an object
            if (e.GetType(node.ch[1]) != TYPE.VOID) Emit(OP.POP);

            Emit(OP.DECREMENT);

            Emit(OP.JUMP, startLabel - opList.Count);

            PatchJump(jumpToEndIndex, opList.Count);
            Emit(OP.POP);
        }
        else if (op == OP.SEQ)
        {
            for (int i = 0; i < node.ch.Length; i++)
            {
                Compile(e, node.ch[i]);
                if (i < node.ch.Length - 1 && e.GetType(node.ch[i]) != TYPE.VOID) Emit(OP.POP);
            }
        }
        else if (op == OP.EXISTS)
        {
            Compile(e, node.ch[1]); ///all or allVisible
            functions.Add((node.ch[0], Emit(node.op)));
        }
        else if (op == OP.CLOSEST) ///это будет специальный случай, потому что мы тут не компилируем детей автоматически
        {
            Compile(e, node.ch[0]);
            Compile(e, node.ch[2]); ///all or allVisible
            functions.Add((node.ch[1], Emit(op)));
        }
        else if (op == OP.MAX_DIRECTION || op == OP.AVERAGE_DIRECTION || op == OP.MAX_TILE) functions.Add((node.ch[0], Emit(op)));
        else if (op == OP.FOREACH)
        {
            Compile(e, node.ch[0]);
            functions.Add((node.ch[1], Emit(op)));
        }
        else if (op == OP.FLOAT)
        {
            Emit(OP.FLOAT, floatList.Count);
            floatList.Add(e.floats[node.opvariant]);
        }
        else if (op == OP.STRING)
        {
            Emit(OP.STRING, stringList.Count);
            stringList.Add(e.strings[node.opvariant]);
        }
        else
        {
            if (node.ch != null) for (int i = 0; i < node.ch.Length; i++) Compile(e, node.ch[i]);
            Emit(op, node.opvariant);
        }
    }

    private int Emit(OP op, int opvariant = 0)
    {
        opList.Add(new Codon(op, opvariant));
        return opList.Count - 1;
    }
    private void PatchJump(int instructionIndex, int targetIndex)
    {
        opList[instructionIndex] = new Codon(opList[instructionIndex].op, targetIndex - instructionIndex);
    }
}
