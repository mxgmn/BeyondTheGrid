using System.Numerics;

struct KeyboardState
{
    public bool UP, DOWN, LEFT, RIGHT, USE, JUMP;

    public KeyboardState(bool UP, bool DOWN, bool LEFT, bool RIGHT, bool USE, bool JUMP)
    {
        this.UP = UP;
        this.DOWN = DOWN;
        this.LEFT = LEFT;
        this.RIGHT = RIGHT;
        this.USE = USE;
        this.JUMP = JUMP;
    }

    public static KeyboardState None = new(false, false, false, false, false, false);
}

class State
{
    public Object[] array;
    public int maxObjectCount;
    public List<int> toDestroy;

    List<Object> collidingObjects = [];
    public List<(float t, Vector2 n, int idx)> collisionHits = []; ///просто хранилище, нужное для столкновений - чтобы не ассигнировать память каждый раз

    public MyStack stack, inputs;
    public CollisionState collisionState;
    public float score;
    public LCG lcg;
    public Game game;

    public int currentFrame;

    public int VMX, VMY, VMZ;
    public float voxelSize;
    public byte[] voxelData;
    public int[] field, directions; ///z=0 is buried dirt, z=1 is the grass surface, and world z=0 sits on top of the grass
    
    public const int BURIED_TERRAIN_LAYER = 0;
    public const int SURFACE_TERRAIN_LAYER = 1;
    public const int SOLID_VOXEL_LAYER = 2;
    public const int VOXEL_LAYER_COUNT = 3; ///it's all controlled from here

    public State(Game game, uint seed)
    {
        this.game = game;
        toDestroy = [];
        array = new Object[128];
        collisionState = new(array.Length);
        stack = new(16);
        inputs = new(16);
        lcg = new LCG(seed);

        CreateVoxels();
        game.init.Execute(this, -1, null, []);
    }

    public State Copy()
    {
        State copy = (State)MemberwiseClone();
        copy.array = (Object[])array.Clone();
        for (int i = 0; i < maxObjectCount; i++) if (array[i] != null) copy.array[i] = array[i].Copy();
        copy.toDestroy = new(toDestroy);
        copy.collisionState = collisionState.Copy();
        // These scratch lists are rebuilt before every movement/query.
        copy.collidingObjects = [];
        copy.collisionHits = [];
        copy.stack = stack.Copy();
        copy.inputs = inputs.Copy();
        copy.lcg = new(lcg);
        copy.voxelData = (byte[])voxelData.Clone();
        copy.field = (int[])field.Clone();
        copy.directions = (int[])directions.Clone();
        return copy;
    }

    public void CreateVoxels()
    {
        VMX = (int)game.size.X;
        VMY = (int)game.size.Y;
        VMZ = VOXEL_LAYER_COUNT;
        voxelData = new byte[VMX * VMY * VMZ];
        field = new int[VMX * VMY];
        directions = new int[VMX * VMY];

        int layerArea = VMX * VMY;
        byte earth = MJ.dict['E'];
        byte grass = MJ.dict['G'];
        for (int i = 0; i < layerArea; i++)
        {
            voxelData[i + layerArea * BURIED_TERRAIN_LAYER] = earth;
            voxelData[i + layerArea * SURFACE_TERRAIN_LAYER] = grass;
        }

        voxelSize = game.size.X / VMX;
        if (voxelSize != game.size.Y / VMY) throw new Exception("disproportional voxels");
    }

    public Vector3 RandomPosition(Vector3 osize, LCG lcg)
    {
        Vector3 sceneMin = -0.5f * new Vector3(game.size, 0f);
        bool[] occupied = new bool[VMX * VMY]; ///этот массив может быть единственным для state. Да, мы ничего тут не теряем. Но сначала нужно продумать перехорд к 3D
        int numOccupied = 0;
        for (int n = 0; n < maxObjectCount; n++)
        {
            Object o = array[n];
            if (o == null) continue;

            (int X, int Y) = game.TileFromPoint(o.body.position);
            int I = X + Y * VMX;
            if (!occupied[I])
            {
                occupied[I] = true;
                numOccupied++;                
            }
        }
        for (int y = 0; y < VMY; y++) for (int x = 0; x < VMX; x++)
        {
            int i = x + y * VMX;
            if (voxelData[i + VMX * VMY * SOLID_VOXEL_LAYER] != 0)
            {
                occupied[i] = true;
                numOccupied++;
            }
        }

        int f = -1;
        if (numOccupied == occupied.Length) return Vector3.PositiveInfinity; ///all cells are already occupied
        else if (numOccupied > 0.75f * occupied.Length)
        {
            List<int> candidates = [];
            for (int i = 0; i < occupied.Length; i++) if (!occupied[i]) candidates.Add(i);
            f = candidates.FRandom(lcg);
        }
        else f = occupied.RandomFalse(lcg);
        if (f < 0) return Vector3.PositiveInfinity;
        int fx = f % VMX, fy = f / VMX;

        float centerX = fx + 0.5f + sceneMin.X;
        float centerY = fy + 0.5f + sceneMin.Y;

        float posx = osize.X > 1 ? centerX : lcg.Uniform(centerX - 0.5f + 0.5f * osize.X, centerX + 0.5f - 0.5f * osize.X);
        float posy = osize.Y > 1 ? centerY : lcg.Uniform(centerY - 0.5f + 0.5f * osize.Y, centerY + 0.5f - 0.5f * osize.Y);

        return new Vector3(posx, posy, 0.5f * osize.Z);
    }
    
    static bool OverlapsVoxelXY(Body o, Vector2 center, Vector2 half)
    {
        if (o.shape == SHAPE.CUBOID) return MathF.Abs(o.position.X - center.X) <= 0.5f * o.size.X + half.X && MathF.Abs(o.position.Y - center.Y) <= 0.5f * o.size.Y + half.Y;

        float radius = o.size.AverageRadius2D();
        float closestX = Math.Clamp(o.position.X, center.X - half.X, center.X + half.X);
        float closestY = Math.Clamp(o.position.Y, center.Y - half.Y, center.Y + half.Y);
        float dx = o.position.X - closestX;
        float dy = o.position.Y - closestY;
        return dx * dx + dy * dy <= radius * radius + 1e-6f;
    }

    float SupportHeight(Body o)
    {
        float support = 0f;
        if (!Settings.COLLISION3D) return support;
        if (voxelData == null || VMX <= 0 || VMY <= 0 || VMZ <= 0) return support;
        if (voxelSize <= 0f) return support;

        float feetZ = o.MinZ() + 0.01f;
        Vector2 gridMin = -0.5f * game.size;
        Vector2 cellHalf = new(0.5f * voxelSize, 0.5f * voxelSize);
        Vector2 extra = o.shape == SHAPE.CUBOID ? new(0.5f * o.size.X + cellHalf.X, 0.5f * o.size.Y + cellHalf.Y) : new(o.size.AverageRadius2D() + cellHalf.X, o.size.AverageRadius2D() + cellHalf.Y);
        Vector2 sweepMin = o.position.XY() - extra;
        Vector2 sweepMax = o.position.XY() + extra;

        int minX = Math.Max(0, (int)MathF.Floor((sweepMin.X - gridMin.X) / voxelSize));
        int maxX = Math.Min(VMX - 1, (int)MathF.Floor((sweepMax.X - gridMin.X) / voxelSize));
        int minY = Math.Max(0, (int)MathF.Floor((sweepMin.Y - gridMin.Y) / voxelSize));
        int maxY = Math.Min(VMY - 1, (int)MathF.Floor((sweepMax.Y - gridMin.Y) / voxelSize));

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                Vector2 center = new(gridMin.X + (x + 0.5f) * voxelSize, gridMin.Y + (y + 0.5f) * voxelSize);
                if (!OverlapsVoxelXY(o, center, cellHalf)) continue;

                for (int z = VMZ - 1; z >= 0; z--)
                {
                    int index = x + VMX * (y + VMY * z);
                    if (voxelData[index] == 0) continue;

                    float top = (z - SURFACE_TERRAIN_LAYER) * voxelSize;
                    if (top > feetZ) continue;
                    if (top > support) support = top;
                    break;
                }
            }
        }

        return support;
    }

    void ExecuteGo(Object self, Vector2 dir)
    {
        collidingObjects.Clear();
        for (int i = 0; i < maxObjectCount; i++)
        {
            Object o = array[i];
            if (o != null && !o.template.passable && o != self && i != self.equippedUID) collidingObjects.Add(o); ///it's almost all the objects, the overlap with self doesn't matter
        }
        Collision.Move(collidingObjects, self, self.speed * Settings.SPEED * dir, game.size, this, true);
    }

    public void PutOn(int itemID, int surfaceID)
    {
        if (itemID < 0 || surfaceID < 0) throw new Exception("cannot puton none");
        if (itemID == surfaceID) throw new Exception("cannot put on itself");

        Object item = array[itemID];
        Object surface = array[surfaceID];
        if (surface != null && surface.equippedUID >= 0) return;

        for (int i = 0; i < maxObjectCount; i++)
        {
            Object o = array[i];
            if (o != null && o.equippedUID == itemID) o.equippedUID = -1;
        }

        if (surface != null) surface.equippedUID = itemID;
        item.holderUID = surface == null ? -1 : surfaceID;
    }
    public void Drop(Object self)
    {
        if (self.equippedUID >= 0)
        {
            Object item = array[self.equippedUID];
            self.equippedUID = -1; ///у нас действительно двойные ссылки, и нужно обновлять их обе
            item.holderUID = -1;
        }
    }
    public void Throw(Object self, Vector2 v)
    {
        if (self.equippedUID < 0) return;
        if (v.X == 0f && v.Y == 0f) throw new Exception("cannot throw with zero direction");
        v = Vector2.Normalize(v);

        Object item = array[self.equippedUID];

        float STRENGTH = 0.2f;// * strength;
        item.velocity.X = STRENGTH * v.X;
        item.velocity.Y = STRENGTH * v.Y;
        item.velocity.Z = 0;

        self.equippedUID = -1;
        item.holderUID = -1;
        self.stunCounter = 15;
    }

    ///вообще-то нужно было его удалять из uiddict, а мы этого никогда не делали. Чтобы память отчищалась
    void Destroy(int targetID)
    {
        if (targetID < 0) throw new Exception($"cannot destroy null");
        Object target = array[targetID];
        if (target == null) return; ///if 2 cats touch mouse at the same time

        if (target.template.onDeath != null) target.template.onDeath.Execute(this, target.id, target.storage, []);
        array[targetID] = null;
        collisionState.Forget(targetID, array.Length);

        for (int i = 0; i < maxObjectCount; i++)
        {
            Object o = array[i];
            if (o == null) continue;
            o.Forget(targetID);
        }
    }

    public bool Step(Value keyboardValue, Script agentPolicy) ///returns true if the game is over
    {
        currentFrame++;
        toDestroy.Clear();

        //if (game.everySecond != null && currentFrame % Settings.FPS == 0) game.everySecond.Eval(this, env);
        if (game.onFrame != null) game.onFrame.Execute(this, -1, null, []);

        Template agentTemplate = game.templates[game.agentTemplate];
        for (int i = 0; i < maxObjectCount; i++)
        {
            Object o = array[i];
            if (o == null) continue;
            if (o.template.onFrame != null) o.template.onFrame.Execute(this, o.id, o.storage, []);
            //if (o.template.everySecond != null && currentFrame % Settings.FPS == 0) o.template.everySecond.Eval(this, o.env); //не удалять, полезная штука
            if (o.stunCounter > 0)
            {
                o.stunCounter--;
                continue;
            }

            for (int t = 0; t < o.template.sensors.Count; t++)
            {
                (_, Script sensorScript, int memoryIndex, _, _) = o.template.sensors[t];
                Value sensorValue = sensorScript.Execute(this, o.id, o.storage, []);
                o.storage[memoryIndex] = sensorValue;
            }

            Value value = Value.VOID;
            if (o.template == agentTemplate && keyboardValue.type != TYPE.VOID) value = keyboardValue;
            else if (o.template == agentTemplate && agentPolicy != null) value = agentPolicy.Execute(this, o.id, o.storage, []);
            else if (o.template.policy != null) value = o.template.policy.Execute(this, o.id, o.storage, []);

            if (value.type != TYPE.VOID)
            {
                int bnf = value.i % 3 - 1;
                float rotspeed = Settings.ROTSPEED * o.speed;

                if (value.v.X != 0f || value.v.Y != 0f) o.dir = Settings.HOLONOMIC ? Vector2.Normalize(value.v) : VH.Clamped(value.v, o.dir, rotspeed);
                if (bnf == 1) ExecuteGo(o, o.dir);
                else if (bnf == -1) ExecuteGo(o, -o.dir);
                if (value.i / 3 == 1 && o.template.onUse != null) o.template.onUse.Execute(this, o.id, o.storage, []);
            }
            //if (o.template.onMove != null && Vector3.Distance(o.body.position, oldPosition) > o.speed * Settings.SPEED - 0.0001f) o.template.onMove.Execute(this, o.id, o.storage, []);
        }

        if (game.PHYSICS)
        {
            for (int i = 0; i < maxObjectCount; i++)
            {
                Object o = array[i];
                if (o == null || !o.dynamic) continue;

                float supportZ = SupportHeight(o.body);
                float supportCenterZ = supportZ + 0.5f * o.body.size.Z;

                if (MathF.Abs(o.body.position.Z - supportCenterZ) <= 0.01f)
                {
                    if (o.velocity.Z < 0.0f)
                    {
                        o.velocity.X = 0.5f * o.velocity.X;
                        o.velocity.Y = 0.5f * o.velocity.Y;
                        o.velocity.Z = -0.5f * o.velocity.Z;
                        if (o.velocity.Z < 0.001f)
                        {
                            o.velocity = Vector3.Zero;
                            o.body.position.Z = supportCenterZ;
                        }
                        //CS.WriteLine($"{o} vertical collision, halfing velocity from {-2.0f * o.velocity.Z} to {o.velocity.Z}", ConsoleColor.Magenta);
                    }
                    o.body.position += o.velocity;
                }
                else
                {
                    o.velocity.Z -= 0.005f;
                    o.body.position += o.velocity;

                    Vector3 omin = o.body.Min();
                    Vector3 omax = o.body.Max();
                    Vector2 sceneMin = -0.5f * game.size;
                    Vector2 sceneMax = 0.5f * game.size;

                    if (o.body.position.Z < supportCenterZ)
                    {
                        o.body.position.Z = supportCenterZ;
                    }

                    if (omin.X < sceneMin.X)
                    {
                        o.body.position.X = sceneMin.X + 0.5f * o.body.size.X;
                        o.velocity.X = -0.5f * o.velocity.X;
                    }
                    else if (omax.X > sceneMax.X)
                    {
                        o.body.position.X = sceneMax.X - 0.5f * o.body.size.X;
                        o.velocity.X = -0.5f * o.velocity.X;
                    }
                    if (omin.Y < sceneMin.Y)
                    {
                        o.body.position.Y = sceneMin.Y + 0.5f * o.body.size.Y;
                        o.velocity.Y = -0.5f * o.velocity.Y;
                    }
                    else if (omax.Y > sceneMax.Y)
                    {
                        o.body.position.Y = sceneMax.Y - 0.5f * o.body.size.Y;
                        o.velocity.Y = -0.5f * o.velocity.Y;
                    }
                }
            }
        }

        collisionState.UpdateData(array, maxObjectCount);
        collisionState.ExecuteCollisions(this);

        for (int i = 0; i < maxObjectCount; i++)
        {
            Object o = array[i];
            if (o != null && o.equippedUID >= 0) o.UpdateEquippedItemPosition(this);
        }
        for (int i = 0; i < toDestroy.Count; i++) Destroy(toDestroy[i]);

        return game.agentTemplate >= 0 && Player() == null;
    }

    public Object Player()
    {
        Template agentTemplate = game.templates[game.agentTemplate];
        for (int i = 0; i < maxObjectCount; i++)
        {
            Object o = array[i];
            if (o != null && o.template == agentTemplate) return o;
        }
        return null;
    }
    public int Amount(Template t)
    {
        int result = 0;
        for (int i = 0; i < maxObjectCount; i++)
        {
            Object o = array[i];
            if (o != null && o.template == t) result++;
        }
        return result;
    }
    public int FreeIndex()
    {
        for (int i = 0; i < array.Length; i++) if (array[i] == null) return i;
        return -1;
    }
}
