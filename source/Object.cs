using System.Numerics;

class Object
{
    public Template template;
    public Body body;
    public Vector3 velocity, color;
    public Vector2 dir;
    public string name;
    public float speed, hp;
    public int id;

    public Value[] storage;
    public int equippedUID, holderUID;
    public bool dynamic;
    public int stunCounter;

    public Object(Template template, State state, int id, Vector3 position, Vector2 dir, string name)
    {
        this.template = template;
        body = new(position, template.size, template.shape);
        color = template.color;
        this.dir = dir;
        this.name = name;
        dynamic = true;
        speed = template.speed;
        hp = template.maxhp;
        this.id = id;

        storage = new Value[template.env.data.Count];
        equippedUID = -1;
        holderUID = -1;

        for (int t = 0; t < template.lets.Count; t++)
        {
            (_, Script script, int memoryIndex, _, _) = template.lets[t];
            Value value = script.Execute(state, id, storage, []);
            storage[memoryIndex] = value;
        }
    }

    public Object Copy()
    {
        Object copy = (Object)MemberwiseClone();
        copy.storage = (Value[])storage.Clone();
        return copy;
    }

    public void Forget(int id)
    {
        for (int i = 0; i < storage.Length; i++) if (storage[i].type == TYPE.OBJECT && storage[i].i == id) storage[i].i = -1;
        if (equippedUID == id) equippedUID = -1;
        if (holderUID == id) holderUID = -1;
    }

    public void UpdateEquippedItemPosition(State state)
    {
        Object equipped = state.array[equippedUID];
        equipped.body.position = body.position + new Vector3(0f, 0f, 0.5f * (body.size.Z + equipped.body.size.Z) - 0.1f); ///-0.1 is needed to prevent a new collision
        equipped.velocity = Vector3.Zero;
    }

    public bool OnGround(float supportZ = 0f) => Math.Abs(velocity.Z) < 0.05f && Math.Abs(body.MinZ() - supportZ) < 0.05f;

    public override string ToString() => name;
    public static string MyString(Object o) => o == null ? "none" : o.name;

    //эти 2 функции должны вызывать одну общую приватную функцию, просто с разным радиусом. Can be body functions if I add speed to the body
    public bool CanReach(Vector2 goal)
    {
        float R = Settings.SPEED / Settings.ROTSPEED; ///делить на template.speed не нужно, если скорость поворота тоже пропорциональна базовой скорости
        float RSmall = R - body.size.AverageRadius2D();
        if (RSmall <= 0f) return true;

        Vector2 v = goal - body.position.XY();
        Vector2 ort = R * dir.Left();
        return ((v + ort).LengthSquared() >= RSmall * RSmall) && ((v - ort).LengthSquared() >= RSmall * RSmall);
    }
    public bool CanReach(Object goal)
    {
        float R = Settings.SPEED / Settings.ROTSPEED; ///делить на template.speed не нужно, если скорость поворота тоже пропорциональна базовой скорости
        float RSmall = R - (body.size.AverageRadius2D() + goal.body.size.AverageRadius2D());
        if (RSmall <= 0f) return true;

        Vector2 v = goal.body.position.XY() - body.position.XY();
        Vector2 ort = R * dir.Left();
        return ((v + ort).LengthSquared() >= RSmall * RSmall) && ((v - ort).LengthSquared() >= RSmall * RSmall);
    }
}
