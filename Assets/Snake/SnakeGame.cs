using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 3D 贪吃蛇：
///   · 地图可选 3×3×3 / 5×5×5 / 7×7×7，出生在地图正中
///   · 食物：3³=1 个，5³=2 个，7³=3 个
///   · 目标分数 = 总格数的 50%
///   · 逻辑离散、视觉连续；身体铺满路径；拐角补块
///   · A/D 左右转，W/S 持续升降，Shift 加速；撞墙给宽限期
/// </summary>
public class SnakeGame : MonoBehaviour
{
    public enum GameState { Menu, Countdown, Playing, GameOver }

    [Header("网格")]
    public float countdownSeconds = 3f;
    public float wallGraceFactor = 1f;

    [Header("外观")]
    public Color headColor = new Color(0.52f, 0.90f, 0.62f);
    public Color bodyColor = new Color(0.30f, 0.66f, 0.88f);
    public Color foodColor = new Color(1.00f, 0.84f, 0.18f);
    public float segmentScale = 1.0f;

    [Header("光影")]
    public bool setupLighting = true;

    // ---- 运行时状态 ----
    public GameState state { get; private set; } = GameState.Menu;
    public float countdownLeft { get; private set; }
    public int score { get; private set; }
    public int gridSize { get; private set; } = 5;
    public int targetScore { get; private set; } = 62;
    public int foodCount { get; private set; } = 2;
    public float MoveInterval { get; set; } = 0.5f;
    [Range(0.3f, 1f)] public float boostFactor = 0.45f;
    public bool blockedByWall { get; private set; }
    public bool paused { get; private set; }
    public bool targetReached => score >= targetScore;
    public float runSeconds { get; private set; }
    public bool won { get; private set; }

    public Vector3 HeadWorldPosition => views.Count > 0 ? views[0].transform.position : transform.position;

    public Vector3 HeadForward
    {
        get
        {
            var d = new Vector3(dir.x, 0f, dir.z);
            return d.sqrMagnitude < 0.01f ? Vector3.right : d.normalized;
        }
    }

    // ---- 蛇 ----
    private readonly List<Vector3Int> cells = new List<Vector3Int>();
    private readonly List<Vector3Int> prevCells = new List<Vector3Int>();
    private readonly List<GameObject> views = new List<GameObject>();
    private readonly List<GameObject> pathFillers = new List<GameObject>();
    private readonly List<GameObject> outlineViews = new List<GameObject>();   // 描边壳（外扩）
    private readonly List<GameObject> maskViews = new List<GameObject>();      // 模板掩码（与身体同形）
    private readonly List<GameObject> fillerMasks = new List<GameObject>();
    private readonly List<GameObject> fillerHulls = new List<GameObject>();
    private Material outlineMat;
    private Material maskMat;
    private readonly Queue<Vector3Int> turnQueue = new Queue<Vector3Int>();

    private Vector3Int dir = Vector3Int.right;
    private int verticalMode = 0;
    private Vector3Int faceDir = Vector3Int.right;

    // ---- 食物 ----
    private readonly List<Vector3Int> foodCells = new List<Vector3Int>();
    private readonly List<GameObject> foodViews = new List<GameObject>();

    // ---- 计时 ----
    private float timer;          // 当前格的进度 0~1
    private float blockTimer;

    // ---- 场景 ----
    private Transform boardRoot, snakeRoot;
    private Material headMat, bodyMat, foodMat;
    private Material headMatBoost, bodyMatBoost;
    private Material bodyMatGhost, bodyMatBoostGhost;
    private GameObject boundaryRoot;
    private GameObject skySphere;
    private Transform menuDecorRoot;
    private readonly List<Transform> decorItems = new List<Transform>();
    private readonly List<float> decorSpeeds = new List<float>();
    private readonly List<float> decorPhases = new List<float>();
    private readonly List<float> decorBaseY = new List<float>();
    private readonly List<GameObject> layerBlocks = new List<GameObject>();   // 蛇头所在层的格子高亮
    private int lastLayer = -999;
    private readonly List<LineRenderer> layerEdges = new List<LineRenderer>();   // 层的外框
    private Light headLight;
    private Transform eyesRoot;
    private Material eyeWhiteMat, eyePupilMat;
    private Material boundaryMat;

    private bool isBoosting;
    private bool showLayerHighlight = true;   // L 键切换
    private bool wasBoosting;
    private int initialLength = 3;      // 本局初始长度
    private bool bodyRevealed = false;  // 倒计时期间只显示头
    private string blockReason = "撞墙";

    // ---- 菜单自动演示 ----
    private bool demoActive;
    private float demoTimer;
    private float demoInterval = 0.42f;
    private Vector3Int demoDir = Vector3Int.right;
    public bool DemoActive => demoActive;
    private bool bodyOccluding;

    // ---- 特效 ----
    private readonly List<Transform> fxItems = new List<Transform>();
    private readonly List<Vector3> fxVel = new List<Vector3>();
    private readonly List<float> fxLife = new List<float>();
    private readonly List<float> fxMaxLife = new List<float>();
    private readonly List<TextMesh> fxTexts = new List<TextMesh>();
    private readonly List<Vector3> fxBaseScale = new List<Vector3>();
    private Material fxMat;
    private Material fxTrailMat;

    public event System.Action OnEat;
    public event System.Action OnBumped;          // 撞墙/撞身体
    public event System.Action OnCoinEaten;       // 吃到金币
    public bool IsBoosting => isBoosting;
    public event System.Action<string> OnGameOver;

    // ---------------- 工具 ----------------
    static Vector3Int Cross(Vector3Int a, Vector3Int b) => new Vector3Int(
        a.y * b.z - a.z * b.y,
        a.z * b.x - a.x * b.z,
        a.x * b.y - a.y * b.x);

    Vector3 CellToWorld(Vector3Int c)
    {
        float half = (gridSize - 1) * 0.5f;
        return new Vector3(c.x - half, c.y - half, c.z - half);
    }

    bool InBounds(Vector3Int c) =>
        c.x >= 0 && c.x < gridSize && c.y >= 0 && c.y < gridSize && c.z >= 0 && c.z < gridSize;

    Vector3Int StepDir() =>
        verticalMode > 0 ? Vector3Int.up :
        verticalMode < 0 ? Vector3Int.down : dir;

    bool CanMove(Vector3Int from, Vector3Int step)
    {
        Vector3Int n = from + step;
        return InBounds(n) && !SelfHit(n);
    }

    static int FoodCountForSize(int size) => size <= 3 ? 1 : size <= 5 ? 2 : 3;
    static int TargetForSize(int size) => Mathf.RoundToInt(size * size * size * GameSettings.DifficultyPercent);

    static Material MakeMaterial(Color c, float smoothness = 0.55f, float metallic = 0.05f, Color? emission = null)
    {
        var sh = Shader.Find("Standard");
        if (sh == null) sh = Shader.Find("Universal Render Pipeline/Lit");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        var m = new Material(sh);
        m.color = c;
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
        if (emission.HasValue && m.HasProperty("_EmissionColor"))
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission.Value * 0.6f);
        }
        return m;
    }

    /// <summary>纯色无光照材质：块与块之间没有明暗差异，整条蛇看起来是一个整体。</summary>
    static Material MakeFlat(Color c)
    {
        // 世界法线着色：块与块之间不会出现明暗断层，同时有体积感
        var sh = Shader.Find("Snake/SoftShade");
        if (sh == null)
        {
            Debug.LogError("[材质] 找不到 Snake/SoftShade 着色器，退回 Unlit/Color");
            sh = Shader.Find("Unlit/Color");
        }

        var m = new Material(sh);
        if (m.HasProperty("_Color")) m.color = c;
        Debug.Log("[材质] 蛇身使用：" + (sh != null ? sh.name : "null"));
        return m;
    }

    static Material MakeTransparent(Color c, float alpha)
    {
        var m = MakeMaterial(c, 0.4f, 0f);
        var col = c; col.a = alpha;
        m.color = col;

        if (m.HasProperty("_Mode"))
        {
            m.SetFloat("_Mode", 2f);
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.DisableKeyword("_ALPHATEST_ON");
            m.EnableKeyword("_ALPHABLEND_ON");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
        return m;
    }

    // ---------------- 生命周期 ----------------
    void Awake()
    {
        MoveInterval = GameSettings.MoveInterval;
        if (wallGraceFactor <= 0.01f) wallGraceFactor = 1f;
        gridSize = GameSettings.MapSize;
    }

    void Start()
    {
        if (setupLighting) SetupLighting();

        // 读取玩家自定义颜色
        headColor = GameSettings.HeadColor;
        bodyColor = GameSettings.BodyColor;

        // 纯色无光照：整条蛇是一块整色，块之间不会有明暗缝
        headMat = MakeFlat(headColor);
        bodyMat = MakeFlat(bodyColor);
        foodMat = MakeMaterial(foodColor, 0.8f, 0.2f, foodColor);
        foodMat.EnableKeyword("_EMISSION");
        headMatBoost = MakeFlat(Color.Lerp(headColor, Color.white, 0.28f));
        bodyMatBoost = MakeFlat(Color.Lerp(bodyColor, Color.white, 0.28f));
        bodyMatGhost = MakeTransparent(bodyColor, 0.28f);
        bodyMatBoostGhost = MakeTransparent(Color.Lerp(bodyColor, Color.white, 0.22f), 0.32f);
        boundaryMat = MakeMaterial(new Color(0.65f, 0.92f, 1f, 0.95f), 0.3f, 0f);
        fxMat = MakeFlat(foodColor);
        fxTrailMat = new Material(Shader.Find("Sprites/Default"));

        // 描边：模板掩码 + 模板描边
        var maskShader = Shader.Find("Snake/StencilMask");
        if (maskShader != null) maskMat = new Material(maskShader);

        var outlineShader = Shader.Find("Snake/OutlineStencil");
        if (outlineShader != null)
        {
            outlineMat = new Material(outlineShader);
            outlineMat.SetColor("_Color", GameSettings.OutlineColor);
        }

        BuildWorld();
    }

    void Update()
    {
        UpdateFx();

        switch (state)
        {
            case GameState.Countdown:
                countdownLeft -= Time.deltaTime;
                if (countdownLeft <= 0f)
                {
                    timer = 0f;
                    bodyRevealed = true;   // 开始后才展开身体
                    state = GameState.Playing;
                }
                break;

            case GameState.Menu:
                if (demoActive) UpdateDemo();
                break;

            case GameState.Playing:
                if (Input.GetKeyDown(KeyCode.L))
                {
                    showLayerHighlight = !showLayerHighlight;
                    foreach (var b in layerBlocks) if (b != null) b.SetActive(showLayerHighlight);
                    foreach (var e in layerEdges) if (e != null) e.gameObject.SetActive(showLayerHighlight);
                }
                if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P))
                {
                    paused = !paused;
                    return;
                }
                if (paused) return;
                UpdatePlaying();
                break;

            case GameState.GameOver:
                if (Input.GetKeyDown(KeyCode.R)) StartGame();
                break;
        }
    }

    void UpdatePlaying()
    {
        ReadInput();


        isBoosting = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        if (isBoosting != wasBoosting)
        {
            wasBoosting = isBoosting;
            if (AudioManager.Instance != null)
            {
                if (isBoosting) AudioManager.Instance.PlayBoostIn();
                else AudioManager.Instance.PlayBoostOut();
            }
        }

        float interval = MoveInterval;
        if (isBoosting) interval = Mathf.Max(0.05f, MoveInterval * boostFactor);

        if (blockedByWall)
        {
            blockTimer += Time.deltaTime;
            float grace = interval * Mathf.Max(1f, wallGraceFactor);
            if (blockTimer >= grace) { GameOver(blockReason); return; }
            ApplyVisuals(1f);
            return;
        }

        runSeconds += Time.deltaTime;
        timer += Time.deltaTime / interval;

        if (timer >= 1f)
        {
            timer -= 1f;
            if (timer > 1f) timer = 1f;
            CommitStep();
        }

        ApplyVisuals(Mathf.Clamp01(timer));
    }

    // ---------------- 菜单演示 ----------------
    void UpdateDemo()
    {
        demoTimer += Time.deltaTime;
        if (demoTimer >= demoInterval)
        {
            demoTimer -= demoInterval;
            if (demoTimer > demoInterval) demoTimer = demoInterval;
            DemoStep();
        }

        ApplyVisuals(Mathf.Clamp01(demoTimer / demoInterval));
    }

    void DemoStep()
    {
        if (cells.Count == 0) { StartDemo(); return; }

        Vector3Int head = cells[0];
        Vector3Int best = demoDir;
        float bestScore = float.MaxValue;
        bool found = false;

        Vector3Int[] candidates =
        {
            Vector3Int.right, Vector3Int.left, Vector3Int.forward, Vector3Int.back
        };

        foreach (var d in candidates)
        {
            if (d == -demoDir) continue;
            Vector3Int n = head + d;
            if (!InBounds(n)) continue;
            if (SelfHit(n)) continue;

            float dist = 9999f;
            for (int i = 0; i < foodCells.Count; i++)
                dist = Mathf.Min(dist, (n - foodCells[i]).sqrMagnitude);

            // 靠近食物优先，但别贴墙（贴墙扣分）
            float score = dist;
            if (n.x == 0 || n.x == gridSize - 1) score += 3f;
            if (n.z == 0 || n.z == gridSize - 1) score += 3f;

            if (score < bestScore) { bestScore = score; best = d; found = true; }
        }

        if (!found) { StartDemo(); return; }   // 卡死就重开一段

        demoDir = best;
        Vector3Int next = head + best;

        bool eating = foodCells.Contains(next);
        var old = new List<Vector3Int>(cells);

        cells.Insert(0, next);
        if (eating)
        {
            score++;
            SpawnCoinBurst(CellToWorld(next));
            SpawnScorePopup(CellToWorld(next), "1");
            if (AudioManager.Instance != null) AudioManager.Instance.PlayEat();
            foodCells.Remove(next);
            SpawnExtraFood();
            RebuildViews();
        }
        else
        {
            cells.RemoveAt(cells.Count - 1);
        }

        prevCells.Clear();
        for (int i = 0; i < cells.Count; i++)
            prevCells.Add(i < old.Count ? old[i] : cells[i]);

        score = 0;   // 演示不计分
    }

    // ---------------- 对外接口 ----------------
    public void StartGame()
    {
        score = 0;
        timer = 0f;
        blockTimer = 0f;
        blockedByWall = false;
        paused = false;
        isBoosting = false;
        won = false;
        wasBoosting = false;
        runSeconds = 0f;
        dir = Vector3Int.right;
        verticalMode = 0;
        faceDir = Vector3Int.right;

        gridSize = GameSettings.MapSize;
        targetScore = TargetForSize(gridSize);
        foodCount = FoodCountForSize(gridSize);
        initialLength = Mathf.Clamp(GameSettings.InitialLength, 1, gridSize);
        bodyRevealed = false;

        StopDemo();
        if (menuDecorRoot != null) menuDecorRoot.gameObject.SetActive(false);
        BuildWorld();          // 清空重建：边界 / 蛇 / 食物
        ResetSnake();
        RebuildViews();
        RefreshFoods();
        ApplyVisuals(1f);

        if (snakeRoot != null) snakeRoot.gameObject.SetActive(true);

        countdownLeft = countdownSeconds;
        state = GameState.Countdown;
    }

    /// <summary>应用设置里的自定义颜色（运行时可调用）。</summary>
    public void ApplyCustomColors()
    {
        headColor = GameSettings.HeadColor;
        bodyColor = GameSettings.BodyColor;

        if (headMat != null && headMat.HasProperty("_Color")) headMat.color = headColor;
        if (bodyMat != null && bodyMat.HasProperty("_Color")) bodyMat.color = bodyColor;
        if (headMatBoost != null && headMatBoost.HasProperty("_Color"))
            headMatBoost.color = Color.Lerp(headColor, Color.white, 0.28f);
        if (bodyMatBoost != null && bodyMatBoost.HasProperty("_Color"))
            bodyMatBoost.color = Color.Lerp(bodyColor, Color.white, 0.28f);
        if (outlineMat != null) outlineMat.SetColor("_Color", GameSettings.OutlineColor);
    }

    public void TogglePause()
    {
        if (state != GameState.Playing) return;
        paused = !paused;
    }

    public void StartDemo()
    {
        state = GameState.Menu;
        paused = false;
        demoActive = true;
        bodyRevealed = true;

        BuildWorld();
        cells.Clear();
        prevCells.Clear();
        turnQueue.Clear();

        int mid = gridSize / 2;
        cells.Add(new Vector3Int(mid, mid, mid));
        for (int i = 0; i < cells.Count; i++) prevCells.Add(cells[i]);

        demoDir = Vector3Int.right;
        RebuildViews();
        RefreshFoods();
        ApplyVisuals(1f);
        if (snakeRoot != null) snakeRoot.gameObject.SetActive(true);
        demoTimer = 0f;
    }

    public void StopDemo()
    {
        demoActive = false;
        if (snakeRoot != null) snakeRoot.gameObject.SetActive(false);
        foreach (var f in foodViews) if (f != null) f.SetActive(false);
    }

    public void ToMenu()
    {
        state = GameState.Menu;
        paused = false;
        if (snakeRoot != null) snakeRoot.gameObject.SetActive(false);
        foreach (var f in foodViews) if (f != null) f.SetActive(false);
        if (menuDecorRoot != null) menuDecorRoot.gameObject.SetActive(true);
        if (boundaryRoot != null) boundaryRoot.SetActive(false);
        foreach (var b in layerBlocks) if (b != null) b.SetActive(false);
        foreach (var e in layerEdges) if (e != null) e.gameObject.SetActive(false);
    }

    // ---------------- 输入 ----------------
    void ReadInput()
    {
        if (Input.GetKeyDown(KeyCode.D)) QueueTurn(+1);
        else if (Input.GetKeyDown(KeyCode.A)) QueueTurn(-1);
        else if (Input.GetKeyDown(KeyCode.W))
        {
            verticalMode = verticalMode < 0 ? 0 : 1;
            RefreshBlocked();
        }
        else if (Input.GetKeyDown(KeyCode.S))
        {
            verticalMode = verticalMode > 0 ? 0 : -1;
            RefreshBlocked();
        }
    }

    void QueueTurn(int sign)
    {
        verticalMode = 0;

        Vector3Int basis = dir;
        if (turnQueue.Count > 0) basis = turnQueue.ToArray()[turnQueue.Count - 1];

        Vector3Int right = Cross(Vector3Int.up, basis);
        Vector3Int newDir = sign > 0 ? right : -right;

        if (newDir == -basis) return;
        if (turnQueue.Count >= 2) return;

        turnQueue.Enqueue(newDir);
        RefreshBlocked();
    }

    void RefreshBlocked()
    {
        if (!blockedByWall) return;

        Vector3Int d = dir;
        if (turnQueue.Count > 0) d = turnQueue.Peek();

        Vector3Int step = verticalMode > 0 ? Vector3Int.up
                        : verticalMode < 0 ? Vector3Int.down
                        : d;

        if (CanMove(cells[0], step))
        {
            blockedByWall = false;
            blockTimer = 0f;
        }
        else blockTimer = 0f;
    }

    // ---------------- 提交一步 ----------------
    void CommitStep()
    {
        if (turnQueue.Count > 0)
        {
            var nd = turnQueue.Dequeue();
            if (nd != -dir) dir = nd;
        }

        Vector3Int step = StepDir();
        Vector3Int next = cells[0] + step;

        if (!InBounds(next))
        {
            blockedByWall = true;
            blockReason = "撞墙";
            OnBumped?.Invoke();
            if (AudioManager.Instance != null) AudioManager.Instance.PlayBump();
            blockTimer = 0f;
            FreezeVisuals();
            return;
        }

        if (SelfHit(next))
        {
            blockedByWall = true;
            blockReason = "咬到自己";
            OnBumped?.Invoke();
            if (AudioManager.Instance != null) AudioManager.Instance.PlayBump();
            blockTimer = 0f;
            FreezeVisuals();
            return;
        }

        faceDir = step;

        bool eating = foodCells.Contains(next);
        var old = new List<Vector3Int>(cells);

        cells.Insert(0, next);

        bool needsGrowth = cells.Count <= initialLength && initialLength > 1;
        if (eating)
        {
            score++;
            SpawnCoinBurst(CellToWorld(next));
            SpawnScorePopup(CellToWorld(next), "+1");
            OnCoinEaten?.Invoke();
            if (AudioManager.Instance != null) AudioManager.Instance.PlayEat();
            OnEat?.Invoke();
            foodCells.Remove(next);
            SpawnExtraFood();
        }

        if (eating || needsGrowth)
        {
            // 吃食物 或 还没长到初始长度 → 保留尾巴，身体多一节
            RebuildViews();
        }
        else
        {
            cells.RemoveAt(cells.Count - 1);
        }

        prevCells.Clear();
        for (int i = 0; i < cells.Count; i++)
            prevCells.Add(i < old.Count ? old[i] : cells[i]);

        blockedByWall = false;
        blockTimer = 0f;

        if (targetReached)
        {
            won = true;
            GameOver("达成目标");
        }
    }

    bool SelfHit(Vector3Int cell)
    {
        for (int i = 0; i < cells.Count - 1; i++)
            if (cells[i] == cell) return true;
        return false;
    }

    void FreezeVisuals()
    {
        prevCells.Clear();
        for (int i = 0; i < cells.Count; i++) prevCells.Add(cells[i]);
    }

    // ---------------- 特效 ----------------
    void SpawnCoinBurst(Vector3 pos)
    {
        for (int i = 0; i < 14; i++)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Fx";
            go.transform.SetParent(transform, false);
            Destroy(go.GetComponent<Collider>());
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * Random.Range(0.16f, 0.30f);   // 碎片更大一点，更明显

            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = fxMat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // 拖尾
            var trail = go.AddComponent<TrailRenderer>();
            trail.material = fxTrailMat;
            trail.time = 0.22f;
            trail.startWidth = 0.16f;
            trail.endWidth = 0f;
            trail.numCapVertices = 2;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(foodColor, 0f), new GradientColorKey(foodColor, 1f) },
                new[] { new GradientAlphaKey(0.85f, 0f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = grad;

            Vector3 dir = new Vector3(Random.Range(-1f, 1f), Random.Range(0.2f, 1.2f), Random.Range(-1f, 1f)).normalized;
            fxItems.Add(go.transform);
            fxVel.Add(dir * Random.Range(3.2f, 6.5f));
            fxLife.Add(0f);
            fxMaxLife.Add(Random.Range(1.0f, 2.0f));   // 存活 1~2 秒
            fxTexts.Add(null);
            fxBaseScale.Add(go.transform.localScale);
        }
    }

    void SpawnScorePopup(Vector3 pos, string text)
    {
        var go = new GameObject("FxText");
        go.transform.SetParent(transform, false);
        go.transform.position = pos + Vector3.up * 0.55f;

        var tm = go.AddComponent<TextMesh>();
        tm.text = text;
        tm.fontSize = 72;
        tm.characterSize = 0.09f;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.color = new Color(1f, 0.95f, 0.45f);

        fxItems.Add(go.transform);
        fxVel.Add(new Vector3(0f, 1.6f, 0f));
        fxLife.Add(0f);
        fxMaxLife.Add(1.1f);
        fxTexts.Add(tm);
        fxBaseScale.Add(Vector3.one);
    }

    void UpdateFx()
    {
        for (int i = fxItems.Count - 1; i >= 0; i--)
        {
            var t = fxItems[i];
            if (t == null) { RemoveFx(i); continue; }

            fxLife[i] += Time.deltaTime;
            if (fxLife[i] >= fxMaxLife[i]) { Destroy(t.gameObject); RemoveFx(i); continue; }

            fxVel[i] += Vector3.down * 18f * Time.deltaTime;   // 重力加大，碎片落得更快
            t.position += fxVel[i] * Time.deltaTime;

            float k = fxLife[i] / fxMaxLife[i];
            if (fxTexts[i] != null)
            {
                var c = fxTexts[i].color;
                c.a = 1f - k;
                fxTexts[i].color = c;
                if (Camera.main != null)
                    t.rotation = Camera.main.transform.rotation;
            }
            else
            {
                // 按寿命比例缩小，1~2 秒内平滑消失
                float shrink = Mathf.Clamp01(1f - k * 0.85f);
                t.localScale = fxBaseScale[i] * shrink;
            }
        }
    }

    void RemoveFx(int i)
    {
        fxItems.RemoveAt(i);
        fxVel.RemoveAt(i);
        fxLife.RemoveAt(i);
        fxMaxLife.RemoveAt(i);
        fxTexts.RemoveAt(i);
        fxBaseScale.RemoveAt(i);
    }

    // ---------------- 渲染 ----------------
    void ApplyVisuals(float t)
    {
        if (views.Count != cells.Count) return;

        float scale = Mathf.Max(1.04f, segmentScale);   // 略微重叠，拐角天然填满
        bodyOccluding = IsBodyOccludingHead();

        var bodyCurrent = isBoosting
            ? (bodyOccluding ? bodyMatBoostGhost : bodyMatBoost)
            : (bodyOccluding ? bodyMatGhost : bodyMat);
        var headCurrent = isBoosting ? headMatBoost : headMat;

        for (int i = 0; i < views.Count; i++)
        {
            bool show = bodyRevealed || i == 0;   // 倒计时只露出蛇头
            if (views[i].activeSelf != show) views[i].SetActive(show);

            Vector3 a = CellToWorld(prevCells.Count > i ? prevCells[i] : cells[i]);
            Vector3 b = CellToWorld(cells[i]);
            views[i].transform.position = Vector3.Lerp(a, b, t);
            // 头略大一点点：避免和第一节身体在接缝处产生深度打架（闪烁）
            float thisScale = (i == 0) ? scale * 1.02f : scale;
            views[i].transform.localScale = Vector3.one * thisScale;
            views[i].transform.rotation = Quaternion.identity;
            views[i].GetComponent<Renderer>().sharedMaterial = (i == 0) ? headCurrent : bodyCurrent;

            // 模板掩码 + 外扩描边
            // 掩码盖住整条蛇，描边只在掩码之外显示 → 只有最外一圈，内部无线
            if (i < maskViews.Count && i < outlineViews.Count)
            {
                bool showHull = bodyRevealed;
                if (maskViews[i].activeSelf != showHull) maskViews[i].SetActive(showHull);
                if (outlineViews[i].activeSelf != showHull) outlineViews[i].SetActive(showHull);

                if (showHull)
                {
                    // 掩码必须和身体**完全同尺寸**（都用 scale），否则边缘会漏
                    maskViews[i].transform.position = views[i].transform.position;
                    maskViews[i].transform.rotation = Quaternion.identity;
                    maskViews[i].transform.localScale = Vector3.one * scale;

                    // 描边壳再大一圈
                    outlineViews[i].transform.position = views[i].transform.position;
                    outlineViews[i].transform.rotation = Quaternion.identity;
                    outlineViews[i].transform.localScale = Vector3.one * (scale * 1.16f);
                }
            }
        }

        // 金币：竖着绕 Y 轴旋转（像旋转的金币）+ 轻微起伏
        if (foodViews.Count > 0)
        {
            for (int i = 0; i < foodViews.Count && i < foodCells.Count; i++)
            {
                var f = foodViews[i];
                if (f == null || !f.activeSelf) continue;

                Vector3 basePos = CellToWorld(foodCells[i]);   // 用格子坐标算，避免累积漂移
                float bob = Mathf.Sin(Time.time * 2.2f + i) * 0.06f;
                f.transform.position = new Vector3(basePos.x, basePos.y + bob, basePos.z);
                f.transform.rotation = Quaternion.Euler(90f, Time.time * 160f, 0f);
            }
        }

        UpdatePathFillers(bodyCurrent);

        UpdateHeadRotation();

        if (headLight != null)
            headLight.transform.position = views[0].transform.position + Vector3.up * 0.6f;

        // 把头灯位置传给蛇身着色器
        Shader.SetGlobalVector("_HeadPos", views[0].transform.position);
        Shader.SetGlobalColor("_HeadLight", new Color(0.55f, 1f, 0.7f, 1f));
        Shader.SetGlobalFloat("_HeadRange", 6f);


        UpdateHeadPlane();
    }

    void UpdatePathFillers(Material mat)
    {
        int used = 0;

        if (bodyRevealed)
        {
            float scale = Mathf.Max(1.04f, segmentScale);

            for (int i = 1; i < cells.Count; i++)
            {
                // ---- 身体底块 ----
                if (used >= pathFillers.Count)
                {
                    var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    go.name = "PathFill";
                    go.transform.SetParent(snakeRoot, false);
                    Destroy(go.GetComponent<Collider>());
                    pathFillers.Add(go);

                    var m = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    m.name = "FillMask";
                    m.transform.SetParent(snakeRoot, false);
                    Destroy(m.GetComponent<Collider>());
                    var mr = m.GetComponent<Renderer>();
                    if (maskMat != null) mr.sharedMaterial = maskMat;
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    mr.receiveShadows = false;
                    fillerMasks.Add(m);

                    var h = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    h.name = "FillHull";
                    h.transform.SetParent(snakeRoot, false);
                    Destroy(h.GetComponent<Collider>());
                    var hr = h.GetComponent<Renderer>();
                    if (outlineMat != null) hr.sharedMaterial = outlineMat;
                    hr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    hr.receiveShadows = false;
                    fillerHulls.Add(h);
                }

                Vector3 pos = CellToWorld(cells[i]);
                var filler = pathFillers[used];
                filler.SetActive(true);
                filler.transform.position = pos;
                filler.transform.rotation = Quaternion.identity;
                filler.transform.localScale = Vector3.one * scale;
                filler.GetComponent<Renderer>().sharedMaterial = mat;

                fillerMasks[used].SetActive(true);
                fillerMasks[used].transform.position = pos;
                fillerMasks[used].transform.rotation = Quaternion.identity;
                fillerMasks[used].transform.localScale = Vector3.one * scale;

                fillerHulls[used].SetActive(true);
                fillerHulls[used].transform.position = pos;
                fillerHulls[used].transform.rotation = Quaternion.identity;
                fillerHulls[used].transform.localScale = Vector3.one * (scale * 1.16f);

                used++;
            }
        }

        for (int i = used; i < pathFillers.Count; i++)
        {
            if (pathFillers[i] != null) pathFillers[i].SetActive(false);
            if (i < fillerMasks.Count && fillerMasks[i] != null) fillerMasks[i].SetActive(false);
            if (i < fillerHulls.Count && fillerHulls[i] != null) fillerHulls[i].SetActive(false);
        }
    }

    void BuildHeadPlane()
    {
        // 每层 gridSize × gridSize 个高亮块
        var mat = MakeTransparent(new Color(0.55f, 0.95f, 1f), 0.09f);
        mat.SetInt("_ZWrite", 0);

        for (int i = 0; i < gridSize * gridSize; i++)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "LayerBlock";
            go.transform.SetParent(transform, false);
            Destroy(go.GetComponent<Collider>());
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            go.transform.localScale = Vector3.one * 0.98f;   // 整格立方体，才有体积感
            layerBlocks.Add(go);
        }

        // 层外框：12 条边
        var edgeMat = MakeTransparent(new Color(0.55f, 0.95f, 1f), 0.45f);
        for (int i = 0; i < 12; i++)
        {
            var go = new GameObject("LayerEdge");
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.material = edgeMat;
            lr.useWorldSpace = true;
            lr.positionCount = 2;
            lr.startWidth = 0.025f;
            lr.endWidth = 0.025f;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            layerEdges.Add(lr);
        }

        lastLayer = -999;
    }

    void UpdateHeadPlane()
    {
        if (!showLayerHighlight) return;
        if (layerBlocks.Count == 0 || views.Count == 0) return;

        float half = (gridSize - 1) * 0.5f;
        int layer = Mathf.RoundToInt(views[0].transform.position.y + half);
        layer = Mathf.Clamp(layer, 0, gridSize - 1);

        float y = layer - half;

        int k = 0;
        for (int x = 0; x < gridSize; x++)
        {
            for (int z = 0; z < gridSize; z++)
            {
                var b = layerBlocks[k++];
                if (!b.activeSelf) b.SetActive(true);
                b.transform.position = new Vector3(x - half, y, z - half);
            }
        }

        // 外框：整层范围（格心 ±0.5）与一格高的立方体
        if (layerEdges.Count == 12)
        {
            float e = gridSize * 0.5f;
            float y0 = y - 0.5f, y1 = y + 0.5f;
            Vector3[] c = {
                new Vector3(-e, y0, -e), new Vector3( e, y0, -e),
                new Vector3( e, y0,  e), new Vector3(-e, y0,  e),
                new Vector3(-e, y1, -e), new Vector3( e, y1, -e),
                new Vector3( e, y1,  e), new Vector3(-e, y1,  e)
            };
            int[,] edges = {
                {0,1},{1,2},{2,3},{3,0},
                {4,5},{5,6},{6,7},{7,4},
                {0,4},{1,5},{2,6},{3,7}
            };
            for (int i = 0; i < 12; i++)
            {
                layerEdges[i].SetPosition(0, c[edges[i, 0]]);
                layerEdges[i].SetPosition(1, c[edges[i, 1]]);
            }
        }

        lastLayer = layer;
    }

    bool IsBodyOccludingHead()
    {
        var cam = Camera.main;
        if (cam == null || views.Count < 3) return false;

        Vector3 c = cam.transform.position;
        Vector3 h = views[0].transform.position;
        Vector3 ab = h - c;
        float len2 = ab.sqrMagnitude;
        if (len2 < 0.0001f) return false;

        for (int i = 2; i < views.Count; i++)
        {
            Vector3 p = views[i].transform.position;
            float t = Vector3.Dot(p - c, ab) / len2;
            if (t <= 0.03f || t >= 0.97f) continue;
            Vector3 proj = c + ab * t;
            if ((p - proj).sqrMagnitude < 0.8f * 0.8f) return true;
        }
        return false;
    }

    void UpdateHeadRotation()
    {
        if (views.Count == 0) return;
        var d = faceDir;
        if (d == Vector3Int.zero) d = Vector3Int.right;
        var refUp = Mathf.Abs(d.y) > 0.5f ? Vector3.forward : Vector3.up;
        views[0].transform.rotation = Quaternion.LookRotation(d, refUp);
    }

    void RebuildViews()
    {
        while (views.Count < cells.Count)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = views.Count == 0 ? "Head" : "Segment";
            go.transform.SetParent(snakeRoot, false);
            go.transform.localScale = Vector3.one * Mathf.Max(1f, segmentScale);
            views.Add(go);
        }
        while (views.Count > cells.Count)
        {
            Destroy(views[views.Count - 1]);
            views.RemoveAt(views.Count - 1);
        }

        // 掩码 + 描边：和身体等量（头也一起，描边会包住整条蛇）
        while (outlineViews.Count < views.Count)
        {
            var maskGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            maskGo.name = "StencilMask";
            maskGo.transform.SetParent(snakeRoot, false);
            Destroy(maskGo.GetComponent<Collider>());
            var mr = maskGo.GetComponent<Renderer>();
            if (maskMat != null) mr.sharedMaterial = maskMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            maskViews.Add(maskGo);

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Outline";
            go.transform.SetParent(snakeRoot, false);
            Destroy(go.GetComponent<Collider>());
            var r = go.GetComponent<Renderer>();
            if (outlineMat != null) r.sharedMaterial = outlineMat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            outlineViews.Add(go);
        }
        while (outlineViews.Count > views.Count)
        {
            Destroy(outlineViews[outlineViews.Count - 1]);
            outlineViews.RemoveAt(outlineViews.Count - 1);
            Destroy(maskViews[maskViews.Count - 1]);
            maskViews.RemoveAt(maskViews.Count - 1);
        }

        for (int i = 0; i < views.Count; i++)
            views[i].GetComponent<Renderer>().sharedMaterial = (i == 0) ? headMat : bodyMat;

        UpdateHeadRotation();
        if (views.Count > 0) EnsureEyes(views[0].transform);
    }

    void EnsureEyes(Transform head)
    {
        if (eyesRoot != null && eyesRoot.parent == head) return;
        if (eyesRoot != null) Destroy(eyesRoot.gameObject);

        if (eyeWhiteMat == null)
        {
            eyeWhiteMat = MakeMaterial(new Color(0.95f, 0.97f, 1f), 0.85f, 0f, new Color(0.9f, 0.95f, 1f) * 0.1f);
            eyePupilMat = MakeMaterial(new Color(0.04f, 0.04f, 0.06f), 0.9f, 0f);
        }

        var root = new GameObject("Eyes").transform;
        root.SetParent(head, false);
        eyesRoot = root;

        for (int i = -1; i <= 1; i += 2)
        {
            var eye = GameObject.CreatePrimitive(PrimitiveType.Cube);
            eye.name = i < 0 ? "Eye_L" : "Eye_R";
            eye.transform.SetParent(root, false);
            eye.transform.localPosition = new Vector3(0.20f * i, 0.10f, 0.40f);
            eye.transform.localScale = Vector3.one * 0.18f;
            eye.GetComponent<Renderer>().sharedMaterial = eyeWhiteMat;
            Destroy(eye.GetComponent<Collider>());

            var pupil = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pupil.name = "Pupil";
            pupil.transform.SetParent(eye.transform, false);
            pupil.transform.localPosition = new Vector3(0f, 0f, 0.55f);
            pupil.transform.localScale = new Vector3(0.55f, 0.55f, 0.3f);
            pupil.GetComponent<Renderer>().sharedMaterial = eyePupilMat;
            Destroy(pupil.GetComponent<Collider>());
        }
    }

    // ---------------- 蛇身数据 ----------------
    void ResetSnake()
    {
        cells.Clear();
        prevCells.Clear();
        turnQueue.Clear();

        int mid = gridSize / 2;
        cells.Add(new Vector3Int(mid, mid, mid));   // 只生成头，正好在地图中心

        for (int i = 0; i < cells.Count; i++) prevCells.Add(cells[i]);
    }

    // ---------------- 食物 ----------------
    void RefreshFoods()
    {
        foodCells.Clear();
        for (int i = 0; i < foodCount; i++) SpawnExtraFood();
        SyncFoodViews();
    }

    void SpawnExtraFood()
    {
        int guard = 0;
        while (guard++ < 5000)
        {
            var c = new Vector3Int(Random.Range(0, gridSize), Random.Range(0, gridSize), Random.Range(0, gridSize));
            if (cells.Contains(c)) continue;
            if (foodCells.Contains(c)) continue;
            foodCells.Add(c);
            SyncFoodViews();
            return;
        }
    }

    void SyncFoodViews()
    {
        while (foodViews.Count < foodCells.Count)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "Coin";
            go.transform.SetParent(boardRoot, false);
            go.transform.localScale = new Vector3(0.72f, 0.07f, 0.72f);   // 薄圆盘
            go.GetComponent<Renderer>().sharedMaterial = foodMat;
            Destroy(go.GetComponent<Collider>());
            foodViews.Add(go);
        }
        while (foodViews.Count > foodCells.Count)
        {
            Destroy(foodViews[foodViews.Count - 1]);
            foodViews.RemoveAt(foodViews.Count - 1);
        }

        for (int i = 0; i < foodCells.Count; i++)
        {
            foodViews[i].SetActive(true);
            foodViews[i].transform.position = CellToWorld(foodCells[i]);
        }
    }

    // ---------------- 场景搭建 ----------------
    void BuildWorld()
    {
        CleanWorldContent();

        boardRoot = new GameObject("Board").transform;
        boardRoot.SetParent(transform, false);

        snakeRoot = new GameObject("Snake").transform;
        snakeRoot.SetParent(transform, false);

        BuildSky();
        BuildMenuDecor();
        BuildWorldPieces();
    }

    void BuildMenuDecor()
    {
        // 已改为 2D 全屏背景（MenuUI 里实现），这里不再生成 3D 装饰
        return;
        if (menuDecorRoot != null) Destroy(menuDecorRoot.gameObject);
        decorItems.Clear();
        decorSpeeds.Clear();
        decorPhases.Clear();
        decorBaseY.Clear();

        menuDecorRoot = new GameObject("MenuDecor").transform;
        menuDecorRoot.SetParent(transform, false);

        var colors = new[]
        {
            new Color(0.35f, 0.75f, 0.95f, 1f),
            new Color(0.45f, 0.9f, 0.65f, 1f),
            new Color(0.95f, 0.8f, 0.3f, 1f),
            new Color(0.8f, 0.45f, 0.9f, 1f)
        };

        var rng = new System.Random(20261127);
        for (int i = 0; i < 16; i++)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Decor" + i;
            go.transform.SetParent(menuDecorRoot, false);
            Destroy(go.GetComponent<Collider>());

            float angle = (float)(rng.NextDouble() * Mathf.PI * 2f);
            float radius = 5f + (float)rng.NextDouble() * 6f;
            float height = -3f + (float)rng.NextDouble() * 6f;
            go.transform.position = new Vector3(Mathf.Cos(angle) * radius, height, Mathf.Sin(angle) * radius);
            go.transform.localScale = Vector3.one * (0.6f + (float)rng.NextDouble() * 1.6f);
            go.transform.rotation = Quaternion.Euler((float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 360f, 0f);

            var mat = MakeFlat(colors[i % colors.Length]);
            go.GetComponent<Renderer>().sharedMaterial = mat;

            decorItems.Add(go.transform);
            decorSpeeds.Add(0.2f + (float)rng.NextDouble() * 0.5f);
            decorPhases.Add((float)rng.NextDouble() * 6.28f);
            decorBaseY.Add(go.transform.localPosition.y);
        }
    }

    void UpdateMenuDecor()
    {
        if (menuDecorRoot == null) return;

        for (int i = 0; i < decorItems.Count; i++)
        {
            var t = decorItems[i];
            if (t == null) continue;

            t.Rotate(Vector3.up, decorSpeeds[i] * 28f * Time.deltaTime, Space.World);
            t.Rotate(Vector3.right, decorSpeeds[i] * 11f * Time.deltaTime, Space.World);

            var p = t.localPosition;
            p.y = decorBaseY[i] + Mathf.Sin(Time.time * 0.7f + decorPhases[i]) * 0.5f;
            t.localPosition = p;
        }
    }

    void BuildSky()
    {
        var shader = Shader.Find("Snake/GradientSky");
        if (shader == null) return;

        if (skySphere != null) Destroy(skySphere);

        skySphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        skySphere.name = "Sky";
        skySphere.transform.SetParent(transform, false);
        skySphere.transform.position = Vector3.zero;
        skySphere.transform.localScale = Vector3.one * 400f;
        Destroy(skySphere.GetComponent<Collider>());

        var skyMat = new Material(shader);
        if (skyMat.HasProperty("_TopColor")) skyMat.SetColor("_TopColor", new Color(0.12f, 0.20f, 0.36f, 1f));
        if (skyMat.HasProperty("_BottomColor")) skyMat.SetColor("_BottomColor", new Color(0.04f, 0.07f, 0.13f, 1f));

        var r = skySphere.GetComponent<Renderer>();
        r.sharedMaterial = skyMat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
    }

    void BuildWorldPieces()
    {
        if (boundaryRoot != null) Destroy(boundaryRoot);

        boundaryRoot = BuildBoundary();
        BuildHeadPlane();
    }

    void CleanWorldContent()
    {
        foreach (var v in views) if (v != null) Destroy(v);
        views.Clear();
        foreach (var f in pathFillers) if (f != null) Destroy(f);
        pathFillers.Clear();
        foreach (var m in fillerMasks) if (m != null) Destroy(m);
        fillerMasks.Clear();
        foreach (var h in fillerHulls) if (h != null) Destroy(h);
        fillerHulls.Clear();
        foreach (var o in outlineViews) if (o != null) Destroy(o);
        outlineViews.Clear();
        foreach (var m in maskViews) if (m != null) Destroy(m);
        maskViews.Clear();
        foreach (var f in foodViews) if (f != null) Destroy(f);
        foodViews.Clear();
        foodCells.Clear();

        if (eyesRoot != null) { Destroy(eyesRoot.gameObject); eyesRoot = null; }
        foreach (var b in layerBlocks) if (b != null) Destroy(b);
        layerBlocks.Clear();
        foreach (var e in layerEdges) if (e != null) Destroy(e.gameObject);
        layerEdges.Clear();
        lastLayer = -999;
        if (skySphere != null) { Destroy(skySphere); skySphere = null; }
        if (boardRoot != null) { boardRoot.gameObject.SetActive(false); Destroy(boardRoot.gameObject); boardRoot = null; }
        boundaryRoot = null;
        if (snakeRoot != null) { snakeRoot.gameObject.SetActive(false); Destroy(snakeRoot.gameObject); snakeRoot = null; }
    }

    GameObject BuildBoundary()
    {
        var root = new GameObject("Boundary");
        root.transform.SetParent(boardRoot, false);

        Vector3[] corners = new Vector3[8];
        int k = 0;
        for (int x = 0; x < 2; x++)
        for (int y = 0; y < 2; y++)
        for (int z = 0; z < 2; z++)
            corners[k++] = new Vector3((x - 0.5f) * gridSize, (y - 0.5f) * gridSize, (z - 0.5f) * gridSize);

        int[,] edges = {
            {0,1},{1,3},{3,2},{2,0},
            {4,5},{5,7},{7,6},{6,4},
            {0,4},{1,5},{2,6},{3,7}
        };

        for (int i = 0; i < edges.GetLength(0); i++)
        {
            var go = new GameObject("Edge");
            go.transform.SetParent(root.transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.material = boundaryMat;
            lr.positionCount = 2;
            lr.startWidth = 0.05f;
            lr.endWidth = 0.05f;
            lr.useWorldSpace = false;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.SetPosition(0, corners[edges[i, 0]]);
            lr.SetPosition(1, corners[edges[i, 1]]);
        }
        return root;
    }

    void SetupLighting()
    {
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.24f, 0.32f, 0.45f);
        RenderSettings.ambientEquatorColor = new Color(0.10f, 0.12f, 0.16f);
        RenderSettings.ambientGroundColor = new Color(0.04f, 0.05f, 0.07f);
        RenderSettings.ambientIntensity = 0.7f;

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = 0.012f;
        RenderSettings.fogColor = new Color(0.05f, 0.06f, 0.09f);

        Light sun = null;
        foreach (var l in FindObjectsOfType<Light>())
            if (l.type == LightType.Directional) { sun = l; break; }

        if (sun == null)
        {
            var go = new GameObject("Sun");
            go.transform.SetParent(transform, false);
            sun = go.AddComponent<Light>();
            sun.type = LightType.Directional;
        }
        sun.intensity = 0.0f;   // 只保留蛇头光源，方向光关掉
        sun.color = new Color(1f, 0.96f, 0.88f);
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = 0.75f;
        sun.transform.rotation = Quaternion.Euler(52f, -35f, 0f);

        QualitySettings.shadowDistance = 60f;

        var headGo = new GameObject("HeadLight");
        headGo.transform.SetParent(transform, false);
        headLight = headGo.AddComponent<Light>();
        headLight.type = LightType.Point;
        headLight.color = new Color(0.55f, 1f, 0.7f);
        headLight.range = 7f;
        headLight.intensity = 1.1f;
        headLight.shadows = LightShadows.None;

        var cam = Camera.main;
        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.03f, 0.04f, 0.06f);
        }
    }

    void GameOver(string reason)
    {
        isBoosting = false;
        state = GameState.GameOver;
        blockedByWall = false;
        FreezeVisuals();
        if (AudioManager.Instance != null)
        {
            if (won) AudioManager.Instance.PlayWin();
            else AudioManager.Instance.PlayOver();
        }

        Debug.Log($"游戏结束：{reason}，得分 {score}/{targetScore}");
        OnGameOver?.Invoke(reason);
    }
}
