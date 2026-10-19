class Env<T>
{
    public List<(string name, T value)> data;

    public Env()
    {
        data = [];
    }

    public (int storageIndex, T type) Has(string name)
    {
        for (int i = 0; i < data.Count; i++) if (data[i].name == name) return (i, data[i].value);
        return (-1, default(T));
    }

    public int Define(string name, T value)
    {
        if (Has(name).storageIndex >= 0) return -1;
        data.Add((name, value));
        return data.Count - 1;
    }

    public int Set(string name, T value)
    {
        int index = Has(name).storageIndex;
        if (index < 0) return -1;
        data[index] = (name, value);
        return index;
    }
}
