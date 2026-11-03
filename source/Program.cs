/*
TODO: Сделать сенсор, который мерит расстояние до цели (ближайшего объекта темплейта). С таким сенсором довольно естественно замерять расстояние в нескольких точках, и потом находить градиент, но наверняка же найдутся какие-то удивительные политики? Можно попробовать такой сенсор в каждой из предыдущих игр.
TODO: Например, интересно с таким сенсором убегать от (медленной кошки). Но тут точно нужно как-то сохранять предыдущее показание сенсора? Да, очень интересно провести такой эксперимент! Это даст интересные истории успеха, которые можно рассказывать на защите.

NEXT: делать проверку, что каждый паттерн был засчитан. Потому что иногда мы можем ошибиться и написать аргументы не в том порядке в симметричном паттерне.

TODO: JurassicPark до сих пор нет в моём фреймворке, это было интересно. Есть хайдер, но в парке интересно то, что этот динозавр гораздо быстрее.
TODO: Хайдер, который более похож на хайдер. Сравнить его со своим хайдером, написанном вручную.

NEXT: Make multiagent version of buttoncheese - I had this one before. При этом добавь мышам память. Будет очень интересно, появится ли распределение по ролям. И если я сложню систему (добавлю память), то м.б. и более интересные алгоритмы появятся. М.б. можно схемы как-то генерировать и без ЛЛМ, и оценивать их перспективность. В футболе мне не нравилось последовательное goalAhead friendAhead, например. Может быть, это можно сделать через абстрактную интерпретацию.

NEXT: Hide, но где сыщик может нападать на тайловый след игрока. Это будет лучше работать в дискретном случае, видимо.

TODO: DeSugar() на самом деле будет более короткий, потому что там не нужно будет делать тайпчеки. Так что он таки полезен.

TODO: Remove TypeHelper.




*/

using System.Numerics;
using Raylib_cs;

static class Settings
{
    public const uint SEED = 3;

    ///public const int SHX = 960, SHY = 960;
    public const int SHX = 1200, SHY = 1200;
    ///public const int SHX = 1360, SHY = 1360;
    public const int SCALE = 1; ///set more than 1 to enable pixart graphics or scale to large monitors

    public const bool DRAW_NAMES = false, DRAW_DIRS = false, DRAW_SENSORS = true, DRAW_POLICY_VECTORS = false, DRAW_FIELD = false;
    public const bool RANDOMIZE = false, HOLONOMIC = false, SAVE_ALL_POLICIES = false, PRINT_ONLY_POLICIES = true, PRINT_EXPRESSIONS = false;
    public const int FPS = 60, FRAMES_PER_AGENT_CLI = 150;

    public const bool COLLISION3D = false;
    public const int NUM_RAYS = 32; ///16, for gap it's x2
    public const float MAX_DIRECTION_ZONE = 0.5f, ALIGNMENT_POWER = 0.25f; ///0.5f, 0.25f

    public const float SPEED = 1.0f * 0.04f;
    public const float ROTSPEED = 1.0f * 0.05f;
    public const float NEAR_DISTANCE = 0.01f;
    public const float DISCOUNT_DEFAULT = 1f; ///0.99998f

    public const int MAX_SUB_LENGTH = 3, MAX_PRINTED_EXPRESSION_LENGTH = 11;
    public const OP DEBUG_OP = OP.SEQ; ///set to SEQ to print everything
    public const float AGENT_TIMEOUT = 20f;
}

static class Program
{
    static void Main(string[] args)
    {
        try { Run(args); }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or FormatException or IOException)
        {
            Console.Error.WriteLine($"ERROR: {error.Message}");
            Environment.ExitCode = 1;
        }
    }

    static void Run(string[] args)
    {
        string gamesText = File.ReadAllText("games.lisp");
        (Expr xroot, string gamesError) = Expr.Load($"(games {gamesText})");
        if (gamesError != null)
        {
            Console.WriteLine(gamesError);
            Environment.ExitCode = 1;
            return;
        }

        Expr[] xargs = null;
        if (xroot.children.Length > 0 && xroot.children[0].head == "args") xargs = xroot.children[0].children;
        else if (args.Length > 0)
        {
            const string LOGFILE = "log.lisp";
            int timeLimit = xroot.Children("test").FirstOrDefault()?.GetInt("timeLimitMinutes", 0) ?? 0;
            if (timeLimit > 0 && File.Exists(LOGFILE) && DateTime.UtcNow - File.GetCreationTimeUtc(LOGFILE) > TimeSpan.FromMinutes(timeLimit))
            {
                Console.WriteLine($"Time ({timeLimit} minutes) is up. Your discoveries have been saved.");
                return;
            }

            string join = string.Join(' ', args);

            string timestamp = DateTime.Now.ToString("HH-mm-ss");
            File.AppendAllText(LOGFILE, $"(time {timestamp} {join})" + Environment.NewLine);

            xargs = new Expr[args.Length];
            for (int i = 0; i < xargs.Length; i++)
            {
                (xargs[i], string error) = Expr.Load(args[i]);
                if (error != null)
                {
                    Console.WriteLine($"ARGUMENT ERROR: {error}");
                    Environment.ExitCode = 1;
                    return;
                }
            }
        }

        Expr[] realGames = xroot.Children("real");
        Expr[] testGames = xroot.Children("test");
        Expr[] searchGames = xroot.Children("search");
        Expr[] prepareGames = xroot.Children("prepare");

        if (xargs != null)
        {
            if (xargs.Length == 0) throw new ArgumentException("Supply a policy or a search expression");
            if (testGames.Length == 0)
            {
                Console.Error.WriteLine("Command-line evaluation or search requires a (test \"Game\") entry in games.lisp.");
                Environment.ExitCode = 1;
                return;
            }
            if (xargs.Any(a => a.head == "search"))
            {
                if (xargs.Length != 1) throw new ArgumentException("Pass one search expression per invocation");
                Evals.SearchSketch(testGames[0], xargs[0]);
            }
            else Evals.Test(testGames[0], xargs);
            return;
        }

        foreach (Expr xgame in prepareGames) Evals.Prepare(xgame);

        if (realGames.Length > 0)
        {
            Interactive(new AppState(realGames));
            return;
        }
        foreach (Expr xgame in testGames) Evals.Test(xgame, null);

        if (searchGames.Length > 0)
        {
            //File.Delete("discoveries.lisp");
            if (Settings.NUM_RAYS != 16) CS.WriteLine($"WARNING: NUM_RAYS = {Settings.NUM_RAYS}", ConsoleColor.DarkYellow);

            Report report = new();
            foreach (Expr xgame in searchGames)
            {
                string gameName = xgame.children[0].head;
                int steps = 3600, minLength = 1, maxLength = 10, numSeeds = 5;
                for (int i = 1; i < xgame.children.Length; i++)
                {
                    Expr child = xgame.children[i];

                    if (child.head == "steps") steps = int.Parse(child.children[0].head);
                    else if (child.head == "minLength") minLength = int.Parse(child.children[0].head);
                    else if (child.head == "maxLength") maxLength = int.Parse(child.children[0].head);
                    else if (child.head == "numSeeds") numSeeds = int.Parse(child.children[0].head);
                }

                Game game = Game.LoadFromFile(gameName).game;
                if (game.error != null)
                {
                    Console.WriteLine(game.error);
                    return;
                }
                Template agent = game.templates[game.agentTemplate];

                (Codon, TYPE)[] grammar = Record.Grammar(agent, game.templates);
                GameReport gameReport = Search.BottomUp(game, minLength, maxLength, grammar, numSeeds, steps, []);
                report.Add(gameReport);
            }
            report.Save();
        }
    }

    static void Interactive(AppState a)
    {
        Raylib.SetTraceLogLevel(TraceLogLevel.Error);
        Raylib.SetConfigFlags(ConfigFlags.Msaa4xHint);
        Raylib.InitWindow(Settings.SHX * Settings.SCALE, Settings.SHY * Settings.SCALE, "BeyondTheGrid");

        bool success = a.LoadGame();
        if (!success) return;

        Font tamzen = Raylib.LoadFont("resources/Tamzen10x20.fnt");
        Raylib.SetTextureFilter(tamzen.Texture, TextureFilter.Point);
        //Font jetBrainsMono = Raylib.LoadFontEx("resources/JetBrainsMono-Regular.ttf", 64, null, 0);
        Font jetBrainsMono = Raylib.LoadFontEx("resources/JetBrainsMono-Regular.ttf", 48, null, 0);

        RenderTexture2D target = Raylib.LoadRenderTexture(Settings.SHX, Settings.SHY);

        Raylib.SetTargetFPS(Settings.FPS);
        bool PLAYER = false;
        Object player = a.state.Player();

        while (!Raylib.WindowShouldClose())
        {
            if (Raylib.IsKeyPressed(KeyboardKey.Enter))
            {
                if (a.menu)
                {
                    bool gameLoadSuccess = a.LoadGame();
                    if (!gameLoadSuccess) return;
                    a.gameover = false;
                }
                a.menu = !a.menu;
            }
            else if (Raylib.IsKeyPressed(KeyboardKey.Space))
            {
                if (a.menu)
                {
                    a.menu = false;
                    a.paused = false;
                }
                else if (a.paused) a.paused = false;
                else a.paused = true;
            }
            else if (a.menu && Raylib.IsKeyPressed(KeyboardKey.Down)) a.MenuDown();
            else if (a.menu && Raylib.IsKeyPressed(KeyboardKey.Up)) a.MenuUp();
            else if (Raylib.IsKeyPressed(KeyboardKey.Tab)) PLAYER = !PLAYER;
            else if (a.paused && Raylib.IsKeyDown(KeyboardKey.Left)) a.shader.RotateSun(0.02f);
            else if (a.paused && Raylib.IsKeyDown(KeyboardKey.Right)) a.shader.RotateSun(-0.02f);
            else if ((!a.menu && !a.paused) || Raylib.IsKeyPressed(KeyboardKey.G))
            {
                Value value = Value.VOID;
                if (PLAYER)
                {
                    KeyboardState keyboard = new(Raylib.IsKeyDown(KeyboardKey.Up), Raylib.IsKeyDown(KeyboardKey.Down), Raylib.IsKeyDown(KeyboardKey.Left), Raylib.IsKeyDown(KeyboardKey.Right), Raylib.IsKeyDown(KeyboardKey.Z), Raylib.IsKeyDown(KeyboardKey.J));
                    value = Value.ActionValue(keyboard, player);
                }
                a.gameover = a.state.Step(value, null);
                if (a.gameover) a.menu = true;
                a.shader.Update(a.state, a.camera);
            }

            Raylib.BeginTextureMode(target);
            Rlgl.EnableDepthTest();
            Rlgl.EnableDepthMask();
            Raylib.ClearBackground(Color.Orange);
            a.shader.Draw();
            if (Settings.DRAW_DIRS || Settings.DRAW_SENSORS)
            {
                Raylib.BeginMode3D(a.camera.RLCamera());
                if (Settings.DRAW_DIRS) DrawDirs(a);
                if (Settings.DRAW_SENSORS) DrawSensors(a);
                Raylib.EndMode3D();
            }
            Rlgl.DisableDepthTest();
            Rlgl.DisableDepthMask();
            Raylib.EndTextureMode();

            if (Raylib.IsKeyPressed(KeyboardKey.F11))
            {
                Image screenshot = Raylib.LoadImageFromTexture(target.Texture);
                Raylib.ImageFlipVertical(ref screenshot);
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd-HHmmss");
                string filename = $"{a.game.name}-{timestamp}.png";
                Raylib.ExportImage(screenshot, filename);
                Raylib.UnloadImage(screenshot);
                Console.WriteLine($"screenshot taken: {filename}");
            }

            Raylib.BeginDrawing();
            Raylib.ClearBackground(Color.DarkPurple);
            Raylib.DrawTexturePro(target.Texture, new Rectangle(0, 0, Settings.SHX, -Settings.SHY), new Rectangle(0, 0, Settings.SHX * Settings.SCALE, Settings.SHY * Settings.SCALE), new Vector2(0, 0), 0.0f, Color.White);
            //DrawRisingEmoji(a, emojiTexture);
            DrawObjectNames(a.state, a.game.agentTemplate, a.camera.RLCamera(), tamzen);
            if (Settings.DRAW_FIELD) DrawField(a.state.field, a.state.VMX, a.game.size, a.camera.RLCamera(), tamzen);

            if (a.menu) DrawMenu(a, jetBrainsMono, Settings.SHX, Settings.SHY);
            DrawScore(a, jetBrainsMono, PLAYER);
            //Raylib.DrawFPS(Settings.SHX - 100, 10);

            Raylib.EndDrawing();
        }

        Raylib.UnloadFont(tamzen);
        Raylib.UnloadFont(jetBrainsMono);
        a.shader.Unload();
        Raylib.CloseWindow();
    }

    static void DrawMenu(AppState a, Font font, int SW, int SH)
    {
        static float topy(int i, Font font) => (i + 4.5f) * font.BaseSize;
        //static void printCenter(string s, float y, Font font, Color color)
        //{
        //    float swidth = Raylib.MeasureTextEx(font, s, font.BaseSize, 0).X;
        //    Vector2 p = new((SW - swidth) / 2, y);
        //    Raylib.DrawTextEx(font, s, p, font.BaseSize, 0, color);
        //};

        Raylib.DrawRectangle(0, 0, SW, SH, new Color(0, 0, 0, 0x88)); ///shade the game graphics
        if (a.gameover) RaylibHelper.PrintCenter("~~~ GAME OVER ~~~", 3f * font.BaseSize, font, Color.Red, SW);

        for (int i = 0; i < a.xgames.Length; i++)
        {
            string gamename = a.xgames[i].children[0].head;
            RaylibHelper.PrintCenter(gamename, topy(i, font), font, Color.Beige, SW);
        }

        const int RW = 500, RH = 48;
        Rectangle selection = new((SW - RW) / 2, topy(a.menuPosition, font), RW, RH);
        Raylib.DrawRectangleLinesEx(selection, 2f, Color.Yellow);

        const float L = 20.5f;
        RaylibHelper.PrintCenter("Arrow Keys - Move", L * font.BaseSize, font, Color.Gray, SW);
        RaylibHelper.PrintCenter("Z - Use", (L + 1f) * font.BaseSize, font, Color.Gray, SW);
        RaylibHelper.PrintCenter("TAB - Toggle AI", (L + 2f) * font.BaseSize, font, Color.Gray, SW);
        RaylibHelper.PrintCenter("Enter - Menu", (L + 3f) * font.BaseSize, font, Color.Gray, SW);
    }

    static void DrawDirs(AppState a)
    {
        for (int i = 0; i < a.state.maxObjectCount; i++)
        {
            Object o = a.state.array[i];
            if (o == null || o.template.epolicy == null) continue;

            Vector3 start = o.body.FeetPosition();
            Vector3 finish = start + new Vector3(o.dir, 0f);
            Raylib.DrawCylinderEx(start, finish, 0.02f, 0.02f, 5, Color.Green);
        }
    }

    static void DrawSensors(AppState a)
    {
        //всё равно можно сделать - просто имена сенсоров нужно достать из темплейта
        /*for (int i = 0; i < a.state.objects.Count; i++)
        {
            Object o = a.state.objects[i];
            int vizCounter = 0;
            for (int s = 0; s < o.template.sensors.Count; s++)
            {
                string name = o.template.sensors[s].name;
                if (o.env.Has(name, out Value value))
                {
                    if (value.type == TYPE.VECTOR)
                    {
                        Vector3 start = o.body.FeetPosition();
                        Vector3 finish = start + new Vector3(value.v, 0f);
                        const float width = 0.01f;
                        Raylib.DrawCylinderEx(start, finish, width, width, 5, vizCounter == 0 ? Color.Red : Color.Blue);
                        vizCounter++;
                    }
                }
            }
        }*/
    }

    static void DrawScore(AppState a, Font font, bool PLAYER)
    {
        string scoreString = $"{a.game.templates[a.game.agentTemplate].name} score = {a.state.score:0.0}";

        RaylibHelper.PrintCenter($"\"{a.game.name}\"", 1f * font.BaseSize, font, Color.DarkBlue, Settings.SHX);
        RaylibHelper.PrintCenter(scoreString, 2f * font.BaseSize, font, Color.DarkBlue, Settings.SHX);
        RaylibHelper.PrintCenter(PLAYER ? "AI OFF" : "AI ON", 3f * font.BaseSize, font, PLAYER ? Color.Red : Color.Green, Settings.SHX);
        if (a.paused) RaylibHelper.PrintCenter("PAUSED", 4f * font.BaseSize, font, Color.Red, Settings.SHX);
    }

    static void DrawObjectNames(State state, int agentID, Camera3D camera, Font font)
    {
        Color topColor = Color.Gray;
        if (agentID >= 0)
        {
            Template agent = state.game.templates[agentID];
            if (agent.esub != null) Raylib.DrawTextEx(font, agent.esub.ToString(agent.storageNames, state.game.templates), Vector2.Zero, font.BaseSize, 0, topColor);
        }
        Raylib.DrawTextEx(font, $"frame = {state.currentFrame}, fps = {Raylib.GetFPS()}", new Vector2(0, font.BaseSize), font.BaseSize, 0, topColor);
        if (!Settings.DRAW_NAMES) return;

        for (int i = 0; i < state.maxObjectCount; i++)
        {
            Object o = state.array[i];
            if (o == null) continue;

            Vector2 screen = Raylib.GetWorldToScreenEx(o.body.position, camera, Settings.SHX * Settings.SCALE, Settings.SHY * Settings.SCALE);
            float width = Raylib.MeasureTextEx(font, o.name, font.BaseSize, 0).X;
            float x = screen.X - width / 2f;
            float y = screen.Y - font.BaseSize / 2f - 1f;
            const int R = 120;
            Raylib.DrawRectangle((int)(x + 0.5f), (int)(y + 0.5f), (int)width, font.BaseSize - 1, new Color(R, R, R, 80));
            Raylib.DrawTextEx(font, o.name, new Vector2((int)(x + 0.5f), (int)(y + 0.5f)), font.BaseSize, 0, Color.White);
        }
    }

    static void DrawField(int[] field, int MX, Vector2 sceneSize, Camera3D camera, Font font)
    {
        for (int i = 0; i < field.Length; i++)
        {
            int x = i % MX, y = i / MX;
            Vector3 p = new(x + 0.5f - 0.5f * sceneSize.X, y + 0.5f - 0.5f * sceneSize.Y, 0f);
            Vector2 screen = Raylib.GetWorldToScreenEx(p, camera, Settings.SHX * Settings.SCALE, Settings.SHY * Settings.SCALE);

            string text = field[i].ToString();
            float width = Raylib.MeasureTextEx(font, text, font.BaseSize, 0).X;
            Vector2 position = new((int)((int)screen.X - width / 2f + 0.5f), (int)((int)screen.Y - font.BaseSize / 2f + 0.5f));
            Raylib.DrawTextEx(font, text, position, font.BaseSize, 0, Color.Red);
        }
    }
}

class AppState
{
    public Game game;
    public State state;
    public bool menu, paused, gameover;
    public byte menuPosition;
    public Camera camera;
    public MyShader shader;
    public int cliFramesRemaining;

    public Expr[] xgames;

    public AppState(Expr[] xgames)
    {
        this.xgames = xgames;
        menu = false;
        paused = true;
    }

    public bool LoadGame()
    {
        Expr xgame = xgames[menuPosition];
        string gamename = xgame.children[0].head;
        int steps = xgame.GetInt("steps", 3600);

        game = Game.LoadFromFile(gamename).game;
        if (game.error != null)
        {
            Console.WriteLine($"ERROR: could not load game because of <{game.error}>");
            return false;
        }

        uint seed = Settings.RANDOMIZE ? (uint)new Random().Next(1000) : Settings.SEED;
        state = new(game, seed);
        cliFramesRemaining = 0;
        
        string cameraString = xgame.GetString("camera", "perspective");
        Camera.CAMERA cameraType = cameraString switch
        {
            "perspective" => Camera.CAMERA.PERSPECTIVE,
            "isometric" => Camera.CAMERA.ISOMETRIC,
            "topdown" => Camera.CAMERA.TOPDOWN,
            "following" => Camera.CAMERA.FOLLOWING,
            "firstPerson" => Camera.CAMERA.FIRSTPERSON,
            _ => throw new Exception($"unknown camera type <{cameraString}>")
        };
        camera = new Camera(cameraType, game.size);

        if (shader == null) shader = new("resources/shader.c", Settings.SHX, Settings.SHY); ///raylib should be initialized before this
        shader.Update(state, camera);
        return true;
    }

    public void MenuDown()
    {
        menuPosition++;
        if (menuPosition >= xgames.Length) menuPosition = 0;
    }
    public void MenuUp()
    {
        if (menuPosition == 0) menuPosition = (byte)(xgames.Length - 1);
        else menuPosition--;
    }
}

/*
М.б. сделаем майнинг? Очень заманчиво, потому что политика будет несколько длиннее. Для успешного майнинга нужно и майнить, и относить замайненное на базу, и при этом избегать монстров.
При этом нужно сделать так, чтобы анимация майнинга занимала некоторое время, без этого никуда.
Это просто: когда игрок нажимает Q и копание срабатывет, на игрока кладётся стан-каунтер.

Mario - отлично, сделать. Разница будет в том, что нужно не только бросать в монстров, но и избегать монстров. Политика получится банально длиннее.
BoomArena - npc кидают друг в друга взрывающиеся штуки. Нужно комбинировать кидание и увёртывание.
StarWars - прямо как я раньше делал. Тут нужно реализовать стрельбу, которая не является бросанием. Разница в том, что прожектили не остаются на карте, а самоуничтожаются, и их не нужно заново подбирать. Это делает политику проще, чем в Марио.

Ghost gets rewards for collecting cheese, but it also needs to hide from citizens walking around.
Было бы ещё интересно, если бы именно люди спаунили этот сыр.

Нужно ещё сделать корабль, который стреляет пушками с боков. Ему должны помогать соответствующие сенсоры.
Интересно будет сравнить найденные политики для прямого стрелка и для бокового стрелка.

Shooter - интересно автоматически найти политику стреляющего робота.

Remembering locations is an interesting idea, because it's easy to do in Lua.
Remembering a current location. Потом можно будет вертеться вокруг этой локации. Ещё можно оставить в локации бусинку, и потом вертеться вокруг бусинки.
The ability to spawn marbles and remember a location of any one marble. Интересно, если какой-то паззл удастся решить гораздо лучше с помощью спаунинга марблов.
И ещё прикольно в хх, когда робот расставляет марблы, а игроки путают этого робота, перенося marbles.

Сделать точечный синтаксис для одиночных скобочек: (x (max table)) -> table.max.x - спросить у чата, какие конвенции есть в этом вопросе.

Хм, вообще-то это возможно: если атом касается одного или двух атомов в конце игры, за это даётся ревард. А если больше двух, то не даётся. Это будет нетривильно выразить в языке, но можно попробовать.
(onCollision X Y (if (Atom X) (Atom Y) X.Count++ Y.Count++))
(onCollisionEnd X Y ...)
Ещё новизна будет в том, что мы никогда не делали физический пуш. Но его нужно сделать! Он даже в protect the child очень пригодится.
Я даже боюсь представить, как будет работать пуш многих объектов, когда один объект пушится из-за другого.
Хотя наверняка эти физические движки работают по простому принципу.
Просто некоторые объекты будут вкопаны в землю, а некоторые нет. У бульдозера можно кастомно увеличить массу, чтобы он мог двигать стены.

Я только сейчас понял, что в отличие от машины, приведение не будет убегать на бесконечность! В этом принципиальное отличие поиска максимального расстояния.
NEXT: Было бы особенно круто, если бы он игнорировал гапы, где луч пересекается с препятствием слишком по касательной.
К гапу нормали идти бессмысленно, потому что так мы просто будет приходить в угол.
Но если мы будем штрафовать за слишком касающиеся лучи, то может получиться заманчиво.
Довольно много идей моих NPC используют сэмплинг точек - возможно, стоит добавить.

NEXT: I can actually make a lot of patterns like (== button cheese) and (dir cheese) automatic. About the latter, some artifacts are just directed, and some are not.
(== none button) <- вот это будет сложнее. Мы можем смотреть на наличие (destroy ..). Но проблема в том, что, хотя у нас есть информация о типах, у нас нет информации о конкретных темплейтах. Compute templates at compile time? Хм, это может работать!
(template (closest (isCreature #0))) <- sometimes template cannot be computed at compile time. But then we can just keep it undetermined.

Load.Root.Elements.ToArray ~ (ToArray (Elements (Root Load)))
sheep.x.square ~ (square (x sheep))
(sin x) ~ x.sin
Отлично, т.е. это стандартная нотация!

---

Не забыть ещё, что мы хотим вариант с расстановокй маячков.
Главное: политика должна получиться полезной.
С этими простыми сенсорами политика получается слишком реактивной просто. Вся стратегия основана на рейдисте. Поэтому я и хотел подумать о сенсорах.
Есть ещё такая идея, что робот может мерить расстояние до ближайшей травы. Но это не похоже на настоящую газонокосилку.

Мы ещё можем штрафовать за столкновения со стенами. Это гонки по сути.
Но сейчас у меня нет хороших элементарных функций для этого.

More games:
* Reward for keeping far from the target, but seeing the target.
* Lava - very important to add. Но проще в текущих играх давать большой антиревард за столкновения со стенами.
* Driving - штрафуются столкновения с объектами.
* (!) Газонокосильщик, который может расставлять маячки! Эти маячки могут означать, какие участки карты он пропылесосил, например. Чтобы это работало, карта должна быть большая, чтобы там были свои регионы. Но уж очень мне хочется попробовать маячки. Подозреваю, что в Karel маячки тоже где-то есть.
* Стреляющие npc и бомберы ждут нас.
* Ещё пример коллаборативного задания: Seek, но цель ищут несколько роботов. Будет здорово показать, что если разрешить роботам смотреть на других роботов, то они будут находить цели быстрее.
* А представь, сколько интересных командных взаимодействий может быть в шутерах - там можно прятаться за препятствиями, например, или фланковать.
* Пытаемся убежать от кошек, если известно только расстояние до ближайшей кошки.
* Shooter - если нужна последняя игра. Но тут возникает проблема projectiles, я не хочу делать новый концепт.

TeamBall - точно, это будет выглядеть довольно прикольно. Причём в лабиринте. Большая команда на команду. Нужно постараться сделать, чтобы летящие предметы отскакивали от вокселей.
Побеждённый игрок полностью исчезает? Тогда оптимальной стратегией может быть просто кинуть мяч раньше. Либо ждать, когда другой игрок к тебе подойдёт.
(!) Точно: ревард даётся за нахождение в определённой зоне. При этом можно убивать игроков противоположной команды с помощью мячей.
Блин, у хх была прикольная идея с вайбкодингом таких развлекаловок.
Тогда ИИ-боты могут временно заменять людей для непопулярных стримеров. И миниигры могут быть даже очень интересны, если они социальные.
Product: how to properly make HH-4.1. И можно типа брать свои префабы в миниигры. И система вокселей во всех миниграх одна и та же. И стримить было довольно весело, и дети хотели это делать.

*/
