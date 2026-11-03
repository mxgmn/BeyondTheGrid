using System.Numerics;
using Raylib_cs;

unsafe class MyShader
{
    const float SUNRISE = 0.55f * 0.5f * MathF.PI;
    const float BASE_SUN_ANGLE = -0.25f * 0.5f * MathF.PI;

    Shader shader;
    int locData, locVoxelData, locNumObjects, locVM, locVoxelSize, locCameraOrthographic, locToSun;
    int locEmoji, locVoxelFaces;
    int locCameraPosition, locCameraForward, locCameraCorner, locCameraHorizontal, locCameraVertical, locNumTextures;
    float sunAngle = BASE_SUN_ANGLE;

    float[] objectTexelData;
    Texture2D objectTexture, voxelTexture, emojiTexture, voxelFaceTexture;
    Game loadedGame;

    ///идея в том, чтобы инициализировать текстуру постоянного размера, и больше с ней не возиться пока объекты появляются и исчезают
    const int MAX_OBJECTS = 256, TEXELS_PER_OBJECT = 3, OBJECT_TEXELS = MAX_OBJECTS * TEXELS_PER_OBJECT;

    public MyShader(string filename, int SHX, int SHY)
    {
        if (!File.Exists("resources/textures.png")) throw new FileNotFoundException("texture atlas not found");
        voxelFaceTexture = Raylib.LoadTexture("resources/textures.png");
        Raylib.SetTextureFilter(voxelFaceTexture, TextureFilter.Point);

        if (!File.Exists(filename)) throw new FileNotFoundException($"shader file <{filename}> not found");
        shader = Raylib.LoadShaderFromMemory(null, Helper.ReadAllText(filename));
        if (shader.Id == 0) throw new Exception($"failed to load shader");

        shader.SetVec2("resolution", new Vector2(SHX, SHY));

        locData = Raylib.GetShaderLocation(shader, "data");
        locVoxelData = Raylib.GetShaderLocation(shader, "voxels");
        locNumObjects = Raylib.GetShaderLocation(shader, "numObjects");
        locVM = Raylib.GetShaderLocation(shader, "VM");
        locVoxelSize = Raylib.GetShaderLocation(shader, "voxelSize");
        locCameraOrthographic = Raylib.GetShaderLocation(shader, "cameraOrthographic");
        locToSun = Raylib.GetShaderLocation(shader, "toSun");
        locEmoji = Raylib.GetShaderLocation(shader, "emojiTex");
        locVoxelFaces = Raylib.GetShaderLocation(shader, "voxelFaceTex");
        locCameraPosition = Raylib.GetShaderLocation(shader, "cameraPosition");
        locCameraForward = Raylib.GetShaderLocation(shader, "cameraForward");
        locCameraCorner = Raylib.GetShaderLocation(shader, "cameraCorner");
        locCameraHorizontal = Raylib.GetShaderLocation(shader, "cameraHorizontal");
        locCameraVertical = Raylib.GetShaderLocation(shader, "cameraVertical");
        locNumTextures = Raylib.GetShaderLocation(shader, "numTextures");

        const int MAX_NUM_VOXELS = State.VOXEL_LAYER_COUNT * 50 * 50;

        objectTexelData = new float[OBJECT_TEXELS * 4];

        objectTexture = CreateTexture(OBJECT_TEXELS, PixelFormat.UncompressedR32G32B32A32);
        voxelTexture = CreateTexture(MAX_NUM_VOXELS, PixelFormat.UncompressedGrayscale);
        RotateSun(0f);
    }

    public void RotateSun(float step)
    {
        sunAngle += step;
        if (sunAngle >= 2f * MathF.PI) sunAngle -= 2f * MathF.PI;
        Vector3 toSun = new(MathF.Cos(SUNRISE) * MathF.Cos(sunAngle), MathF.Cos(SUNRISE) * MathF.Sin(sunAngle), MathF.Sin(SUNRISE));
        Raylib.SetShaderValue(shader, locToSun, toSun, ShaderUniformDataType.Vec3);
    }

    public void Update(State state, Camera camera)
    {
        if (loadedGame != state.game)
        {
            if (emojiTexture.Id != 0) Raylib.UnloadTexture(emojiTexture);
            emojiTexture = RaylibHelper.EmojiAtlas(state.game.emojiSymbols);
            Raylib.SetShaderValue(shader, locNumTextures, state.game.emojiSymbols.Count, ShaderUniformDataType.Int);
            loadedGame = state.game;
        }

        if (camera.ENUM == Camera.CAMERA.FOLLOWING || camera.ENUM == Camera.CAMERA.FIRSTPERSON)
        {
            Object player = state.Player();
            if (camera.ENUM == Camera.CAMERA.FOLLOWING) camera.Update(player.body.position, state.game.size);
            else camera.Update(player);
        }

        Camera3D rlCamera = camera.RLCamera();
        (Vector3 position, Vector3 corner, Vector3 horizontal, Vector3 vertical) = camera.Corners();
        Vector3 forward = Vector3.Normalize(rlCamera.Target - rlCamera.Position);

        Raylib.SetShaderValue(shader, locCameraPosition, position, ShaderUniformDataType.Vec3);
        Raylib.SetShaderValue(shader, locCameraForward, forward, ShaderUniformDataType.Vec3);
        Raylib.SetShaderValue(shader, locCameraCorner, corner, ShaderUniformDataType.Vec3);
        Raylib.SetShaderValue(shader, locCameraHorizontal, horizontal, ShaderUniformDataType.Vec3);
        Raylib.SetShaderValue(shader, locCameraVertical, vertical, ShaderUniformDataType.Vec3);

        Raylib.SetShaderValue(shader, locCameraOrthographic, camera.IsPerspective() ? 0 : 1, ShaderUniformDataType.Int);
        Raylib.SetShaderValue(shader, locVM, new Vector3(state.VMX, state.VMY, state.VMZ), ShaderUniformDataType.Vec3);
        Raylib.SetShaderValue(shader, locVoxelSize, state.voxelSize, ShaderUniformDataType.Float);

        int numVisibleObjects = EncodeObjects(state, camera);
        if (numVisibleObjects > 0) Raylib.UpdateTextureRec(objectTexture, new Rectangle(0, 0, numVisibleObjects * TEXELS_PER_OBJECT, 1), objectTexelData);

        if (state.voxelData.Length > voxelTexture.Width) throw new Exception("voxel map is too large for shader");
        Raylib.UpdateTextureRec(voxelTexture, new Rectangle(0, 0, state.voxelData.Length, 1), state.voxelData);
        Raylib.SetShaderValue(shader, locNumObjects, numVisibleObjects, ShaderUniformDataType.Int);
    }

    static Texture2D CreateTexture(int width, PixelFormat format)
    {
        Image img = new()
        {
            Data = null,
            Width = width,
            Height = 1,
            Mipmaps = 1,
            Format = format
        };
        Texture2D result = Raylib.LoadTextureFromImage(img);
        Raylib.SetTextureFilter(result, TextureFilter.Point);
        return result;
    }

    int EncodeObjects(State state, Camera camera)
    {
        Object firstPersonPlayer = camera.ENUM == Camera.CAMERA.FIRSTPERSON ? state.Player() : null;
        int stride = TEXELS_PER_OBJECT * 4;

        int visibleCount = 0;
        for (int i = 0; i < state.maxObjectCount; i++)
        {
            Object o = state.array[i];
            if (o == null || o == firstPersonPlayer) continue;
            if (camera.ENUM == Camera.CAMERA.FOLLOWING && !camera.Visible(o.body.position, 0.5f * o.body.size.Length())) continue; ///frustum culling

            if (visibleCount == MAX_OBJECTS) throw new Exception($"too many visible objects for shader: {visibleCount + 1} > {MAX_OBJECTS}");
            int baseIndex = visibleCount++ * stride;

            objectTexelData[baseIndex + 0] = o.body.size.X;
            objectTexelData[baseIndex + 1] = o.body.size.Y;
            objectTexelData[baseIndex + 2] = o.body.size.Z;
            objectTexelData[baseIndex + 3] = (int)o.template.shape;

            objectTexelData[baseIndex + 4] = o.body.position.X;
            objectTexelData[baseIndex + 5] = o.body.position.Y;
            objectTexelData[baseIndex + 6] = o.body.position.Z;
            objectTexelData[baseIndex + 7] = MathF.Atan2(o.dir.Y, o.dir.X);

            objectTexelData[baseIndex + 8] = o.color.X;
            objectTexelData[baseIndex + 9] = o.color.Y;
            objectTexelData[baseIndex + 10] = o.color.Z;
            objectTexelData[baseIndex + 11] = o.template.textureID;
        }

        return visibleCount;
    }

    public void Draw()
    {
        Raylib.BeginShaderMode(shader);

        Raylib.SetShaderValueTexture(shader, locData, objectTexture);
        Raylib.SetShaderValueTexture(shader, locVoxelData, voxelTexture);
        Raylib.SetShaderValueTexture(shader, locEmoji, emojiTexture);
        Raylib.SetShaderValueTexture(shader, locVoxelFaces, voxelFaceTexture);

        Raylib.DrawRectangle(0, 0, Settings.SHX, Settings.SHY, Color.White);
        Raylib.EndShaderMode();
    }

    public void Unload()
    {
        if (objectTexture.Id != 0) Raylib.UnloadTexture(objectTexture);
        if (voxelTexture.Id != 0) Raylib.UnloadTexture(voxelTexture);
        if (emojiTexture.Id != 0) Raylib.UnloadTexture(emojiTexture);
        if (voxelFaceTexture.Id != 0) Raylib.UnloadTexture(voxelFaceTexture);
        Raylib.UnloadShader(shader);
    }
}
