using System.Numerics;

struct Body
{
    public Vector3 position, size;
    public SHAPE shape;

    public Body(Vector3 position, Vector3 size, SHAPE shape)
    {
        this.position = position;
        this.size = size;
        this.shape = shape;
    }

    public readonly Vector3 Min() => position - 0.5f * size;
    public readonly Vector3 Max() => position + 0.5f * size;
    public readonly float MinZ() => position.Z - 0.5f * size.Z;
    public readonly float MaxZ() => position.Z + 0.5f * size.Z;
    public readonly (float, float) MinXY() => (position.X - 0.5f * size.X, position.Y - 0.5f * size.Y);
    public readonly (float, float) MaxXY() => (position.X + 0.5f * size.X, position.Y + 0.5f * size.Y);
    public readonly Vector3 FeetPosition() => position - new Vector3(0f, 0f, 0.5f * size.Z);

    public readonly float Volume(SHAPE shape)
    {
        if (shape == SHAPE.CUBOID) return size.X * size.Y * size.Z;
        else if (shape == SHAPE.SPHERE)
        {
            float R = size.AverageRadius();
            return 4f / 3f * MathF.PI * R * R * R;
        }
        else if (shape == SHAPE.CYLINDER)
        {
            float R = size.AverageRadius2D();
            return MathF.PI * R * R * size.Z;
        }
        else if (shape == SHAPE.CAPSULE)
        {
            float R = size.AverageRadius2D();
            return MathF.PI * R * R * (size.Z - 2f / 3f * R);
        }
        else throw new Exception($"unknown shape <{shape}>");
    }
    public readonly float LinearSize() => (size.X + size.Y + size.Z) / 3.0f;
    public readonly bool Supports(Body o) => o.MinZ() < MaxZ() + 0.01f;

    public static float BodyDistance(Body o1, Body o2)
    {
        float flatDistance = BodyDistance2D(o1, o2);
        if (!Settings.COLLISION3D) return flatDistance;

        float zDistance = MathF.Abs(o1.position.Z - o2.position.Z) - 0.5f * (o1.size.Z + o2.size.Z);

        float outsideFlat = MathF.Max(flatDistance, 0f);
        float outsideZ = MathF.Max(zDistance, 0f);
        float outside = MathF.Sqrt(outsideFlat * outsideFlat + outsideZ * outsideZ);
        float inside = MathF.Min(MathF.Max(flatDistance, zDistance), 0f);

        return outside + inside;
    }

    public static float BodyDistance2D(Body o1, Body o2)
    {
        if (o1.shape != SHAPE.CUBOID && o2.shape != SHAPE.CUBOID)
        {
            return VH.FlatDistance(o1.position, o2.position) - o1.size.AverageRadius2D() - o2.size.AverageRadius2D();
        }
        else if (o1.shape == SHAPE.CUBOID && o2.shape == SHAPE.CUBOID)
        {
            (float minx1, float miny1) = o1.MinXY();
            (float maxx1, float maxy1) = o1.MaxXY();
            (float minx2, float miny2) = o2.MinXY();
            (float maxx2, float maxy2) = o2.MaxXY();
            return MathF.Max(MathF.Max(minx2 - maxx1, minx1 - maxx2), MathF.Max(miny2 - maxy1, miny1 - maxy2));
        }
        else
        {
            Body circle = o1.shape == SHAPE.CUBOID ? o2 : o1;
            Body rect = o1.shape == SHAPE.CUBOID ? o1 : o2;

            (float minx, float miny) = rect.MinXY();
            (float maxx, float maxy) = rect.MaxXY();

            float closestX = Math.Clamp(circle.position.X, minx, maxx);
            float closestY = Math.Clamp(circle.position.Y, miny, maxy);

            float dx = circle.position.X - closestX;
            float dy = circle.position.Y - closestY;
            float distance = MathF.Sqrt(dx * dx + dy * dy);
            return distance - circle.size.AverageRadius2D();
        }
    }

    public static float BodyDistanceTo2DCapsule(Body o1, Vector2 v, Body o2)
    {
        Vector2 p = o2.position.XY() - o1.position.XY();
        float v2 = v.LengthSquared();
        float t = v2 > 0f ? Math.Clamp(Vector2.Dot(p, v) / v2, 0f, 1f) : 0f;
        return (p - t * v).Length() - o1.size.AverageRadius2D() - o2.size.AverageRadius2D();
    }

    public static float SceneOverlapSDF(Body o, Vector2 sceneMin, Vector2 sceneMax)
    {
        (float minx, float miny) = o.MinXY();
        (float maxx, float maxy) = o.MaxXY();

        float left = minx - sceneMin.X;
        float right = sceneMax.X - maxx;
        float bottom = miny - sceneMin.Y;
        float top = sceneMax.Y - maxy;

        return MathF.Min(MathF.Min(left, right), MathF.Min(bottom, top));
    }
    
    public static bool Near(Body o1, Body o2) => BodyDistance(o1, o2) <= Settings.NEAR_DISTANCE;

    /*public readonly float SceneSDF(Vector2 sceneSize)
    {
        if (shape == SHAPE.CUBOID)
            return MathF.Min(MathF.Min(0.5f * sceneSize.X + position.X, 0.5f * sceneSize.X - position.X) - 0.5f * size.X, MathF.Min(0.5f * sceneSize.Y + position.Y, 0.5f * sceneSize.Y - position.Y) - 0.5f * size.Y);
        else if (shape == SHAPE.SPHERE || shape == SHAPE.CYLINDER || shape == SHAPE.CAPSULE)
            return MathF.Min(MathF.Min(0.5f * sceneSize.X + position.X, 0.5f * sceneSize.X - position.X), MathF.Min(0.5f * sceneSize.Y + position.Y, 0.5f * sceneSize.Y - position.Y)) - size.AverageRadius2D();
        else throw new Exception($"unknown shape <{shape}>");
    }*/
}
