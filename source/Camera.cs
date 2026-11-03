using System.Numerics;
using Raylib_cs;

class Camera
{
    Camera3D rlcam;
    Vector3 corner, horizontal, vertical, right, up, forward; ///lower-left image-plane corner

    public CAMERA ENUM;
    public enum CAMERA { PERSPECTIVE, ISOMETRIC, TOPDOWN, FOLLOWING, FIRSTPERSON };

    public Camera(CAMERA ENUM, Vector2 sceneSize)
    {
        this.ENUM = ENUM;
        Update(Vector3.Zero, sceneSize);
    }

    public void Update(Vector3 target, Vector2 sceneSize)
    {
        float sceneRadius = 0.25f * (sceneSize.X + sceneSize.Y);
        (Vector3 direction, float height) = ENUM switch
        {
            CAMERA.PERSPECTIVE => (new Vector3(0.4f, -6f, 10f), sceneRadius * 3.1f), ///3f, 3.25f
            CAMERA.ISOMETRIC => (new Vector3(-1f, -1f, 2f), sceneRadius * 2f),
            CAMERA.TOPDOWN => (Vector3.UnitZ, sceneRadius * 2f),
            CAMERA.FOLLOWING => (new Vector3(-1f, -1f, 2f), 18f),
            CAMERA.FIRSTPERSON => (new Vector3(0.4f, -6f, 10f), sceneRadius * 3f),
            _ => throw new Exception("cannot happen")
        };

        direction = Vector3.Normalize(direction);
        Vector3 pos = target + height * direction;
        Update(pos, target, height);
    }

    public void Update(Object o)
    {
        Vector3 pos = o.body.position + new Vector3(0f, 0f, 0.25f * o.body.size.Z);
        Update(pos, pos + new Vector3(o.dir, 0f), 0f);
    }

    void Update(Vector3 pos, Vector3 target, float height)
    {
        forward = Vector3.Normalize(target - pos);
        (right, Vector3 basisUp) = ViewBasis(forward);
        up = basisUp;

        if (IsPerspective())
        {
            const float perspectiveFov = MathF.PI / 4f;
            rlcam = new Camera3D(pos, target, basisUp, perspectiveFov * Raylib.RAD2DEG, CameraProjection.Perspective);

            float aspectRatio = Settings.SHX / (float)Settings.SHY;
            const float vlength = 1.0f;
            horizontal = aspectRatio * vlength * right;
            vertical = vlength * basisUp;

            float fovY = rlcam.FovY * Raylib.DEG2RAD;
            float depth = 0.5f * vlength / MathF.Tan(fovY / 2.0f);
            corner = rlcam.Position + depth * forward - 0.5f * horizontal - 0.5f * vertical;
        }
        else
        {
            rlcam = new Camera3D(pos, target, basisUp, height, CameraProjection.Orthographic);

            float aspectRatio = Settings.SHX / (float)Settings.SHY;
            horizontal = aspectRatio * height * right;
            vertical = height * basisUp;
            corner = rlcam.Position - 0.5f * horizontal - 0.5f * vertical;
        }
    }

    static (Vector3 right, Vector3 up) ViewBasis(Vector3 forward)
    {
        Vector3 right = Vector3.Cross(forward, Vector3.UnitZ);
        if (right.LengthSquared() < 1e-6f) right = Vector3.UnitX;
        else right = Vector3.Normalize(right);

        Vector3 up = Vector3.Normalize(Vector3.Cross(right, forward));
        return (right, up);
    }

    public Camera3D RLCamera() => rlcam;
    public (Vector3 position, Vector3 corner, Vector3 horizontal, Vector3 vertical) Corners() => (rlcam.Position, corner, horizontal, vertical);
    public bool IsPerspective() => ENUM == CAMERA.PERSPECTIVE || ENUM == CAMERA.FIRSTPERSON;
    
    public bool Visible(Vector3 center, float radius)
    {
        Vector3 rel = center - rlcam.Position;
        float x = Vector3.Dot(rel, right);
        float y = Vector3.Dot(rel, up);
        float z = Vector3.Dot(rel, forward);

        float halfWidth = 0.5f * horizontal.Length();
        float halfHeight = 0.5f * vertical.Length();
        return z + radius >= 0f && MathF.Abs(x) <= halfWidth + radius && MathF.Abs(y) <= halfHeight + radius;
    }
}
