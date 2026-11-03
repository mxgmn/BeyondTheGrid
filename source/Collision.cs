using System.Numerics;

static class Collision
{
    const float EPS = 1e-5f; //we do the iterations until todo vector is less than EPS, was 1e-4
    const float SEP = 1e-3f; //separation after each TOI
    const float MERGE_EPS = 1e-4f; //window to merge simultaneous hits
    const int MAX_STEPS = 8; //conservative-advancement iterations
    const int WALL_HIT = -1;
    const int VOXEL_HIT = -2;

    public static void Move(List<Object> objects, Object player, Vector2 delta, Vector2 sceneSize, State state, bool collideWithVoxels)
    {
        Vector2 pos = player.body.position.XY();
        Vector2 todo = delta; //motion left this frame

        Vector2 halfScene = sceneSize * 0.5f;
        float rSelf = player.body.size.AverageRadius2D();

        for (int iter = 0; iter < MAX_STEPS && todo.LengthSquared() > EPS * EPS; ++iter)
        {
            if (!QueryEarliest(objects, player, pos, todo, rSelf, halfScene, state, collideWithVoxels, out float t, out Vector2 n, out _, out _))
            {
                // Nothing blocks the remaining segment
                pos += todo;
                break;
            }
            // --- SOLUTION ---
            // If the hit is in the future (t > 0), advance to the point of impact.
            // Otherwise (t=0), we are already overlapping and must only depenetrate and slide.
            if (t > EPS)
            {
                //1) advance to time-of-impact
                Vector2 step = todo * t;
                pos += step;
                todo *= (1.0f - t);
            }

            //2) guarantee separation for next sweep
            pos += n * SEP;

            //3) slide: remove normal component of remaining velocity
            float vn = Vector2.Dot(todo, n);
            // Only project if moving into the surface, not away from it.
            if (vn < 0)
            {
                todo -= n * vn;
            }

            //4) leftover-motion clamp (optional but good)
            if (todo.LengthSquared() > EPS * EPS)
            {
                float minX = -halfScene.X + rSelf;
                float maxX = halfScene.X - rSelf;
                float minY = -halfScene.Y + rSelf;
                float maxY = halfScene.Y - rSelf;

                float tgtX = pos.X + todo.X;
                if (tgtX < minX) todo.X = minX - pos.X;
                else if (tgtX > maxX) todo.X = maxX - pos.X;

                float tgtY = pos.Y + todo.Y;
                if (tgtY < minY) todo.Y = minY - pos.Y;
                else if (tgtY > maxY) todo.Y = maxY - pos.Y;
            }
        }

        //FINAL absolute clamp – no chance to end outside
        {
            float minX = -halfScene.X + rSelf;
            float maxX = halfScene.X - rSelf;
            float minY = -halfScene.Y + rSelf;
            float maxY = halfScene.Y - rSelf;

            pos = new(Math.Clamp(pos.X, minX, maxX), Math.Clamp(pos.Y, minY, maxY));
        }

        player.body.position = new Vector3(pos, player.body.position.Z);
    }

    ///FirstHit essentially
    public static bool QueryEarliest(List<Object> objects, Object player, Vector2 selfPos, Vector2 d, float rSelf, Vector2 sceneHalf, State state, bool collideWithVoxels, out float bestT, out Vector2 mergedN, out int anyIdx, out bool hitAWall)
    {
        state.collisionHits.Clear();

        TryWalls(selfPos, d, rSelf, sceneHalf, state.collisionHits);
        if (collideWithVoxels) TryVoxels(player, selfPos, d, rSelf, state, state.collisionHits);

        for (int i = 0; i < objects.Count; i++)
        {
            var o = objects[i];
            if (o == player) continue;
            if (Settings.COLLISION3D && !OverlapInZ(player.body, o.body)) continue;

            if (TestObject(o, selfPos, d, rSelf, out float t, out Vector2 n)) state.collisionHits.Add((t, n, i));
        }

        bestT = 1f;
        mergedN = Vector2.Zero;
        anyIdx = -1;
        hitAWall = false;

        if (state.collisionHits.Count == 0) return false;

        ///pass 0 – earliest time
        for (int k = 0; k < state.collisionHits.Count; ++k) if (state.collisionHits[k].t < bestT) bestT = state.collisionHits[k].t;
        if (bestT > 1f - EPS) return false; ///free path

        ///pass 1 – merge simultaneous contacts
        for (int k = 0; k < state.collisionHits.Count; ++k)
        {
            if (MathF.Abs(state.collisionHits[k].t - bestT) <= MERGE_EPS)
            {
                mergedN += state.collisionHits[k].n;
                if (state.collisionHits[k].idx >= 0) anyIdx = state.collisionHits[k].idx;
                if (state.collisionHits[k].idx == WALL_HIT) hitAWall = true;
            }
        }
        mergedN = mergedN.LengthSquared() < EPS * EPS ? -Vector2.Normalize(d) : Vector2.Normalize(mergedN);
        return true;
    }

    static bool OverlapInZ(Body o1, Body o2) => MathF.Abs(o1.position.Z - o2.position.Z) <= 0.5f * (o1.size.Z + o2.size.Z);

    static void TryWalls(Vector2 p, Vector2 d, float r, Vector2 half, List<(float t, Vector2 n, int idx)> hits)
    {
        //X slabs
        if (MathF.Abs(d.X) > EPS)
        {
            float t = (d.X < 0f) ? (-half.X + r - p.X) / d.X : (half.X - r - p.X) / d.X;
            if (t >= 0f && t <= 1f)
            {
                Vector2 n = d.X < 0f ? Vector2.UnitX : -Vector2.UnitX;
                hits.Add((t, n, WALL_HIT));
            }
        }

        //Y slabs
        if (MathF.Abs(d.Y) > EPS)
        {
            float t = (d.Y < 0f) ? (-half.Y + r - p.Y) / d.Y : (half.Y - r - p.Y) / d.Y;
            if (t >= 0f && t <= 1f)
            {
                Vector2 n = d.Y < 0f ? Vector2.UnitY : -Vector2.UnitY;
                hits.Add((t, n, WALL_HIT));
            }
        }
    }

    static void TryVoxels(Object player, Vector2 p, Vector2 d, float r, State state, List<(float t, Vector2 n, int idx)> hits)
    {
        if (!Settings.COLLISION3D)
        {
            TryVoxels2D(p, d, r, state, hits);
            return;
        }

        Vector2 cellHalf = new(0.5f * state.voxelSize, 0.5f * state.voxelSize);
        Vector2 gridMin = -0.5f * state.game.size;
        Vector2 extra = new(r + cellHalf.X, r + cellHalf.Y);
        Vector2 sweepMin = Vector2.Min(p, p + d) - extra;
        Vector2 sweepMax = Vector2.Max(p, p + d) + extra;
        float feetZ = player.body.MinZ();
        float maxZ = player.body.MaxZ();

        int minX = Math.Max(0, (int)MathF.Floor((sweepMin.X - gridMin.X) / state.voxelSize));
        int maxX = Math.Min(state.VMX - 1, (int)MathF.Floor((sweepMax.X - gridMin.X) / state.voxelSize));
        int minY = Math.Max(0, (int)MathF.Floor((sweepMin.Y - gridMin.Y) / state.voxelSize));
        int maxY = Math.Min(state.VMY - 1, (int)MathF.Floor((sweepMax.Y - gridMin.Y) / state.voxelSize));

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                Vector2 center = new(gridMin.X + (x + 0.5f) * state.voxelSize, gridMin.Y + (y + 0.5f) * state.voxelSize);
                for (int z = 0; z < state.VMZ; z++)
                {
                    int index = x + state.VMX * (y + state.VMY * z);
                    if (state.voxelData[index] == 0) continue;

                    float voxelMinZ = (z - State.SURFACE_TERRAIN_LAYER - 1) * state.voxelSize;
                    float voxelMaxZ = (z - State.SURFACE_TERRAIN_LAYER) * state.voxelSize;
                    if (maxZ <= voxelMinZ + EPS) continue;

                    if (feetZ >= voxelMaxZ - EPS) continue;
                    if (SweepCircleAABB(p, d, r, center, cellHalf, out float t, out Vector2 n)) hits.Add((t, n, VOXEL_HIT));
                }
            }
        }
    }

    static bool VoxelFilled2D(State state, int x, int y)
    {
        for (int z = State.SOLID_VOXEL_LAYER; z < state.VMZ; z++)
        {
            int index = x + state.VMX * (y + state.VMY * z);
            if (state.voxelData[index] != 0) return true;
        }
        return false;
    }
    static void TryVoxels2D(Vector2 p, Vector2 d, float r, State state, List<(float t, Vector2 n, int idx)> hits)
    {
        Vector2 cellHalf = new(0.5f * state.voxelSize, 0.5f * state.voxelSize);
        Vector2 gridMin = -0.5f * state.game.size;
        Vector2 extra = new(r + cellHalf.X, r + cellHalf.Y);
        Vector2 sweepMin = Vector2.Min(p, p + d) - extra;
        Vector2 sweepMax = Vector2.Max(p, p + d) + extra;

        int minX = Math.Max(0, (int)MathF.Floor((sweepMin.X - gridMin.X) / state.voxelSize));
        int maxX = Math.Min(state.VMX - 1, (int)MathF.Floor((sweepMax.X - gridMin.X) / state.voxelSize));
        int minY = Math.Max(0, (int)MathF.Floor((sweepMin.Y - gridMin.Y) / state.voxelSize));
        int maxY = Math.Min(state.VMY - 1, (int)MathF.Floor((sweepMax.Y - gridMin.Y) / state.voxelSize));

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                if (!VoxelFilled2D(state, x, y)) continue;

                Vector2 center = new(gridMin.X + (x + 0.5f) * state.voxelSize, gridMin.Y + (y + 0.5f) * state.voxelSize);
                if (SweepCircleAABB(p, d, r, center, cellHalf, out float t, out Vector2 n)) hits.Add((t, n, VOXEL_HIT));
            }
        }
    }

    static bool TestObject(Object o, Vector2 p, Vector2 d, float rSelf, out float t, out Vector2 n)
    {
        if (o.template.shape == SHAPE.CUBOID) return SweepCircleRoundedRectangle(p, d, rSelf, o.body.position.XY(), o.body.size.XY() * 0.5f, out t, out n);
        else return SweepCircleCircle(p, d, rSelf, o.body.position.XY(), o.body.size.AverageRadius2D(), out t, out n);
    }

    ///он просто тестирует пересечение что ли? d is the vector we want to move. Он тестирует все позиции вдоль этого луча? Точнее, вдоль отрезочка t in [0,1]. This would make sense!
    static bool SweepCircleCircle(Vector2 p0, Vector2 d, float r0, Vector2 p1, float r1, out float t, out Vector2 n)
    {
        n = Vector2.Zero; t = 0f;

        Vector2 m = p0 - p1;
        float r = r0 + r1;

        float a = Vector2.Dot(d, d);
        if (a < EPS) return false;

        float b = 2f * Vector2.Dot(d, m);
        float c = Vector2.Dot(m, m) - r * r;

        if (c < 0f)
        {
            n = Vector2.Normalize(m == Vector2.Zero ? d : m);
            return true;
        }

        float disc = b * b - 4f * a * c;
        if (disc < 0f) return false;

        float root = MathF.Sqrt(disc);
        float t0 = (-b - root) / (2f * a);

        if (t0 < 0f || t0 > 1f) return false;

        t = t0;
        n = Vector2.Normalize(p0 + d * t - p1);
        return true;
    }

    static bool SweepCircleAABB(Vector2 p, Vector2 d, float r, Vector2 boxC, Vector2 half, out float t, out Vector2 n)
    {
        n = Vector2.Zero; t = 0f;

        Vector2 min = boxC - half - new Vector2(r, r);
        Vector2 max = boxC + half + new Vector2(r, r);

        float tx1, tx2, ty1, ty2;

        if (MathF.Abs(d.X) < EPS)
        {
            if (p.X < min.X || p.X > max.X) return false;
            tx1 = float.NegativeInfinity;
            tx2 = float.PositiveInfinity;
        }
        else
        {
            float inv = 1f / d.X;
            tx1 = (min.X - p.X) * inv;
            tx2 = (max.X - p.X) * inv;
            if (tx1 > tx2) (tx1, tx2) = (tx2, tx1);
        }

        if (MathF.Abs(d.Y) < EPS)
        {
            if (p.Y < min.Y || p.Y > max.Y) return false;
            ty1 = float.NegativeInfinity;
            ty2 = float.PositiveInfinity;
        }
        else
        {
            float inv = 1f / d.Y;
            ty1 = (min.Y - p.Y) * inv;
            ty2 = (max.Y - p.Y) * inv;
            if (ty1 > ty2) (ty1, ty2) = (ty2, ty1);
        }

        float tEnter = MathF.Max(tx1, ty1);
        float tExit = MathF.Min(tx2, ty2);

        if (tEnter > tExit || tExit < 0f || tEnter > 1f) return false;
        t = MathF.Max(tEnter, 0f);

        //normal: distinguish edge vs corner
        const float CORNER_EPS = 1e-5f;
        if (MathF.Abs(tx1 - ty1) < CORNER_EPS) //corner
        {
            Vector2 cp = p + d * t;
            Vector2 closest = new(Math.Clamp(cp.X, boxC.X - half.X, boxC.X + half.X), Math.Clamp(cp.Y, boxC.Y - half.Y, boxC.Y + half.Y));
            n = Vector2.Normalize(cp - closest);
        }
        else if (tx1 > ty1) n = d.X > 0f ? -Vector2.UnitX : Vector2.UnitX; //X-slab first
        else n = d.Y > 0f ? -Vector2.UnitY : Vector2.UnitY; //Y-slab first
        return true;
    }

    static bool SweepCircleRoundedRectangle(Vector2 p, Vector2 d, float r, Vector2 boxC, Vector2 half, out float t, out Vector2 n)
    {
        t = 0f;
        n = Vector2.Zero;

        float cornerRadius = 0.2f * MathF.Min(half.X, half.Y);
        if (!SweepCircleAABB(p, d, r, boxC, half, out float t0, out Vector2 n0)) return false;

        Vector2 impactPoint = p + d * t0;
        Vector2 innerHalf = half - new Vector2(cornerRadius, cornerRadius);

        Vector2 closestFeaturePoint = new(Math.Clamp(impactPoint.X, boxC.X - innerHalf.X, boxC.X + innerHalf.X), Math.Clamp(impactPoint.Y, boxC.Y - innerHalf.Y, boxC.Y + innerHalf.Y));
        Vector2 delta = impactPoint - closestFeaturePoint;

        // A very small delta means the impact point was on or inside the inner rect's bounds,
        // which corresponds to a collision with a flat edge of the rounded rectangle.
        const float FEATURE_EPS = 1e-4f;
        if (delta.LengthSquared() < FEATURE_EPS * FEATURE_EPS)
        {
            // Impact is on a flat edge. The AABB sweep result is correct and sufficient.
            t = t0;
            n = n0;
            return true;
        }
        else return SweepCircleCircle(p, d, r, closestFeaturePoint, cornerRadius, out t, out n);
    }

    public static bool IsDirectionFree(Object self, Vector2 direction, List<Object> allObjects, Vector2 sceneSize, State state, bool collideWithVoxels)
    {
        const float PROBE_DISTANCE = 1.0f;

        if (direction.LengthSquared() < EPS * EPS) return true;

        Vector2 selfPos = self.body.position.XY();
        float rSelf = self.body.size.AverageRadius2D();
        Vector2 halfScene = sceneSize * 0.5f;
        Vector2 delta = direction * PROBE_DISTANCE;

        if (QueryEarliest(allObjects, self, selfPos, delta, rSelf, halfScene, state, collideWithVoxels, out float t, out _, out _, out _)) return t > 0.5f; //EPS;
        return true;
    }
}
