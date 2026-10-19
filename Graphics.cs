using System.Numerics;

struct Ray2
{
    public Vector2 origin, vector;

    public Ray2(Vector2 origin, Vector2 vector)
    {
        this.origin = origin;
        this.vector = vector;
    }
}

static class Graphics
{
    const float MAXT = 1000000f;

    static float SceneBoundHit2D(Ray2 ray, float bodyRadius, Vector2 min, Vector2 max)
    {
        Vector2 rVec = new(bodyRadius, bodyRadius);
        min += rVec;
        max -= rVec;

        if (ray.origin.X < min.X || ray.origin.X > max.X || ray.origin.Y < min.Y || ray.origin.Y > max.Y) return 0f;

        Vector2 tMinForSlabs = (min - ray.origin) / ray.vector;
        Vector2 tMaxForSlabs = (max - ray.origin) / ray.vector;

        Vector2 t1 = Vector2.Min(tMinForSlabs, tMaxForSlabs);
        Vector2 t2 = Vector2.Max(tMinForSlabs, tMaxForSlabs);

        float tNear = MathF.Max(t1.X, t1.Y);
        float tFar = MathF.Min(t2.X, t2.Y);

        if (tNear > tFar || tFar < 0f) return MAXT;
        return (tNear > 0f) ? tNear : tFar;
    }

    static float SweepCircle(Ray2 ray, float bodyRadius, SHAPE oShape, Vector2 oCenter, Vector2 oHalfSize) ///careful, this hits the self object as well
    {
        if (oShape == SHAPE.CUBOID)
        {
            Vector2 min = oCenter - oHalfSize - new Vector2(bodyRadius, bodyRadius);
            Vector2 max = oCenter + oHalfSize + new Vector2(bodyRadius, bodyRadius);

            float tx1, tx2, ty1, ty2;
            if (MathF.Abs(ray.vector.X) < 0.0001f)
            {
                if (ray.origin.X < min.X || ray.origin.X > max.X) return -1f;
                tx1 = float.NegativeInfinity;
                tx2 = float.PositiveInfinity;
            }
            else
            {
                tx1 = (min.X - ray.origin.X) / ray.vector.X;
                tx2 = (max.X - ray.origin.X) / ray.vector.X;
                if (tx1 > tx2) (tx1, tx2) = (tx2, tx1);
            }

            if (MathF.Abs(ray.vector.Y) < 0.0001f)
            {
                if (ray.origin.Y < min.Y || ray.origin.Y > max.Y) return -1f;
                ty1 = float.NegativeInfinity;
                ty2 = float.PositiveInfinity;
            }
            else
            {
                ty1 = (min.Y - ray.origin.Y) / ray.vector.Y;
                ty2 = (max.Y - ray.origin.Y) / ray.vector.Y;
                if (ty1 > ty2) (ty1, ty2) = (ty2, ty1);
            }

            float tEnter = MathF.Max(tx1, ty1);
            float tExit = MathF.Min(tx2, ty2);

            return tEnter > tExit || tExit <= 0.1f ? -1f : MathF.Max(tEnter, 0f); ///это очень чувствительно к константе, к сожалению
        }
        else
        {
            Vector2 m = ray.origin - oCenter;
            float r = bodyRadius + 0.5f * (oHalfSize.X + oHalfSize.Y);

            float b = 2f * Vector2.Dot(ray.vector, m);
            float c = Vector2.Dot(m, m) - r * r;

            if (c <= 0f) return b > 0f ? -1f : 0f;

            float disc = b * b - 4f * c;
            if (disc < 0f) return -1f;

            float root = MathF.Sqrt(disc);
            float t = (-b - root) / 2f;

            return t < 0f ? -1f : t;
        }
    }

    static float VoxelLayerHit2D(Ray2 ray, float bodyRadius, State state, float tLimit)
    {
        if (state == null || state.voxelData == null || state.VMX <= 0 || state.VMY <= 0 || state.VMZ <= State.SOLID_VOXEL_LAYER || state.voxelSize <= 0f || !(tLimit > 0f) || !float.IsFinite(tLimit)) return MAXT;

        float cellSize = state.voxelSize;
        Vector2 gridMin = -0.5f * state.game.size;
        Vector2 cellHalf = new(0.5f * cellSize);
        int x = (int)Math.Floor(((double)ray.origin.X - gridMin.X) / cellSize);
        int y = (int)Math.Floor(((double)ray.origin.Y - gridMin.Y) / cellSize);
        // Float-rounded box edges can touch an extra cell when the radius is almost a whole cell.
        float extent = MathF.Max(state.game.size.X, state.game.size.Y);
        float roundoff = 4f * (MathF.BitIncrement(extent) - extent);
        int padding = (int)MathF.Floor((bodyRadius + roundoff) / cellSize) + 1;
        int stepX = ray.vector.X < 0f ? -1 : 1, stepY = ray.vector.Y < 0f ? -1 : 1;
        double dx = MathF.Abs(ray.vector.X) < 0.0001f ? 0.0 : ray.vector.X;
        double dy = MathF.Abs(ray.vector.Y) < 0.0001f ? 0.0 : ray.vector.Y;
        double deltaX = dx == 0.0 ? double.PositiveInfinity : cellSize / Math.Abs(dx);
        double deltaY = dy == 0.0 ? double.PositiveInfinity : cellSize / Math.Abs(dy);
        double nextX = dx == 0.0 ? double.PositiveInfinity : (gridMin.X + (x + (stepX > 0 ? 1.0 : 0.0)) * cellSize - ray.origin.X) / dx;
        double nextY = dy == 0.0 ? double.PositiveInfinity : (gridMin.Y + (y + (stepY > 0 ? 1.0 : 0.0)) * cellSize - ray.origin.Y) / dy;
        int minX = x - padding, maxX = x + padding, minY = y - padding, maxY = y + padding;
        int layer = state.VMX * state.VMY * State.SOLID_VOXEL_LAYER;
        float nearest = MAXT;
        while (true)
        {
            // Start with the body's neighborhood; each DDA step adds only a new row or column.
            for (int cy = Math.Max(0, minY); cy <= Math.Min(state.VMY - 1, maxY); cy++)
                for (int cx = Math.Max(0, minX); cx <= Math.Min(state.VMX - 1, maxX); cx++)
                {
                    if (state.voxelData[layer + cy * state.VMX + cx] == 0) continue;
                    Vector2 center = new(gridMin.X + (cx + 0.5f) * cellSize, gridMin.Y + (cy + 0.5f) * cellSize);
                    float t = SweepCircle(ray, bodyRadius, SHAPE.CUBOID, center, cellHalf);
                    if (t >= 0f && t < nearest) nearest = t;
                }

            double next = Math.Min(nextX, nextY);
            if (next >= nearest || next > tLimit) return nearest;
            if (nextX < nextY)
            {
                x += stepX;
                nextX += deltaX;
                minX = maxX = x + stepX * padding;
                minY = y - padding;
                maxY = y + padding;
            }
            else
            {
                y += stepY;
                nextY += deltaY;
                minY = maxY = y + stepY * padding;
                minX = x - padding;
                maxX = x + padding;
            }
        }
    }

    public static int BeamObjectHit(Object self, Vector2 v, State state)
    {
        Ray2 ray = new(self.body.position.XY(), v);
        float bodyRadius = self.body.size.AverageRadius2D();

        int minIndex = -1;
        float tmin = SceneBoundHit2D(ray, bodyRadius, -0.5f * state.game.size, 0.5f * state.game.size);
        for (int i = 0; i < state.maxObjectCount; i++)
        {
            Object o = state.array[i];
            if (o == null || o == self || i == self.equippedUID) continue;
            float t = SweepCircle(ray, bodyRadius, o.template.shape, o.body.position.XY(), 0.5f * o.body.size.XY());
            if (t >= 0f && t < tmin)
            {
                tmin = t;
                minIndex = i;
            }
        }
        return minIndex;
    }

    //NEXT: последние 2 аргумента не нужны, я думаю. Удалить их, когда мы проапдейтим бенчмарк. Чтобы можно было быстренько запустить бенчмарк на всех играх сразу. blocksVision тоже сейчас не нужно
    public static (Object o, float t) BodyHit2D(Ray2 ray, float bodyRadius, State state, Object target, Template targetTemplate)
    {
        System.Diagnostics.Debug.Assert(MathF.Abs(ray.vector.LengthSquared() - 1f) < 0.001f, "vector must be normalized");

        int minIndex = -1;
        float tmin = SceneBoundHit2D(ray, bodyRadius, -0.5f * state.game.size, 0.5f * state.game.size);
        float voxelT = VoxelLayerHit2D(ray, bodyRadius, state, tmin);
        if (voxelT < tmin) tmin = voxelT;

        for (int i = 0; i < state.maxObjectCount; i++)
        {
            Object o = state.array[i];
            if (o != null && (o.template.blocksVision || o.template == targetTemplate || o == target))
            {
                float t = SweepCircle(ray, bodyRadius, o.template.shape, o.body.position.XY(), 0.5f * o.body.size.XY());
                if (t >= 0f && t < tmin)
                {
                    tmin = t;
                    minIndex = i;
                }
            }
        }
        return (minIndex >= 0 ? state.array[minIndex] : null, tmin);
    }
    public static (Object o, float t) BodyHit2D(Object self, Vector2 v, State state, Object target, Template targetTemplate) => 
        BodyHit2D(new Ray2(self.body.position.XY(), v), self.body.size.AverageRadius2D(), state, target, targetTemplate);

    static (Object o, float t) BodyHit2DAll(Object self, Vector2 v, State state, float bodyRadius)
    {
        System.Diagnostics.Debug.Assert(MathF.Abs(v.LengthSquared() - 1f) < 0.001f, "vector must be normalized");

        Ray2 ray = new(self.body.position.XY(), v);
        int minIndex = -1;
        float tmin = SceneBoundHit2D(ray, bodyRadius, -0.5f * state.game.size, 0.5f * state.game.size);
        float voxelT = VoxelLayerHit2D(ray, bodyRadius, state, tmin);
        if (voxelT < tmin) tmin = voxelT;

        for (int i = 0; i < state.maxObjectCount; i++)
        {
            Object o = state.array[i];
            if (o == null || o == self) continue;

            float t = SweepCircle(ray, bodyRadius, o.template.shape, o.body.position.XY(), 0.5f * o.body.size.XY());
            if (t >= 0f && t < tmin)
            {
                tmin = t;
                minIndex = i;
            }
        }
        return (minIndex >= 0 ? state.array[minIndex] : null, tmin);
    }

    public static (Object o, float t) BodyHit2DAll(Object self, Vector2 v, State state) => BodyHit2DAll(self, v, state, self.body.size.AverageRadius2D());
    public static (Object o, float t) RayHit2DAll(Object self, Vector2 v, State state) => BodyHit2DAll(self, v, state, 0f);

    public static bool Sees(Object self, Object target, State state) //хм, для конечной цели это можно оставить
    {
        if (self == target) return true;
        if (Body.Near(self.body, target.body)) return true;
        if (VH.FlatDistanceSquared(self.body.position, target.body.position) <= self.template.hearingRad * self.template.hearingRad) return true;

        Vector2 v = Vector2.Normalize(target.body.position.XY() - self.body.position.XY());
        float cos = Vector2.Dot(self.dir, v);
        if (cos < self.template.visionCos) return false;

        return BodyHit2D(self, v, state, target, null).o == target;
    }
    public static bool LOS(Object self, Vector2 p, State state)
    {
        Vector2 selfPos = self.body.position.XY();
        Vector2 v = Vector2.Normalize(p - selfPos);
        float t = BodyHit2DAll(self, v, state).t;
        return Vector2.DistanceSquared(selfPos, p) <= t * t;
    }
}
