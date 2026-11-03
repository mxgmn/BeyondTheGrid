partial class Expression : Error
{
    public bool IsGoodIf(int[] ch)
    {
        if (nodes[ch[0]].op == OP.NOT || nodes[ch[0]].op == OP.FALSE || nodes[ch[0]].op == OP.TRUE) return false;
        if (ch[1] == ch[2]) return false;
        if (ContainsAnyAndFactor(ch[1], ch[0]) || ContainsAnyAndFactor(ch[2], ch[0])) return false;
        if (nodes[ch[1]].op == OP.IF_U) ///I think these 2 are already covered by the previous one?
        {
            if (nodes[ch[1]].ch[1] == ch[2] || nodes[ch[1]].ch[2] == ch[2]) return false;
            if (nodes[ch[1]].ch[0] == ch[0]) return false;
        }
        if (nodes[ch[2]].op == OP.IF_U)
        {
            if (nodes[ch[2]].ch[1] == ch[1] || nodes[ch[2]].ch[2] == ch[1]) return false;
            if (nodes[ch[2]].ch[0] == ch[0]) return false;
        }
        if (nodes[ch[1]].op == OP.TURN && nodes[ch[2]].op == OP.TURN) return false;
        if (nodes[ch[0]].op == OP.EEQ_O && nodes[nodes[ch[0]].ch[0]].op == OP.NONE && nodes[ch[1]].op == OP.GOTO && nodes[ch[0]].ch[1] == nodes[ch[1]].ch[0]) return false;
        if (nodes[ch[0]].op == OP.EEQ_O && nodes[nodes[ch[0]].ch[0]].op == OP.NONE && Contains(ch[1], nodes[ch[0]].ch[1])) return false; ///(if (== none X) X ...)
        if (nodes[ch[1]].op == OP.IF_U && nodes[ch[2]].op == OP.IF_U && nodes[ch[1]].ch[0] == nodes[ch[2]].ch[0])
        {
            if (nodes[ch[1]].ch[1] == nodes[ch[2]].ch[1]) return false; ///(if .. (if b A ..) (if b A ..))
            if (ch[0] <= nodes[ch[1]].ch[0]) return false; ///(if b (if c .. ..) (if c .. ..)) where b<=c
        }
        return true;
    }

    public bool IsGood(Node node, Expression[] badPatterns, int maxBadPatternLength)
    {
        OP op = node.op;
        int[] ch = node.ch;
        if (node.length <= maxBadPatternLength && IsContained(node, badPatterns)) return false;
        if (node.otype == TYPE.TUPLE && node.dbtype != TYPE.VOID) return false; ///dbtype это типа булев параметр - встречается или нет #0 в выражении
        if (Record.dict[op].etype != TYPE.VOID && ch.Length > 0 && nodes[ch[0]].dbtype == TYPE.VOID) return false; ///а вот это я не понимаю, что. Типа, функциональный аргемент не может быть первым?

        if (op == OP.IF_U)
        {
            if (!IsGoodIf(ch)) return false;
        }
        else if (op == OP.GO)
        {
            if (nodes[ch[0]].op == OP.TO) return false;
        }
        else if (op == OP.GOTO)
        {
            if (nodes[ch[0]].op == OP.SELF || nodes[ch[0]].op == OP.NONE || nodes[ch[0]].op == OP.EQUIPPED || nodes[ch[0]].op == OP.TO) return false;
            if (nodes[ch[0]].op == OP.FROM || nodes[ch[0]].op == OP.DIR) return false;
            if (nodes[ch[0]].op == OP.LEFT || nodes[ch[0]].op == OP.RIGHT) return false; ///questionable, but ok
        }
        else if (op == OP.TURN)
        {
            if (nodes[ch[0]].op == OP.DIR && nodes[nodes[ch[0]].ch[0]].op == OP.SELF) return true;
            if ((nodes[ch[0]].op == OP.LEFT || nodes[ch[0]].op == OP.RIGHT) && nodes[nodes[ch[0]].ch[0]].op == OP.DIR && nodes[nodes[nodes[ch[0]].ch[0]].ch[0]].op == OP.SELF) return true;
            return false;

            //if (nodes[ch[0]].op == OP.DIR && nodes[nodes[ch[0]].ch[0]].op != OP.SELF) return false;
            //if (nodes[ch[0]].op == OP.MINUS_VV) return false;
            //if (nodes[ch[0]].op == OP.RANDOM_DIRECTION || nodes[ch[0]].op == OP.AVERAGE_DIRECTION) return false;
            //if ((nodes[ch[0]].op == OP.LEFT || nodes[ch[0]].op == OP.RIGHT) && nodes[nodes[ch[0]].ch[0]].op == OP.TO) return false;
            //if (nodes[ch[0]].op == OP.MAX_DIRECTION) return false;
        }
        else if (op == OP.NOT)
        {
            if (nodes[ch[0]].op == OP.NOT || nodes[ch[0]].op == OP.EEQ_O || nodes[ch[0]].op == OP.FALSE || nodes[ch[0]].op == OP.TRUE || nodes[ch[0]].op == OP.LESS /*|| ch[0].id == NEQ*/) return false;
        }
        else if (op == OP.EEQ_O)
        {
            if (ch[0] >= ch[1]) return false; //now that I think of it, it's not going to be easy! Потому что равенства опов недостаточно

            if (nodes[ch[0]].op == OP.SELF || nodes[ch[1]].op == OP.SELF) return false;
            if (nodes[ch[0]].op == OP.CLOSEST && nodes[ch[1]].op == OP.CLOSEST) return false;
            //if (nodes[ch[0]].op == OP.BEAMCAST && nodes[ch[1]].op == OP.BEAMCAST) return false;
            //if ((nodes[ch[0]].op == OP.BEAMCAST && nodes[ch[1]].op == OP.NONE) || (nodes[ch[1]].op == OP.BEAMCAST && nodes[ch[0]].op == OP.NONE)) return false;
        }
        else if (op == OP.LESS)
        {
            if (ch[0] >= ch[1]) return false;
            if (nodes[ch[0]].op == OP.X2_F && nodes[ch[1]].op == OP.X2_F) return false;
            if ((nodes[ch[0]].op == OP.X2_F && nodes[ch[1]].op == OP.ALIGNMENT_VV) || (nodes[ch[1]].op == OP.ALIGNMENT_VV && nodes[ch[0]].op == OP.X2_F)) return false;
            if ((nodes[ch[0]].op == OP.DISTANCE_OO && nodes[ch[1]].op == OP.ALIGNMENT_VV) || (nodes[ch[1]].op == OP.ALIGNMENT_VV && nodes[ch[0]].op == OP.DISTANCE_OO)) return false;
            if ((nodes[ch[0]].op == OP.CONSTANT && nodes[ch[1]].op == OP.X2_F && nodes[nodes[ch[1]].ch[0]].op == OP.DISTANCE_OO) || (nodes[ch[1]].op == OP.CONSTANT && nodes[ch[0]].op == OP.X2_F && nodes[nodes[ch[0]].ch[0]].op == OP.DISTANCE_OO)) return false; ///и в этом месте я понял, что по-хорошему мне нужен алгоритм работы с этими паттернами!
            if (nodes[ch[0]].op == OP.SQUARE || nodes[ch[1]].op == OP.SQUARE) return false; ///if it can be a good policy, this pattern feels wrong
            if (nodes[ch[0]].op == OP.GAP || nodes[ch[1]].op == OP.GAP) return false;
            if (nodes[ch[1]].op == OP.X2_F && ch[0] == nodes[ch[1]].ch[0]) return false; ///(< a (x2 a))
            if (nodes[ch[0]].op == OP.X2_F && ch[1] == nodes[ch[0]].ch[0]) return false; ///(< (x2 a) a)
        }
        else if (op == OP.ALIGNED)
        {
            if (ch[0] >= ch[1]) return false;
            if (nodes[ch[0]].op == OP.LEFT || nodes[ch[0]].op == OP.RIGHT || nodes[ch[1]].op == OP.LEFT || nodes[ch[1]].op == OP.RIGHT) return false;
            if (nodes[ch[0]].op == OP.MINUS_VV || nodes[ch[1]].op == OP.MINUS_VV) return false;
        }
        else if (op == OP.NEAR)
        {
            if (ch[0] >= ch[1]) return false;
            if (nodes[ch[0]].op == OP.NONE || nodes[ch[1]].op == OP.NONE) return false;
            if (nodes[ch[0]].op == OP.EQUIPPED || nodes[ch[1]].op == OP.EQUIPPED) return false;
            //if (nodes[ch[0]].op == OP.BEAMCAST || nodes[ch[1]].op == OP.BEAMCAST) return false;
        }
        else if (op == OP.SEES)
        {
            if (nodes[ch[0]].op == OP.NONE || nodes[ch[1]].op == OP.NONE) return false;
            if (ch[0] == ch[1]) return false;
            //if (ch[0].id == BEAMCAST || ch[1].id == BEAMCAST) return false; //temporary
        }
        else if (op == OP.MULT_FF)
        {
            if (ch[0] >= ch[1]) return false;
            if (!(Contains(ch[0], OP.DBVAR) && Contains(ch[1], OP.DBVAR))) return false; ///widening
            if (nodes[ch[1]].op == OP.SQUARE && ch[0] == nodes[ch[1]].ch[0]) return false; ///(* x (square x))
            if (nodes[ch[0]].op == OP.SQUARE && ch[1] == nodes[ch[0]].ch[0]) return false; ///(* (square x) x)
            if (nodes[ch[1]].op == OP.X2_F && ch[0] == nodes[ch[1]].ch[0]) return false; ///(* x (double x))
            if (nodes[ch[0]].op == OP.X2_F && ch[1] == nodes[ch[0]].ch[0]) return false; ///(* (double x) x)
            if (nodes[ch[1]].op == OP.MULT_FF && ch[0] >= nodes[ch[1]].ch[0]) return false; ///associativity
            if (nodes[ch[0]].op == OP.MULT_FF) /*nodes[ch[0]].ch[1] >= ch[1]*/ return false; ///associativity

            if (nodes[ch[0]].op == OP.X2_F || nodes[ch[1]].op == OP.X2_F) return false; ///strong but just
            if (nodes[ch[0]].op == OP.CONSTANT || nodes[ch[1]].op == OP.CONSTANT) return false;
        }
        else if (op == OP.SQUARE)
        {
            if (nodes[ch[0]].op != OP.ALIGNMENT_VV) return false; ///very rigid
            if (nodes[ch[0]].op == OP.X2_F) return false; ///taste
        }
        else if (op == OP.DISTANCE_OO)
        {
            if (ch[0] >= ch[1]) return false;
            if (nodes[ch[0]].op == OP.NONE || nodes[ch[1]].op == OP.NONE) return false;
            if (nodes[ch[0]].op == OP.DISTANCE_OO || nodes[ch[1]].op == OP.DISTANCE_OO) return false;
        }
        else if (op == OP.ALIGNMENT_VV)
        {
            if (ch[0] >= ch[1]) return false;
            if (nodes[ch[0]].op != OP.DBVAR && nodes[ch[1]].op != OP.DBVAR) return false;
            if (Contains(ch[0], ch[1]) || Contains(ch[1], ch[0])) return false;
        }
        else if (op == OP.RAYDIST)
        {
            if (Contains(ch[0], OP.MAX_DIRECTION)) return false; ///ok
            if (nodes[ch[0]].op == OP.MINUS_VV) return false; ///widening
        }
        else if (op == OP.RUNDIST)
        {
            if (Contains(ch[0], OP.MAX_DIRECTION)) return false;
            if (nodes[ch[0]].op == OP.MINUS_VV) return false; ///widening
        }
        else if (op == OP.GAP)
        {
            if (nodes[ch[0]].dbtype == TYPE.VOID) return false;
            if (Contains(ch[0], OP.MAX_DIRECTION)) return false; ///ok
            if (nodes[ch[0]].op == OP.MINUS_VV) return false; ///widening
        }
        else if (op == OP.X2_F)
        {
            if (nodes[ch[0]].op == OP.X2_F || nodes[ch[0]].op == OP.SQUARE || nodes[ch[0]].op == OP.ALIGNMENT_VV || nodes[ch[0]].op == OP.GAP) return false;
        }
        else if (op == OP.LEFT || op == OP.RIGHT)
        {
            if (nodes[ch[0]].op == OP.RANDOM_DIRECTION) return false;
            if (nodes[ch[0]].op == OP.LEFT || nodes[ch[0]].op == OP.RIGHT || /*ch[0].id == OPP ||*/ nodes[ch[0]].op == OP.FROM) return false;
            if (nodes[ch[0]].op == OP.MINUS_VV)
            {
                if (op == OP.RIGHT) return false;
                if (nodes[nodes[ch[0]].ch[0]].op == OP.LEFT || nodes[nodes[ch[0]].ch[0]].op == OP.RIGHT) return false;
            }
            if (nodes[ch[0]].op == OP.AVERAGE_DIRECTION) return false;
            if (nodes[ch[0]].op == OP.DBVAR) return false;
        }
        else if (op == OP.DIR)
        {
            if (nodes[ch[0]].op == OP.NONE || nodes[ch[0]].op == OP.EQUIPPED) return false;
            //if (nodes[ch[0]].op == OP.BEAMCAST) return false;
        }
        else if (op == OP.TO || op == OP.GOTO || op == OP.FROM)
        {
            if (nodes[ch[0]].op == OP.SELF || nodes[ch[0]].op == OP.NONE || nodes[ch[0]].op == OP.EQUIPPED) return false;
            //if (nodes[ch[0]].op == OP.BEAMCAST) return false;
        }
        else if (op == OP.MAX_DIRECTION)
        {
            if (!(Contains(ch[0], OP.RAYDIST) || Contains(ch[0], OP.GAP) || Contains(ch[0], OP.RUNDIST))) return false; ///ok
            if (nodes[ch[0]].op == OP.ALIGNMENT_VV || nodes[ch[0]].op == OP.SQUARE || nodes[ch[0]].op == OP.X2_F) return false; ///solid
            if (Contains(ch[0], OP.MAX_DIRECTION)) return false; ///ok
        }
        //else if (op == OP.BEAMCAST) { if (nodes[ch[0]].op == OP.TO) return false; }
        else if (op == OP.MINUS_VV)
        {
            if (ch[0] == ch[1]) return false;
            if (nodes[ch[0]].op == OP.PLUS_VV || nodes[ch[0]].op == OP.MINUS_VV || nodes[ch[1]].op == OP.PLUS_VV || nodes[ch[1]].op == OP.MINUS_VV) return false;
            if (nodes[ch[0]].op == OP.FROM) return false;
            if ((nodes[ch[1]].op == OP.LEFT || nodes[ch[1]].op == OP.RIGHT) && ch[0] == nodes[ch[1]].ch[0]) return false; ///pretty strange transformation (y+x,y-x)
            if ((nodes[ch[0]].op == OP.LEFT || nodes[ch[0]].op == OP.RIGHT) && ch[1] == nodes[ch[0]].ch[0]) return false; ///pretty strange transformation (y+x,y-x)
            //if ((nodes[ch[0]].op == OP.LEFT || nodes[ch[0]].op == OP.RIGHT) && (nodes[ch[1]].op == OP.RIGHT || nodes[ch[1]].op == OP.LEFT) && nodes[ch[0]].ch[0] == nodes[ch[1]].ch[0]) return false;
            if (nodes[ch[1]].op == OP.LEFT || nodes[ch[1]].op == OP.RIGHT) return false; ///fuck this
            if (nodes[ch[0]].op == OP.RANDOM_DIRECTION || nodes[ch[1]].op == OP.RANDOM_DIRECTION) return false;
            if (nodes[ch[0]].op == OP.RIGHT && nodes[ch[1]].op == OP.RIGHT) return false; ///left(A-B)=right(B-A)

            if (nodes[ch[0]].op == OP.TO && nodes[ch[1]].op == OP.FROM && nodes[ch[0]].ch[0] == nodes[ch[1]].ch[0]) return false; ///good
            if (nodes[ch[0]].op == OP.FROM && nodes[ch[1]].op == OP.TO && nodes[ch[0]].ch[0] == nodes[ch[1]].ch[0]) return false; ///good

            if ((nodes[ch[0]].op == OP.LEFT || nodes[ch[0]].op == OP.RIGHT) && nodes[nodes[ch[0]].ch[0]].op == OP.MINUS_VV) return false; ///ok
        }
        return true;
    }

    public bool IsGoodPolicy(int id, TYPE type, bool hasOnUse, int minLength)
    {
        if (nodes[id].length < minLength) return false;
        if (type == TYPE.TUPLE)
        {
            if (!Contains(id, OP.GO) && !Contains(id, OP.GOTO)) return false;
            if (hasOnUse && !Contains(id, OP.USE)) return false;

            //if (!ContainsOutside(IsGotoTarget, c => c.Contains("goalAhead") || c.Contains("friendAhead"), _ => false)) return false;
            //if (ContainsOutside(e => e.node.op == OP.USE, _ => false, c => c.Contains("empty"))) return false; ///если есть USE, который вне non-empty, то это нехорошо
            //if (!Contains(e => e.node.op == OP.IF_U && e.Contains(ch[0], "goalAhead") && e.Contains(ch[1], OP.USE))) return false; ///policy should always contain a USE under goalAhead
        }
        return true;
    }
}
