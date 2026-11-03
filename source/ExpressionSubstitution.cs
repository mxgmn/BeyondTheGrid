partial class Expression
{
    // Copy only the selected trees, remapping literal pools and bound input indices.
    // Each occurrence of SUB receives the same closed expression.
    public Expression Substitute(Expression sub, int root)
    {
        Expression result = new();
        if (error != null || sub.error != null)
        {
            result.error = error ?? sub.error;
            return result;
        }
        if (sub.Contains(root, OP.SUB))
        {
            result.error = "The replacement expression must not contain SUB";
            return result;
        }
        if (nodes.Any(n => n.op == OP.SUB && n.otype != sub.GetType(root)))
        {
            result.error = "SUB and its replacement must have the same type";
            return result;
        }

        int Copy(Expression source, int id, int inputOffset, bool replace)
        {
            Node node = source.nodes[id];
            if (replace && node.op == OP.SUB) return Copy(sub, root, node.opvariant, false);
            node.ch = node.ch.Select(c => Copy(source, c, inputOffset, replace)).ToArray();
            if (node.op == OP.FLOAT)
            {
                float value = source.floats[node.opvariant];
                node.opvariant = result.floats.Count;
                result.floats.Add(value);
            }
            else if (node.op == OP.STRING)
            {
                string value = source.strings[node.opvariant];
                node.opvariant = result.strings.Count;
                result.strings.Add(value);
            }
            else if (node.op == OP.DBVAR) node.opvariant += inputOffset;
            return result.Add(node);
        }
        Copy(this, nodes.Count - 1, 0, true);
        return result;
    }
}
