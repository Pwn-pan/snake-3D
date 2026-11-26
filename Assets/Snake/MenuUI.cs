using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// 代码生成的界面：主菜单 / 设置 / 倒计时 / 计分 / 结束面板。
/// 不需要手动搭 Canvas，运行时全部创建。
/// </summary>
public class MenuUI : MonoBehaviour
{
    private SnakeGame game;
    private Font font;

    private GameObject menuPanel, settingsPanel, hudPanel, overPanel, countdownObj, pausePanel, difficultyPanel, mapPanel;
    private Text scoreText, overScoreText, countdownText, overTitle;
    private Text bgmLabel, sfxLabel, speedLabel, viewLabel, camLabel;
    private Slider bgmSlider, sfxSlider, speedSlider, camSlider;

    private int lastCountdownStep = -1;

    void Awake()
    {
        font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Arial" }, 40);
        if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    void Start()
    {
        game = FindObjectOfType<SnakeGame>();

        if (FindObjectOfType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            es.transform.SetParent(transform, false);
        }

        BuildUI();
        ShowMenu();
    }

    void Update()
    {
        if (game == null) return;

        // 倒计时显示
        if (game.state == SnakeGame.GameState.Countdown)
        {
            countdownObj.SetActive(true);
            int step = Mathf.CeilToInt(game.countdownLeft);
            if (step != lastCountdownStep)
            {
                lastCountdownStep = step;
                countdownText.text = step > 0 ? step.ToString() : "开始！";
                if (AudioManager.Instance != null)
                {
                    if (step > 0) AudioManager.Instance.PlayCount();
                    else AudioManager.Instance.PlayGo();
                }
            }
        }
        else
        {
            countdownObj.SetActive(false);
            lastCountdownStep = -1;
        }

        bool playing = game.state == SnakeGame.GameState.Playing;
        hudPanel.SetActive(playing && !game.paused);
        pausePanel.SetActive(playing && game.paused);
        scoreText.text = $"得分  {game.score} / {game.targetScore}" + (game.targetReached ? "　✔ 目标达成" : "");

        if (game.state == SnakeGame.GameState.GameOver && !overPanel.activeSelf)
        {
            overPanel.SetActive(true);
            overScoreText.text = $"得分 {game.score} / {game.targetScore}";
            overTitle.text = game.won ? "胜利！" : "游戏结束";
            overTitle.color = game.won
                ? new Color(1f, 0.86f, 0.35f)
                : new Color(1f, 0.55f, 0.55f);
        }
    }

    // ---------------- 按钮响应 ----------------
    public void ShowMenu()
    {
        if (game != null) game.ToMenu();   // ← 关键：状态也要回 Menu，否则 Update 会立刻把结束面板弹回来

        menuPanel.SetActive(true);
        settingsPanel.SetActive(false);
        overPanel.SetActive(false);
        hudPanel.SetActive(false);
        countdownObj.SetActive(false);
        if (pausePanel != null) pausePanel.SetActive(false);
        if (difficultyPanel != null) difficultyPanel.SetActive(false);
        if (mapPanel != null) mapPanel.SetActive(false);
    }

    void ShowDifficulty()
    {
        menuPanel.SetActive(false);
        settingsPanel.SetActive(false);
        overPanel.SetActive(false);
        difficultyPanel.SetActive(true);
        mapPanel.SetActive(false);
    }

    void ShowMapSelect()
    {
        difficultyPanel.SetActive(false);
        mapPanel.SetActive(true);
    }

    void StartGameClicked()
    {
        if (game == null) return;
        menuPanel.SetActive(false);
        settingsPanel.SetActive(false);
        overPanel.SetActive(false);
        difficultyPanel.SetActive(false);
        mapPanel.SetActive(false);
        if (pausePanel != null) pausePanel.SetActive(false);
        game.StartGame();
    }

    void OpenSettings()
    {
        menuPanel.SetActive(false);
        settingsPanel.SetActive(true);

        bgmSlider.SetValueWithoutNotify(GameSettings.BgmVolume);
        sfxSlider.SetValueWithoutNotify(GameSettings.SfxVolume);
        speedSlider.SetValueWithoutNotify(GameSettings.IntervalToSlider(GameSettings.MoveInterval));
        if (camSlider != null) camSlider.SetValueWithoutNotify(GameSettings.CameraSmoothing);
        RefreshLabels();
    }

    void CloseSettings()
    {
        settingsPanel.SetActive(false);
        menuPanel.SetActive(true);
    }

    void PushCameraMode()
    {
        var cam = FindObjectOfType<CameraOrbit>();
        if (cam != null) cam.SendMessage("SnapToMode", SendMessageOptions.DontRequireReceiver);
    }

    void RefreshLabels()
    {
        bgmLabel.text = "背景音乐  " + Mathf.RoundToInt(GameSettings.BgmVolume * 100) + "%";
        sfxLabel.text = "音效  " + Mathf.RoundToInt(GameSettings.SfxVolume * 100) + "%";
        if (camLabel != null)
            camLabel.text = $"镜头动画速度  {GameSettings.CameraSmoothing:0.0}（小=稳，大=跟手）";

        if (viewLabel != null)
            viewLabel.text = "视角  " + (GameSettings.FixedView ? "固定（跟随蛇头）" : "自由（左键环绕）");

        float interval = GameSettings.SliderToInterval(speedSlider.value);
        float perSec = 1f / Mathf.Max(0.01f, interval);
        speedLabel.text = $"蛇的速度  {perSec:0.0} 格/秒";
    }

    // ---------------- UI 构建 ----------------
    void BuildUI()
    {
        var canvasGo = new GameObject("UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        // ---- 主菜单 ----
        menuPanel = Panel(canvasGo.transform, "Menu");
        Label(menuPanel.transform, "3D 贪吃蛇", 96, new Vector2(0, 220), new Vector2(1200, 140), FontStyle.Bold, new Color(0.85f, 1f, 0.85f));
        Label(menuPanel.transform, "A / D 左右转　W / S 持续升降（按 A/D 恢复平飞）　Shift 加速　左键拖动转视角", 26, new Vector2(0, 120), new Vector2(1400, 50), FontStyle.Normal, new Color(0.75f, 0.85f, 0.75f));

        Button(menuPanel.transform, "开始游戏", new Vector2(0, -20), ShowDifficulty);
        Button(menuPanel.transform, "设置", new Vector2(0, -140), OpenSettings);

        // ---- 设置 ----
        settingsPanel = Panel(canvasGo.transform, "Settings");
        Label(settingsPanel.transform, "设置", 72, new Vector2(0, 300), new Vector2(900, 110), FontStyle.Bold, Color.white);

        bgmLabel = Label(settingsPanel.transform, "背景音乐", 30, new Vector2(-350, 220), new Vector2(800, 45), FontStyle.Normal, Color.white, TextAnchor.MiddleLeft);
        bgmSlider = Slider(settingsPanel.transform, new Vector2(0, 167), 0f, 1f, GameSettings.BgmVolume, v =>
        {
            GameSettings.BgmVolume = v;
            RefreshLabels();
        });

        sfxLabel = Label(settingsPanel.transform, "音效", 30, new Vector2(-350, 105), new Vector2(800, 45), FontStyle.Normal, Color.white, TextAnchor.MiddleLeft);
        sfxSlider = Slider(settingsPanel.transform, new Vector2(0, 52), 0f, 1f, GameSettings.SfxVolume, v =>
        {
            GameSettings.SfxVolume = v;
            RefreshLabels();
        });
        sfxSlider.gameObject.AddComponent<SliderReleaseSound>();

        speedLabel = Label(settingsPanel.transform, "蛇的速度", 30, new Vector2(-350, -10), new Vector2(800, 45), FontStyle.Normal, Color.white, TextAnchor.MiddleLeft);
        speedSlider = Slider(settingsPanel.transform, new Vector2(0, -63), 0f, 1f, GameSettings.IntervalToSlider(GameSettings.MoveInterval), v =>
        {
            GameSettings.MoveInterval = GameSettings.SliderToInterval(v);
            if (game != null) game.MoveInterval = GameSettings.MoveInterval;
            RefreshLabels();
        });

        camLabel = Label(settingsPanel.transform, "镜头动画速度", 30, new Vector2(-350, -125), new Vector2(800, 45), FontStyle.Normal, Color.white, TextAnchor.MiddleLeft);
        camSlider = Slider(settingsPanel.transform, new Vector2(0, -178), 0.5f, 20f, GameSettings.CameraSmoothing, v =>
        {
            GameSettings.CameraSmoothing = v;
            RefreshLabels();
        });
        camSlider.gameObject.AddComponent<SliderReleaseSound>();

        viewLabel = Label(settingsPanel.transform, "视角", 30, new Vector2(-350, -245), new Vector2(800, 45), FontStyle.Normal, Color.white, TextAnchor.MiddleLeft);
        Button(settingsPanel.transform, "切换视角", new Vector2(0, -298), () =>
        {
            GameSettings.FixedView = !GameSettings.FixedView;
            RefreshLabels();
            PushCameraMode();
        });

        Button(settingsPanel.transform, "返回", new Vector2(0, -400), CloseSettings);

        // ---- 难度选择 ----
        difficultyPanel = Panel(canvasGo.transform, "Difficulty");
        Label(difficultyPanel.transform, "选择难度", 72, new Vector2(0, 280), new Vector2(1000, 110), FontStyle.Bold, Color.white);
        Label(difficultyPanel.transform, "目标分数 = 总格数 × 难度比例", 26, new Vector2(0, 195), new Vector2(1000, 45), FontStyle.Normal, new Color(0.75f, 0.85f, 0.75f));
        Button(difficultyPanel.transform, "简单　20%　　初始 1 节", new Vector2(0, 70), () => { GameSettings.Difficulty = 0; ShowMapSelect(); });
        Button(difficultyPanel.transform, "普通　30%　　初始 3 节", new Vector2(0, -40), () => { GameSettings.Difficulty = 1; ShowMapSelect(); });
        Button(difficultyPanel.transform, "困难　50%　　初始 5 节", new Vector2(0, -150), () => { GameSettings.Difficulty = 2; ShowMapSelect(); });
        Button(difficultyPanel.transform, "返回", new Vector2(0, -280), ShowMenu);

        // ---- 地图选择 ----
        mapPanel = Panel(canvasGo.transform, "MapSelect");
        Label(mapPanel.transform, "选择地图", 72, new Vector2(0, 280), new Vector2(1000, 110), FontStyle.Bold, Color.white);
        Label(mapPanel.transform, "食物数量随地图变大而增加", 26, new Vector2(0, 195), new Vector2(1000, 45), FontStyle.Normal, new Color(0.75f, 0.85f, 0.75f));
        Button(mapPanel.transform, "3 × 3 × 3　食物 1 个", new Vector2(0, 70), () => { GameSettings.MapSize = 3; StartGameClicked(); });
        Button(mapPanel.transform, "5 × 5 × 5　食物 2 个", new Vector2(0, -40), () => { GameSettings.MapSize = 5; StartGameClicked(); });
        Button(mapPanel.transform, "7 × 7 × 7　食物 3 个", new Vector2(0, -150), () => { GameSettings.MapSize = 7; StartGameClicked(); });
        Button(mapPanel.transform, "返回", new Vector2(0, -280), ShowDifficulty);

        // ---- 计分 ----
        hudPanel = Panel(canvasGo.transform, "HUD", false);
        scoreText = Label(hudPanel.transform, "得分  0", 44, new Vector2(0, 0), new Vector2(400, 70), FontStyle.Bold, Color.white, TextAnchor.MiddleLeft);
        scoreText.rectTransform.anchorMin = new Vector2(0, 1);
        scoreText.rectTransform.anchorMax = new Vector2(0, 1);
        scoreText.rectTransform.pivot = new Vector2(0, 1);
        scoreText.rectTransform.anchoredPosition = new Vector2(40, -40);

        // ---- 倒计时 ----
        countdownObj = new GameObject("Countdown", typeof(RectTransform));
        countdownObj.transform.SetParent(canvasGo.transform, false);
        var cdRt = countdownObj.GetComponent<RectTransform>();
        cdRt.anchorMin = cdRt.anchorMax = new Vector2(0.5f, 0.5f);
        cdRt.sizeDelta = new Vector2(1200, 300);
        countdownText = Label(countdownObj.transform, "3", 180, Vector2.zero, new Vector2(1200, 300), FontStyle.Bold, new Color(1f, 0.95f, 0.6f));
        countdownObj.SetActive(false);

        // ---- 暂停 ----
        pausePanel = Panel(canvasGo.transform, "Pause");
        Label(pausePanel.transform, "已暂停", 80, new Vector2(0, 200), new Vector2(900, 120), FontStyle.Bold, new Color(0.85f, 0.95f, 1f));
        Label(pausePanel.transform, "按 Esc 或 P 继续", 26, new Vector2(0, 110), new Vector2(900, 45), FontStyle.Normal, new Color(0.75f, 0.82f, 0.9f));
        Button(pausePanel.transform, "继续", new Vector2(0, 0), () => { if (game != null) game.TogglePause(); });
        Button(pausePanel.transform, "重新开始", new Vector2(0, -120), StartGameClicked);
        Button(pausePanel.transform, "返回主菜单", new Vector2(0, -240), ShowMenu);

        // ---- 结束 ----
        overPanel = Panel(canvasGo.transform, "Over");
        overTitle = Label(overPanel.transform, "游戏结束", 80, new Vector2(0, 220), new Vector2(900, 120), FontStyle.Bold, new Color(1f, 0.55f, 0.55f));
        overScoreText = Label(overPanel.transform, "得分 0", 40, new Vector2(0, 110), new Vector2(600, 60), FontStyle.Normal, Color.white);
        Button(overPanel.transform, "再来一局", new Vector2(0, -20), StartGameClicked);
        Button(overPanel.transform, "返回主菜单", new Vector2(0, -140), ShowMenu);
    }

    private GameObject Panel(Transform parent, string name, bool darkBackground = true)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        if (darkBackground)
        {
            var img = go.AddComponent<Image>();
            img.color = new Color(0.03f, 0.05f, 0.08f, 0.82f);
        }
        return go;
    }

    private GameObject Button(Transform parent, string text, Vector2 pos, System.Action onClick, float width = 380f)
    {
        var go = new GameObject("Button_" + text, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(width, 84);

        var img = go.GetComponent<Image>();
        img.color = new Color(0.16f, 0.35f, 0.24f, 0.95f);

        var btn = go.GetComponent<Button>();
        var colors = btn.colors;
        colors.highlightedColor = new Color(0.25f, 0.55f, 0.36f);
        colors.pressedColor = new Color(0.12f, 0.28f, 0.18f);
        btn.colors = colors;
        btn.onClick.AddListener(() =>
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayTurn();
            onClick?.Invoke();
        });

        Label(go.transform, text, 36, Vector2.zero, rt.sizeDelta, FontStyle.Bold, Color.white);
        return go;
    }

    private Text Label(Transform parent, string text, int size, Vector2 pos, Vector2 size2,
                       FontStyle style = FontStyle.Normal, Color? color = null, TextAnchor anchor = TextAnchor.MiddleCenter)
    {
        var go = new GameObject("Label", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size2;

        var t = go.GetComponent<Text>();
        t.font = font;
        t.text = text;
        t.fontSize = size;
        t.fontStyle = style;
        t.color = color ?? Color.white;
        t.alignment = anchor;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }

    private Slider Slider(Transform parent, Vector2 pos, float min, float max, float value, System.Action<float> onChange)
    {
        var go = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(760, 26);

        // 背景条
        var bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(go.transform, false);
        var bgRt = bg.GetComponent<RectTransform>();
        bgRt.anchorMin = bgRt.anchorMax = new Vector2(0.5f, 0.5f);
        bgRt.sizeDelta = new Vector2(760, 14);
        bg.GetComponent<Image>().color = new Color(0.12f, 0.15f, 0.18f, 0.95f);

        // 填充条
        var fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(go.transform, false);
        var faRt = fillArea.GetComponent<RectTransform>();
        faRt.anchorMin = faRt.anchorMax = new Vector2(0.5f, 0.5f);
        faRt.sizeDelta = new Vector2(760, 14);

        var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(fillArea.transform, false);
        var fillRt = fill.GetComponent<RectTransform>();
        fillRt.sizeDelta = Vector2.zero;
        fill.GetComponent<Image>().color = new Color(0.35f, 0.8f, 0.45f);

        // 滑块
        var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
        handleArea.transform.SetParent(go.transform, false);
        var haRt = handleArea.GetComponent<RectTransform>();
        haRt.anchorMin = haRt.anchorMax = new Vector2(0.5f, 0.5f);
        haRt.sizeDelta = new Vector2(760, 30);

        var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handle.transform.SetParent(handleArea.transform, false);
        var hRt = handle.GetComponent<RectTransform>();
        hRt.sizeDelta = new Vector2(26, 30);
        handle.GetComponent<Image>().color = new Color(0.85f, 0.95f, 0.85f);

        var slider = go.GetComponent<Slider>();
        slider.fillRect = fillRt;
        slider.handleRect = hRt;
        slider.targetGraphic = handle.GetComponent<Image>();
        slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
        slider.minValue = min;
        slider.maxValue = max;
        slider.value = value;
        slider.onValueChanged.AddListener(v => onChange?.Invoke(v));

        return slider;
    }
}
