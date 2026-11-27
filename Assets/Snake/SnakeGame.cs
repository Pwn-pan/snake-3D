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
    public Color headColor = new Color(0.45f, 0.95f, 0.55f);
    public Color bodyColor = new Color(0.20f, 0.62f, 0.95f);
    public Color foodColor = new Color(1.00f, 0.80f, 0.20f);
    public float segmentScale = 1.02f;

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
    private bool bodyOccluding;

    public event System.Action OnEat;
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

        headMat = MakeMaterial(headColor, 0.65f, 0.1f, headColor * 0.25f);
        bodyMat = MakeMaterial(bodyColor, 0.55f, 0.1f);
        foodMat = MakeMaterial(foodColor, 0.8f, 0.2f, foodColor);
        headMatBoost = MakeMaterial(Color.Lerp(headColor, Color.white, 0.22f), 0.8f, 0.1f, headColor * 0.8f);
        bodyMatBoost = MakeMaterial(Color.Lerp(bodyColor, Color.white, 0.22f), 0.8f, 0.1f, bodyColor * 0.8f);
        bodyMatGhost = MakeTransparent(bodyColor, 0.28f);
        bodyMatBoostGhost = MakeTransparent(Color.Lerp(bodyColor, Color.white, 0.22f), 0.32f);
        boundaryMat = MakeMaterial(new Color(0.55f, 0.75f, 0.9f, 0.85f), 0.3f, 0f);

        BuildWorld();
    }

    void Update()
    {
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

        BuildWorld();          // 清空重建：边界 / 地面 / 蛇 / 食物
        ResetSnake();
        RebuildViews();
        RefreshFoods();
        ApplyVisuals(1f);

        if (snakeRoot != null) snakeRoot.gameObject.SetActive(true);

        countdownLeft = countdownSeconds;
        state = GameState.Countdown;
    }

    public void TogglePause()
    {
        if (state != GameState.Playing) return;
        paused = !paused;
    }

    public void ToMenu()
    {
        state = GameState.Menu;
        paused = false;
        if (snakeRoot != null) snakeRoot.gameObject.SetActive(false);
        foreach (var f in foodViews) if (f != null) f.SetActive(false);
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
            if (AudioManager.Instance != null) AudioManager.Instance.PlayBump();
            blockTimer = 0f;
            FreezeVisuals();
            return;
        }

        if (SelfHit(next))
        {
            blockedByWall = true;
            blockReason = "咬到自己";
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

    // ---------------- 渲染 ----------------
    void ApplyVisuals(float t)
    {
        if (views.Count != cells.Count) return;

        float scale = Mathf.Max(1f, segmentScale);
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
            views[i].transform.localScale = Vector3.one * (scale * 1.003f);
            views[i].transform.rotation = Quaternion.identity;
            views[i].GetComponent<Renderer>().sharedMaterial = (i == 0) ? headCurrent : bodyCurrent;
        }

        UpdateHeadRotation();

        if (headLight != null)
            headLight.transform.position = views[0].transform.position + Vector3.up * 0.6f;

        UpdatePathFillers(tmpFillerMat: bodyCurrent);
        UpdateHeadPlane();
    }

    void UpdatePathFillers(Material tmpFillerMat)
    {
        if (!bodyRevealed)
        {
            foreach (var f in pathFillers) if (f != null) f.SetActive(false);
            return;
        }

        int used = 0;
        float fillerScale = Mathf.Max(1f, segmentScale);

        for (int i = 1; i < cells.Count; i++)
        {
            if (used >= pathFillers.Count)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "PathFill";
                go.transform.SetParent(snakeRoot, false);
                go.GetComponent<Renderer>().sharedMaterial = bodyMat;
                Destroy(go.GetComponent<Collider>());
                pathFillers.Add(go);
            }

            var filler = pathFillers[used++];
            filler.SetActive(true);
            filler.transform.position = CellToWorld(cells[i]);
            filler.transform.rotation = Quaternion.identity;
            filler.transform.localScale = Vector3.one * fillerScale;
            filler.GetComponent<Renderer>().sharedMaterial = tmpFillerMat;
        }

        for (int i = used; i < pathFillers.Count; i++)
            pathFillers[i].SetActive(false);
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
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Food";
            go.transform.SetParent(boardRoot, false);
            go.transform.localScale = Vector3.one * Mathf.Max(1f, segmentScale);
            go.GetComponent<Renderer>().sharedMaterial = foodMat;
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

        BuildWorldPieces();
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
        foreach (var f in foodViews) if (f != null) Destroy(f);
        foodViews.Clear();
        foodCells.Clear();

        if (eyesRoot != null) { Destroy(eyesRoot.gameObject); eyesRoot = null; }
        foreach (var b in layerBlocks) if (b != null) Destroy(b);
        layerBlocks.Clear();
        foreach (var e in layerEdges) if (e != null) Destroy(e.gameObject);
        layerEdges.Clear();
        lastLayer = -999;
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
            lr.startWidth = 0.035f;
            lr.endWidth = 0.035f;
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
        RenderSettings.ambientSkyColor = new Color(0.18f, 0.22f, 0.30f);
        RenderSettings.ambientEquatorColor = new Color(0.10f, 0.12f, 0.16f);
        RenderSettings.ambientGroundColor = new Color(0.04f, 0.05f, 0.07f);
        RenderSettings.ambientIntensity = 1f;

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
        sun.intensity = 1.15f;
        sun.color = new Color(1f, 0.96f, 0.88f);
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = 0.75f;
        sun.transform.rotation = Quaternion.Euler(52f, -35f, 0f);

        QualitySettings.shadowDistance = 60f;

        var headGo = new GameObject("HeadLight");
        headGo.transform.SetParent(transform, false);
        headLight = headGo.AddComponent<Light>();
        headLight.type = LightType.Point;
        headLight.color = new Color(0.55f, 1f, 0.65f);
        headLight.range = 6f;
        headLight.intensity = 1.6f;
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
