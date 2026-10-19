using System.Numerics;

static class VH
{
    public static float FlatDistanceSquared(Vector3 v1, Vector3 v2) => (v1.X - v2.X) * (v1.X - v2.X) + (v1.Y - v2.Y) * (v1.Y - v2.Y);
    public static float FlatDistance(Vector3 v1, Vector3 v2) => MathF.Sqrt((v1.X - v2.X) * (v1.X - v2.X) + (v1.Y - v2.Y) * (v1.Y - v2.Y));

    public static float AverageRadius(this Vector3 v) => (v.X + v.Y + v.Z) / 6f;
    public static float AverageRadius2D(this Vector3 v) => (v.X + v.Y) / 4f;

    public static Vector2 XY(this Vector3 v) => new(v.X, v.Y);
    public static Vector3 V3(this Vector2 v) => new(v.X, v.Y, 0f);

    public static float Volume(this Vector3 v) => v.X * v.Y * v.Z;

    /*public static Vector3 Normalized2D(this Vector3 v)
    {
        float length2d = MathF.Sqrt(v.X * v.X + v.Y * v.Y);
        return new Vector3(v.X / length2d, v.Y / length2d, 0.0f);
    }
    public static Vector3 Normalized(this Vector3 v)
    {
        float length = MathF.Sqrt(v.X * v.X + v.Y * v.Y + v.Z * v.Z);
        return new Vector3(v.X / length, v.Y / length, v.Z / length);
    }*/

    public static Vector2 Rotated(this Vector2 v, float angle) ///это стандартное правильное вращение против часовой стрелки
    {
        float cos = MathF.Cos(angle), sin = MathF.Sin(angle);
        return new Vector2(v.X * cos - v.Y * sin, v.X * sin + v.Y * cos);
    }
    public static Vector2 RotatedP45(this Vector2 v) => 0.7071f * new Vector2(v.X - v.Y, v.X + v.Y);
    public static Vector2 RotatedM45(this Vector2 v) => 0.7071f * new Vector2(v.X + v.Y, v.Y - v.X);
    public static Vector2 Left(this Vector2 v) => new(-v.Y, v.X);
    public static Vector2 Right(this Vector2 v) => new(v.Y, -v.X);

    public static Vector3 Gray(float A) => new(A, A, A);

    public static Vector2 Unit(float angle) => new(MathF.Cos(angle), MathF.Sin(angle));
    public static Vector2 Unit(LCG lcg)
    {
        float angle = lcg.NextSingle() * MathF.Tau;
        return new(MathF.Cos(angle), MathF.Sin(angle));
    }
    public static Vector3 Unit3d(float angle) => new(MathF.Cos(angle), MathF.Sin(angle), 0f);

    public static bool Same(Vector2 a, Vector2 b, float eps) => MathF.Abs(a.X - b.X) < eps && MathF.Abs(a.Y - b.Y) < eps;
    public static bool Same(Vector3 a, Vector3 b, float eps) => MathF.Abs(a.X - b.X) < eps && MathF.Abs(a.Y - b.Y) < eps && MathF.Abs(a.Z - b.Z) < eps;

    public static float Det(Vector2 v1, Vector2 v2) => v1.X * v2.Y - v1.Y * v2.X;

    public static float SDFRect(Vector2 point, Vector2 rectCenter, Vector2 rectHalfSize)
    {
        Vector2 p = point - rectCenter;
        Vector2 q = Vector2.Abs(p) - rectHalfSize;
        return Vector2.Max(q, Vector2.Zero).Length() + Math.Min(Math.Max(q.X, q.Y), 0.0f);
    }

    public static Vector2 Clamped(Vector2 policyVec, Vector2 dir, float rotspeed)
    {
        Vector2 normalized = Vector2.Normalize(policyVec);

        float det = Det(dir, normalized);
        Vector2 result = dir;

        if (det < 0.0005f)
        {
            result = dir.Rotated(-rotspeed);
            if (Det(result, normalized) > 0) result = normalized;
        }
        else if (det > 0.0005f)
        {
            result = dir.Rotated(rotspeed);
            if (Det(result, normalized) < 0) result = normalized;
        }
        return result;
    }
}
