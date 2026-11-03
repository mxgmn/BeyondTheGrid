class CollisionState
{
    bool[] data;
    List<(int id1, int id2)> toExecute;

    public CollisionState(int maxObjects)
    {
        data = new bool[maxObjects * maxObjects];
        toExecute = [];
    }

    public CollisionState Copy()
    {
        CollisionState copy = (CollisionState)MemberwiseClone();
        copy.data = (bool[])data.Clone();
        copy.toExecute = new(toExecute);
        return copy;
    }

    public void Clear()
    {
        data.AsSpan().Clear();
        toExecute.Clear();
    }

    public void UpdateData(Object[] objects, int maxObjectCount)
    {
        for (int i = 0; i < maxObjectCount; i++)
        {
            Object o = objects[i];
            if (o == null || o.holderUID >= 0 || o.template.onCollision == null) continue;
            for (int j = 0; j < maxObjectCount; j++)
            {
                Object other = objects[j];
                if (other == null || other.holderUID >= 0 || i == j) continue;
                if (j < i && other.template.onCollision != null) continue;

                int i1 = Math.Min(i, j), i2 = Math.Max(i, j);
                int index = i2 + i1 * objects.Length;
                if (Body.Near(objects[i1].body, objects[i2].body))
                {
                    if (!data[index])
                    {
                        data[index] = true;
                        toExecute.Add((i1, i2));
                    }
                }
                else if (data[index]) data[index] = false;
            }
        }
    }

    public void ExecuteCollisions(State state)
    {
        for (int c = 0; c < toExecute.Count; c++)
        {
            (int o1, int o2) = toExecute[c];
            Object O1 = state.array[o1];
            Object O2 = state.array[o2];
            if (O1.template.onCollision != null) O1.template.onCollision.Execute(state, o1, O1.storage, [Value.ObjectValue(o2)]);
            if (O2.template.onCollision != null) O2.template.onCollision.Execute(state, o2, O2.storage, [Value.ObjectValue(o1)]);
        }
        toExecute.Clear();
    }

    public void Forget(int id, int maxObjects)
    {
        for (int i = 0; i < maxObjects; i++)
        {
            data[id * maxObjects + i] = false;
            data[i * maxObjects + id] = false;
        }
    }
}
