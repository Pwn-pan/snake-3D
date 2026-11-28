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

    private GameObject menuPanel, settingsPanel, hudPanel, overPanel, countdownObj, pausePanel, difficultyPanel, mapPanel, leaderboardPanel, skinPanel;
    private System.Collections.Generic.List<GameObject> skinTiles;
    private ScrollRect skinScrollRect;
    private Text scoreText, overScoreText, countdownText, overTitle;
    private Text bgmLabel, sfxLabel, speedLabel, viewLabel, camLabel, viewButtonLabel;
    private Text lbListText, lbStatusText, lbDiffText, lbMapText, lbNameStatus;
    private int lbDifficulty = 2, lbMap = 5;
    private InputField lbNameInput;
    private bool submitQueued;
    private Slider bgmSlider, sfxSlider, speedSlider, camSlider;
    private RectTransform settingsContent;
    private RawImage menuBackground;
    private Texture2D menuBgTex;
    private Image flashImage;
    private float flashAlpha;
    private class ClosingPanel { public GameObject go; public float t; }
    private readonly System.Collections.Generic.List<ClosingPanel> closingPanels = new System.Collections.Generic.List<ClosingPanel>();

    private GameObject animTarget;
    private float animT = 1f;
    private float animDur = 0.2f;
    private Sprite roundedSprite;
    private Sprite circleSprite;
    private Sprite gradientSprite;
    private float settingsContentHeight = 1100f;
    private ScrollRect settingsScrollRect;
    private float settingsMaxScroll = 0f;
    private RectTransform settingsViewport;
    private Image hintTop;
    private Image hintBottom;
    private float hintTopAlpha;
    private float hintBottomAlpha;

    private int lastCountdownStep = -1;

    void Awake()
    {
        font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Arial" }, 40);
        if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    void BuildSprites()
    {
        roundedSprite = MakeRoundedSprite(64, 18);
        circleSprite = MakeCircleSprite(64);
        gradientSprite = MakeGradientSprite(64);
    }

    static Sprite MakeRoundedSprite(int size, int radius)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float px = x + 0.5f, py = y + 0.5f;
                float dx = Mathf.Max(radius - px, px - (size - radius));
                float dy = Mathf.Max(radius - py, py - (size - radius));
                dx = Mathf.Max(0f, dx);
                dy = Mathf.Max(0f, dy);
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(radius - d + 0.5f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();

        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                             SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
    }

    static Sprite MakeCircleSprite(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;

        float r = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                float a = Mathf.Clamp01(r - d + 0.5f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    static Sprite MakeGradientSprite(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        for (int y = 0; y < size; y++)
        {
            float t = y / (float)(size - 1);
            var c = Color.Lerp(new Color(0.75f, 0.75f, 0.75f, 1f), new Color(1f, 1f, 1f, 1f), t);
            for (int x = 0; x < size; x++) tex.SetPixel(x, y, c);
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    void Start()
    {
        game = FindObjectOfType<SnakeGame>();

        if (FindObjectOfType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            es.transform.SetParent(transform, false);
        }

        BuildSprites();
        BuildUI();
        foreach (var panel in new[] { settingsPanel, skinPanel, leaderboardPanel, difficultyPanel, mapPanel, pausePanel, overPanel })
            if (panel != null) panel.SetActive(false);
        ShowMenu();

        game.OnBumped += () =>
        {
            flashAlpha = 0.28f;
            var cam = FindObjectOfType<CameraOrbit>();
            if (cam != null) cam.Shake(0.18f, 0.22f);
        };
    }

    void BuildFlash(Transform canvas)
    {
        var go = new GameObject("Flash", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(canvas, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        flashImage = go.GetComponent<Image>();
        flashImage.color = new Color(1f, 0.15f, 0.15f, 0f);
        flashImage.raycastTarget = false;
    }

    void BuildMenuBackground(Transform canvas)
    {
        // 生成一张渐变纹理，铺满全屏
        const int size = 128;
        menuBgTex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        menuBgTex.wrapMode = TextureWrapMode.Repeat;
        menuBgTex.filterMode = FilterMode.Bilinear;

        Color deepBlue = new Color(0.10f, 0.16f, 0.30f);
        Color midBlue = new Color(0.18f, 0.30f, 0.52f);
        Color glow = new Color(0.38f, 0.62f, 0.88f);

        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float t = (x / (float)(size - 1)) * 0.55f + (y / (float)(size - 1)) * 0.45f;
                Color c = Color.Lerp(deepBlue, midBlue, t);

                // 一点柔和光斑
                float dx = (x - size * 0.7f) / size;
                float dy = (y - size * 0.75f) / size;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float spot = Mathf.Clamp01(1f - d * 1.8f);
                c = Color.Lerp(c, glow, spot * 0.55f);

                px[y * size + x] = c;
            }
        }
        menuBgTex.SetPixels(px);
        menuBgTex.Apply();

        var go = new GameObject("MenuBackground", typeof(RectTransform), typeof(RawImage));
        go.transform.SetParent(canvas, false);
        go.transform.SetAsFirstSibling();     // 放到最底层

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        menuBackground = go.GetComponent<RawImage>();
        menuBackground.texture = menuBgTex;
        menuBackground.uvRect = new Rect(0f, 0f, 1f, 1f);
        menuBackground.raycastTarget = false;   // 不挡按钮
    }

    void Update()
    {
        UpdatePanelAnim();
        UpdateClosing();

        if (game == null) return;

        // 菜单背景：缓慢流动
        if (menuBackground != null)
        {
            var uv = menuBackground.uvRect;
            uv.x += Time.deltaTime * 0.012f;
            uv.y += Time.deltaTime * 0.006f;
            menuBackground.uvRect = uv;

            bool inMenu = game.state == SnakeGame.GameState.Menu;   // 只在菜单里显示，游戏/倒计时不覆盖画面

            if (menuBackground.gameObject.activeSelf != inMenu)
                menuBackground.gameObject.SetActive(inMenu);
        }


        // 越界提示：已经到顶/到底还继续滚
        if (settingsPanel != null && settingsPanel.activeSelf && settingsScrollRect != null)
        {
            float w = Input.mouseScrollDelta.y;
            if (Mathf.Abs(w) > 0.01f)
            {
                float v = settingsScrollRect.verticalNormalizedPosition;
                if (v >= 0.999f) hintTopAlpha = 0.9f;
                if (v <= 0.001f) hintBottomAlpha = 0.9f;
            }
        }

        // 越界提示淡出
        if (hintTopAlpha > 0f || hintBottomAlpha > 0f)
        {
            hintTopAlpha = Mathf.Max(0f, hintTopAlpha - Time.deltaTime * 2.2f);
            hintBottomAlpha = Mathf.Max(0f, hintBottomAlpha - Time.deltaTime * 2.2f);

            if (hintTop != null) hintTop.color = new Color(0.82f, 0.84f, 0.87f, hintTopAlpha);
            if (hintBottom != null) hintBottom.color = new Color(0.82f, 0.84f, 0.87f, hintBottomAlpha);
        }

        // 撞击红闪衰减
        if (flashImage != null)
        {
            flashAlpha = Mathf.Max(0f, flashAlpha - Time.deltaTime * 1.6f);
            flashImage.color = new Color(1f, 0.15f, 0.15f, flashAlpha);
        }

        // Esc：在任何菜单面板里都能直接退回主菜单
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            bool anyPanel = (settingsPanel != null && settingsPanel.activeSelf)
                         || (skinPanel != null && skinPanel.activeSelf)
                         || (leaderboardPanel != null && leaderboardPanel.activeSelf)
                         || (difficultyPanel != null && difficultyPanel.activeSelf)
                         || (mapPanel != null && mapPanel.activeSelf);

            if (anyPanel && game.state != SnakeGame.GameState.Playing)
            {
                ShowMenu();
                return;
            }
        }

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

        bool wantPause = playing && game.paused;
        if (wantPause && !pausePanel.activeSelf) ActivatePanel(pausePanel);
        else if (!wantPause && pausePanel.activeSelf) pausePanel.SetActive(false);
        scoreText.text = $"得分  {game.score} / {game.targetScore}" + (game.targetReached ? "　✔ 目标达成" : "");

        if (game.state == SnakeGame.GameState.GameOver && !submitQueued)
        {
            submitQueued = true;
            SubmitScore(game.score, game.runSeconds);
        }

        if (game.state == SnakeGame.GameState.GameOver && !overPanel.activeSelf)
        {
            ActivatePanel(overPanel);
            overScoreText.text = $"得分 {game.score} / {game.targetScore}";
            overTitle.text = game.won ? "胜利！" : "游戏结束";
            overTitle.color = game.won
                ? new Color(1f, 0.86f, 0.35f)
                : new Color(1f, 0.55f, 0.55f);
        }
    }

    async void SubmitScore(int score, float seconds)
    {
        if (GameSettings.Difficulty != 2) return;   // 只有困难模式上榜
        var client = LeaderboardClient.Instance;
        if (client == null) return;

        bool ok = await client.SubmitAsync(GameSettings.Difficulty, GameSettings.MapSize, score, seconds);
        Debug.Log(ok
            ? $"[排行榜] 已提交 {score} 分（{seconds:0.0}s）"
            : "[排行榜] 提交失败：" + client.Error);
    }

    // ---------------- 按钮响应 ----------------
    public void ShowMenu()
    {
        if (game != null) game.StartDemo();   // 菜单里跑自动演示

        ActivatePanel(menuPanel);
        ClosePanel(settingsPanel);
        ClosePanel(skinPanel);
        ClosePanel(leaderboardPanel);
        ClosePanel(difficultyPanel);
        ClosePanel(mapPanel);
        overPanel.SetActive(false);
        hudPanel.SetActive(false);
        countdownObj.SetActive(false);
        if (pausePanel != null) pausePanel.SetActive(false);
        if (difficultyPanel != null) difficultyPanel.SetActive(false);
        if (mapPanel != null) mapPanel.SetActive(false);
        if (leaderboardPanel != null) leaderboardPanel.SetActive(false);
    }

    void OpenLeaderboard()
    {
        menuPanel.SetActive(false);
        settingsPanel.SetActive(false);
        ActivatePanel(leaderboardPanel);
        lbDifficulty = 2;   // 只做困难榜
        if (lbNameInput != null) lbNameInput.text = GameSettings.PlayerName;
        lbMap = GameSettings.MapSize;
        RefreshLeaderboard();
    }

    async void RefreshLeaderboard()
    {
        lbDiffText.text = "难度：困难（仅困难模式有排行榜）";
        lbMapText.text = $"地图：{lbMap}×{lbMap}×{lbMap}";
        lbListText.text = "加载中…";
        lbStatusText.text = "";

        var client = LeaderboardClient.Instance;
        if (client == null)
        {
            lbStatusText.text = "排行榜未初始化（场景里缺少 LeaderboardClient）";
            lbListText.text = "";
            return;
        }

        // 云端没配也能用本地榜，不拦

        var entries = await client.TopAsync(lbDifficulty, lbMap, 10);
        if (entries == null)
        {
            lbListText.text = "";
            lbStatusText.text = "读取失败：" + (string.IsNullOrEmpty(client.Error) ? "未知错误" : client.Error);
            return;
        }
        if (entries.Count == 0)
        {
            lbListText.text = "";
            lbStatusText.text = "这个榜单还没有成绩";
            return;
        }

        var sb = new System.Text.StringBuilder();
        foreach (var e in entries)
        {
            string name = string.IsNullOrEmpty(e.name) ? "匿名玩家" : e.name;
            sb.AppendLine($"{e.rank,2}.  {name,-12} {e.score,4} 分　{e.seconds:0} 秒");
        }
        lbListText.text = sb.ToString();
        lbStatusText.text = $"共 {entries.Count} 条 · {client.LastSource}榜";
    }

    async void SaveNickname()
    {
        var client = LeaderboardClient.Instance;
        if (client == null) { lbNameStatus.text = "排行榜未初始化"; return; }

        string raw = lbNameInput != null ? lbNameInput.text : "";
        string clean = LeaderboardClient.Sanitize(raw);
        if (string.IsNullOrEmpty(clean)) { lbNameStatus.text = "昵称不能为空"; return; }

        lbNameStatus.text = "保存中…";
        bool ok = await client.SetPlayerNameAsync(clean);
        lbNameStatus.text = ok ? "已保存：" + clean : "保存失败：" + client.Error;
        if (ok) RefreshLeaderboard();
    }

    void ShowDifficulty()
    {
        menuPanel.SetActive(false);
        settingsPanel.SetActive(false);
        overPanel.SetActive(false);
        ActivatePanel(difficultyPanel);
        mapPanel.SetActive(false);
    }

    void ShowMapSelect()
    {
        difficultyPanel.SetActive(false);
        ActivatePanel(mapPanel);
    }

    void StartGameClicked()
    {
        if (game == null) return;
        menuPanel.SetActive(false);
        settingsPanel.SetActive(false);
        overPanel.SetActive(false);
        difficultyPanel.SetActive(false);
        mapPanel.SetActive(false);
        if (leaderboardPanel != null) leaderboardPanel.SetActive(false);
        if (pausePanel != null) pausePanel.SetActive(false);
        submitQueued = false;
        game.StopDemo();
        game.StartGame();
    }

    void OpenSettings()
    {
        menuPanel.SetActive(false);
        ActivatePanel(settingsPanel);
        if (settingsScrollRect != null) settingsScrollRect.verticalNormalizedPosition = 1f;

        bgmSlider.SetValueWithoutNotify(GameSettings.BgmVolume);
        sfxSlider.SetValueWithoutNotify(GameSettings.SfxVolume);
        speedSlider.SetValueWithoutNotify(GameSettings.IntervalToSlider(GameSettings.MoveInterval));
        if (camSlider != null) camSlider.SetValueWithoutNotify(GameSettings.CameraSmoothing);
        RefreshLabels();
    }

    void CloseSettings()
    {
        ClosePanel(settingsPanel);
        ActivatePanel(menuPanel);
        return;

        settingsPanel.SetActive(false);
        menuPanel.SetActive(true);
    }

    Text RowLabel(Transform parent, string text, float y)
    {
        var t = Label(parent, text, 30, new Vector2(-300, y), new Vector2(360, 60), FontStyle.Normal,
                      new Color(0.9f, 0.95f, 1f), TextAnchor.MiddleLeft);
        var trt = t.rectTransform;
        trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 1f);
        trt.pivot = new Vector2(0.5f, 1f);
        trt.anchoredPosition = new Vector2(-300, y);
        return t;
    }

    GameObject RowButton(Transform parent, string text, float y, System.Action onClick)
    {
        var go = new GameObject("Row_" + text, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(240, y);
        rt.sizeDelta = new Vector2(440, 72);

        go.GetComponent<Image>().color = new Color(0.22f, 0.24f, 0.27f, 0.94f);
        var btn = go.GetComponent<Button>();
        var colors = btn.colors;
        colors.highlightedColor = new Color(0.34f, 0.37f, 0.41f);
        colors.pressedColor = new Color(0.16f, 0.17f, 0.19f);
        btn.colors = colors;
        btn.onClick.AddListener(() =>
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayTurn();
            onClick?.Invoke();
        });

        if (!string.IsNullOrEmpty(text))
            Label(go.transform, text, 30, Vector2.zero, rt.sizeDelta, FontStyle.Bold, Color.white);

        return go;
    }

    Image MakeHint(Transform parent, Vector2 pos)
    {
        var go = new GameObject("Hint", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(1560f, 14f);

        var img = go.GetComponent<Image>();
        img.sprite = roundedSprite;
        img.type = Image.Type.Sliced;
        img.color = new Color(0.82f, 0.84f, 0.87f, 0f);
        img.raycastTarget = false;
        return img;
    }

    /// <summary>动画时长由设置里的「镜头速度」决定：越大越快。</summary>
    float AnimDuration()
    {
        float v = 10f;   // 固定用 10 档，不再跟随设置
        return Mathf.Lerp(0.55f, 0.12f, Mathf.InverseLerp(0.5f, 20f, v));
    }

    void ActivatePanel(GameObject panel)
    {
        if (panel == null) return;

        // 清掉其它面板可能残留的半透明
        foreach (var other in new[] { menuPanel, settingsPanel, skinPanel, leaderboardPanel, difficultyPanel, mapPanel, pausePanel, overPanel })
        {
            if (other == null || other == panel) continue;
            var og = other.GetComponent<CanvasGroup>();
            if (og != null) og.alpha = 1f;
        }
        if (!panel.activeSelf) panel.SetActive(true);

        var cg = panel.GetComponent<CanvasGroup>();
        if (cg == null) return;

        animTarget = panel;
        animT = 0f;
        animDur = AnimDuration();
        cg.alpha = 0f;

        var rt = panel.GetComponent<RectTransform>();
        if (rt != null) rt.localScale = new Vector3(0.97f, 0.97f, 1f);
    }

    /// <summary>淡出后关闭，时长取决于「镜头速度」设置。</summary>
    void ClosePanel(GameObject panel)
    {
        if (panel == null || !panel.activeSelf) return;

        var cg = panel.GetComponent<CanvasGroup>();
        if (cg == null) { panel.SetActive(false); return; }

        // 避免重复添加
        foreach (var c in closingPanels) if (c.go == panel) return;

        closingPanels.Add(new ClosingPanel { go = panel, t = 0f });
    }

    void UpdateClosing()
    {
        float dur = AnimDuration();

        for (int i = closingPanels.Count - 1; i >= 0; i--)
        {
            var c = closingPanels[i];
            if (c.go == null) { closingPanels.RemoveAt(i); continue; }

            c.t += Time.deltaTime / Mathf.Max(0.05f, dur);
            float k = Mathf.Clamp01(c.t);

            var cg = c.go.GetComponent<CanvasGroup>();
            if (cg != null) cg.alpha = 1f - k;

            var rt = c.go.GetComponent<RectTransform>();
            if (rt != null) rt.localScale = Vector3.Lerp(Vector3.one, new Vector3(0.98f, 0.98f, 1f), k);

            if (c.t >= 1f)
            {
                if (cg != null) cg.alpha = 1f;
                if (rt != null) rt.localScale = Vector3.one;
                c.go.SetActive(false);
                closingPanels.RemoveAt(i);
            }
        }
    }

    void UpdatePanelAnim()
    {
        if (animTarget == null) return;

        animT += Time.deltaTime / Mathf.Max(0.05f, animDur);
        float k = Mathf.Clamp01(animT);
        k = 1f - (1f - k) * (1f - k);      // 缓出

        var cg = animTarget.GetComponent<CanvasGroup>();
        if (cg != null) cg.alpha = k;

        var rt = animTarget.GetComponent<RectTransform>();
        if (rt != null) rt.localScale = Vector3.Lerp(new Vector3(0.97f, 0.97f, 1f), Vector3.one, k);

        if (animT >= 1f)
        {
            if (cg != null) cg.alpha = 1f;
            if (rt != null) rt.localScale = Vector3.one;
            animTarget = null;
        }
    }

    void CreateHeader(Transform panel, string title, float width = 1636f, float height = 110f, System.Action onBack = null)
    {
        var go = new GameObject("Header", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(panel, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, 6f);       // 略微超出卡片，避免露出浅色边
        rt.sizeDelta = new Vector2(width, height);

        var img = go.GetComponent<Image>();
        img.sprite = roundedSprite;
        img.type = Image.Type.Sliced;
        img.color = new Color(0.10f, 0.11f, 0.13f, 0.32f);   // 很透明的深灰

        Label(go.transform, title, 52, new Vector2(0, -height * 0.5f), new Vector2(width, height * 0.85f),
              FontStyle.Bold, new Color(0.95f, 0.96f, 0.98f));

        // 左上角返回
        if (onBack != null)
        {
            var backGo = new GameObject("Back", typeof(RectTransform), typeof(Button));
            backGo.transform.SetParent(go.transform, false);
            var brt = backGo.GetComponent<RectTransform>();
            brt.anchorMin = new Vector2(0f, 0.5f);
            brt.anchorMax = new Vector2(0f, 0.5f);
            brt.pivot = new Vector2(0f, 0.5f);
            brt.anchoredPosition = new Vector2(28f, 0f);
            brt.sizeDelta = new Vector2(90f, 90f);

            var btn = backGo.GetComponent<Button>();
            btn.onClick.AddListener(() =>
            {
                if (AudioManager.Instance != null) AudioManager.Instance.PlayTurn();
                onBack();
            });

            Label(backGo.transform, "<", 56, Vector2.zero, new Vector2(90f, 90f), FontStyle.Bold,
                  new Color(0.95f, 0.96f, 0.98f));
        }
    }

    void OpenSkins()
    {
        if (skinScrollRect != null) skinScrollRect.verticalNormalizedPosition = 1f;
        menuPanel.SetActive(false);
        settingsPanel.SetActive(false);
        leaderboardPanel.SetActive(false);
        difficultyPanel.SetActive(false);
        mapPanel.SetActive(false);
        skinPanel.SetActive(true);
        RefreshSkinTiles();
    }

    void MakeBar(Transform parent, Vector2 pos, Color color)
    {
        var go = new GameObject("Bar", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(150f, 22f);

        var img = go.GetComponent<Image>();
        img.sprite = roundedSprite;
        img.type = Image.Type.Sliced;
        img.color = color;
        img.raycastTarget = false;
    }

    void MakeDot(Transform parent, Vector2 pos, Color color)
    {
        var go = new GameObject("Dot", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(34f, 34f);

        var img = go.GetComponent<Image>();
        img.sprite = circleSprite;
        img.color = color;
        img.raycastTarget = false;
    }

    void RefreshSkinTiles()
    {
        if (skinTiles == null) return;
        for (int i = 0; i < skinTiles.Count; i++)
        {
            var img = skinTiles[i].GetComponent<Image>();
            bool unlocked = Skins.IsUnlocked(i);
            bool selected = GameSettings.SkinIndex == i;

            if (!unlocked) img.color = new Color(0.25f, 0.26f, 0.28f, 0.9f);
            else if (selected) img.color = new Color(0.55f, 0.60f, 0.66f, 1f);
            else img.color = Skins.All[i].body;
        }
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
            camLabel.text = $"镜头速度  {GameSettings.CameraSmoothing:0.0}";

        if (viewLabel != null)
            viewLabel.text = "视角";

        if (viewButtonLabel != null)
            viewButtonLabel.text = GameSettings.FixedView ? "固定（跟随蛇头）" : "自由（左键环绕）";

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

        BuildFlash(canvasGo.transform);

        // ---- 主菜单 ----
        menuPanel = Panel(canvasGo.transform, "Menu", false);

        // 主菜单只要一层淡压暗，让背后的自动演示能看见
        var dimGo = new GameObject("Dim", typeof(RectTransform), typeof(Image));
        dimGo.transform.SetParent(menuPanel.transform, false);
        dimGo.transform.SetAsFirstSibling();
        var dimRt = dimGo.GetComponent<RectTransform>();
        dimRt.anchorMin = Vector2.zero;
        dimRt.anchorMax = Vector2.one;
        dimRt.offsetMin = Vector2.zero;
        dimRt.offsetMax = Vector2.zero;
        var dimImg = dimGo.GetComponent<Image>();
        dimImg.color = new Color(0.02f, 0.04f, 0.07f, 0.42f);
        dimImg.raycastTarget = false;
        Label(menuPanel.transform, "3D 贪吃蛇", 96, new Vector2(0, 220), new Vector2(1200, 140), FontStyle.Bold, new Color(0.85f, 1f, 0.85f));
        Label(menuPanel.transform, "A / D 左右转　W / S 持续升降（按 A/D 恢复平飞）　Shift 加速　左键拖动转视角", 26, new Vector2(0, 120), new Vector2(1400, 50), FontStyle.Normal, new Color(0.75f, 0.85f, 0.75f));

        Button(menuPanel.transform, "开始游戏", new Vector2(0, -20), ShowDifficulty);
        Button(menuPanel.transform, "设置", new Vector2(0, -140), OpenSettings);
        Button(menuPanel.transform, "排行榜", new Vector2(0, -260), OpenLeaderboard);
        Button(menuPanel.transform, "皮肤", new Vector2(0, -380), OpenSkins);

        // ---- 设置 ----
        settingsPanel = Panel(canvasGo.transform, "Settings");

        // 标题固定，内容可滚动

        // ---- 滚动视图（ScrollRect：自带范围限制与回弹）----
        var scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(ScrollRect));
        scrollGo.transform.SetParent(settingsPanel.transform, false);
        var srcRt = scrollGo.GetComponent<RectTransform>();
        srcRt.anchorMin = srcRt.anchorMax = new Vector2(0.5f, 0.5f);
        srcRt.pivot = new Vector2(0.5f, 0.5f);
        srcRt.anchoredPosition = new Vector2(0f, -55f);
        srcRt.sizeDelta = new Vector2(1560f, 830f);
        settingsScrollRect = scrollGo.GetComponent<ScrollRect>();

        // 视口（负责裁剪）
        var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D), typeof(Image));
        viewportGo.transform.SetParent(scrollGo.transform, false);
        var vpRt = viewportGo.GetComponent<RectTransform>();
        vpRt.anchorMin = Vector2.zero;
        vpRt.anchorMax = Vector2.one;
        vpRt.offsetMin = Vector2.zero;
        vpRt.offsetMax = Vector2.zero;
        viewportGo.GetComponent<Image>().color = new Color(0, 0, 0, 0);   // 仅供遮罩

        // 内容
        var contentGo = new GameObject("Content", typeof(RectTransform));
        contentGo.transform.SetParent(viewportGo.transform, false);
        settingsContent = contentGo.GetComponent<RectTransform>();
        settingsContent.anchorMin = new Vector2(0f, 1f);
        settingsContent.anchorMax = new Vector2(1f, 1f);
        settingsContent.pivot = new Vector2(0.5f, 1f);
        settingsContent.anchoredPosition = Vector2.zero;
        settingsContent.sizeDelta = new Vector2(0f, 800f);

        settingsScrollRect.viewport = vpRt;
        settingsScrollRect.content = settingsContent;
        settingsScrollRect.horizontal = false;
        settingsScrollRect.vertical = true;
        settingsScrollRect.movementType = ScrollRect.MovementType.Clamped;
        settingsScrollRect.inertia = true;
        settingsScrollRect.decelerationRate = 0.12f;
        settingsScrollRect.scrollSensitivity = 45f;
        settingsScrollRect.elasticity = 0.1f;

        float rowY = -20f;
        float rowStep = 140f;    // 行距加大

        // 每个设置一行：左边名字，右边控件
        bgmLabel = RowLabel(settingsContent, "背景音乐", rowY);
        bgmSlider = Slider(settingsContent, new Vector2(320, rowY), 0f, 1f, GameSettings.BgmVolume, v =>
        {
            GameSettings.BgmVolume = v;
            RefreshLabels();
        });
        rowY -= rowStep;

        sfxLabel = RowLabel(settingsContent, "音效", rowY);
        sfxSlider = Slider(settingsContent, new Vector2(320, rowY), 0f, 1f, GameSettings.SfxVolume, v =>
        {
            GameSettings.SfxVolume = v;
            RefreshLabels();
        });
        sfxSlider.gameObject.AddComponent<SliderReleaseSound>();
        rowY -= rowStep;

        speedLabel = RowLabel(settingsContent, "蛇的速度", rowY);
        speedSlider = Slider(settingsContent, new Vector2(320, rowY), 0f, 1f, GameSettings.IntervalToSlider(GameSettings.MoveInterval), v =>
        {
            GameSettings.MoveInterval = GameSettings.SliderToInterval(v);
            if (game != null) game.MoveInterval = GameSettings.MoveInterval;
            RefreshLabels();
        });
        rowY -= rowStep;

        camLabel = RowLabel(settingsContent, "镜头速度", rowY);
        camSlider = Slider(settingsContent, new Vector2(320, rowY), 0.5f, 20f, GameSettings.CameraSmoothing, v =>
        {
            GameSettings.CameraSmoothing = v;
            RefreshLabels();
        });
        camSlider.gameObject.AddComponent<SliderReleaseSound>();
        rowY -= rowStep;

        viewLabel = RowLabel(settingsContent, "视角", rowY);
        var viewButton = RowButton(settingsContent, "", rowY, () =>
        {
            GameSettings.FixedView = !GameSettings.FixedView;
            RefreshLabels();
            PushCameraMode();
        });
        viewButtonLabel = Label(viewButton.transform, "固定", 30, Vector2.zero,
                                new Vector2(520, 76), FontStyle.Bold, Color.white);

        // 滚动范围
        // 内容总高（最后一行底部 + 余量）
        settingsContentHeight = Mathf.Abs(rowY) + 150f;    // 最后一行下面留 150
        settingsContent.sizeDelta = new Vector2(0f, settingsContentHeight);

        // 顶栏最后创建 → 盖住滚动到上面的内容
        CreateHeader(settingsPanel.transform, "设置", 1636f, 110f, CloseSettings);
        settingsContent.SetAsFirstSibling();

        // ---- 皮肤 ----
        skinPanel = Panel(canvasGo.transform, "Skins");
        CreateHeader(skinPanel.transform, "皮肤", 1636f, 110f, () => { ClosePanel(skinPanel); ActivatePanel(menuPanel); });

        // 滚动区域
        var skinScrollGo = new GameObject("SkinScroll", typeof(RectTransform), typeof(ScrollRect));
        skinScrollGo.transform.SetParent(skinPanel.transform, false);
        var ssRt = skinScrollGo.GetComponent<RectTransform>();
        ssRt.anchorMin = ssRt.anchorMax = new Vector2(0.5f, 0.5f);
        ssRt.anchoredPosition = new Vector2(0f, -55f);
        ssRt.sizeDelta = new Vector2(1560f, 830f);
        skinScrollRect = skinScrollGo.GetComponent<ScrollRect>();

        var skinVp = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D), typeof(Image));
        skinVp.transform.SetParent(skinScrollGo.transform, false);
        var svpRt = skinVp.GetComponent<RectTransform>();
        svpRt.anchorMin = Vector2.zero;
        svpRt.anchorMax = Vector2.one;
        svpRt.offsetMin = Vector2.zero;
        svpRt.offsetMax = Vector2.zero;
        skinVp.GetComponent<Image>().color = new Color(0, 0, 0, 0);

        var skinContentGo = new GameObject("Content", typeof(RectTransform));
        skinContentGo.transform.SetParent(skinVp.transform, false);
        var skc = skinContentGo.GetComponent<RectTransform>();
        skc.anchorMin = new Vector2(0f, 1f);
        skc.anchorMax = new Vector2(1f, 1f);
        skc.pivot = new Vector2(0.5f, 1f);
        skc.anchoredPosition = Vector2.zero;

        skinScrollRect.viewport = svpRt;
        skinScrollRect.content = skc;
        skinScrollRect.horizontal = false;
        skinScrollRect.vertical = true;
        skinScrollRect.movementType = ScrollRect.MovementType.Clamped;
        skinScrollRect.inertia = true;
        skinScrollRect.decelerationRate = 0.12f;
        skinScrollRect.scrollSensitivity = 45f;

        // 生成 3D 预览贴图
        var gameRef = FindObjectOfType<SnakeGame>();
        if (gameRef != null) gameRef.BuildSkinPreviews();
        var previewTex = gameRef != null ? gameRef.SkinPreviewRT : null;

        skinTiles = new System.Collections.Generic.List<GameObject>();

        int cols = 4;
        float tileW = 330f, tileH = 320f, gapX = 14f, gapY = 18f;

        for (int i = 0; i < Skins.All.Length; i++)
        {
            int idx = i;
            var skin = Skins.All[i];
            bool unlocked = Skins.IsUnlocked(i);

            int col = i % cols;
            int row = i / cols;

            var tile = new GameObject("Skin_" + skin.name, typeof(RectTransform), typeof(Image), typeof(Button));
            tile.transform.SetParent(skinContentGo.transform, false);
            var rt = tile.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2((col - (cols - 1) * 0.5f) * (tileW + gapX),
                                             -20f - row * (tileH + gapY));
            rt.sizeDelta = new Vector2(tileW, tileH);

            var img = tile.GetComponent<Image>();
            img.sprite = roundedSprite;
            img.type = Image.Type.Sliced;
            img.color = new Color(0.20f, 0.21f, 0.24f, 0.95f);

            var btn = tile.GetComponent<Button>();
            btn.interactable = unlocked;
            if (unlocked)
                btn.onClick.AddListener(() =>
                {
                    GameSettings.SkinIndex = idx;
                    var g = FindObjectOfType<SnakeGame>();
                    if (g != null) g.ApplyCustomColors();
                    // 皮肤格子点击不播音效
                    RefreshSkinTiles();
                });

            // 3D 预览：从大贴图里取第 i 格
            var rawGo = new GameObject("Preview", typeof(RectTransform), typeof(RawImage));
            rawGo.transform.SetParent(tile.transform, false);
            var rrt = rawGo.GetComponent<RectTransform>();
            rrt.anchorMin = new Vector2(0.5f, 1f);
            rrt.anchorMax = new Vector2(0.5f, 1f);
            rrt.pivot = new Vector2(0.5f, 1f);
            rrt.anchoredPosition = new Vector2(0f, -12f);
            rrt.sizeDelta = new Vector2(230f, 230f);

            var raw = rawGo.GetComponent<RawImage>();
            raw.texture = previewTex;
            float h = 1f / Skins.All.Length;
            raw.uvRect = new Rect(0f, 1f - (i + 1) * h, 1f, h);   // 竖排取第 i 片
            raw.raycastTarget = false;
            raw.color = unlocked ? Color.white : new Color(0.35f, 0.36f, 0.38f, 0.85f);

            // 名字（顶部对齐，固定在预览下方）
            var nameText = Label(tile.transform, unlocked ? skin.name : "未解锁", 26, Vector2.zero,
                                 new Vector2(tileW, 40), FontStyle.Bold,
                                 unlocked ? Color.white : new Color(0.68f, 0.70f, 0.73f));
            var nrt = nameText.rectTransform;
            nrt.anchorMin = nrt.anchorMax = new Vector2(0.5f, 1f);
            nrt.pivot = new Vector2(0.5f, 1f);
            nrt.anchoredPosition = new Vector2(0f, -250f);

            skinTiles.Add(tile);
        }

        // 内容高度 = 行数
        int rows = Mathf.CeilToInt(Skins.All.Length / (float)cols);
        skc.sizeDelta = new Vector2(0f, 20f + rows * (tileH + gapY) + 40f);


        // ---- 排行榜 ----
        leaderboardPanel = Panel(canvasGo.transform, "Leaderboard");
        Label(leaderboardPanel.transform, "排行榜", 72, new Vector2(0, 400), new Vector2(900, 110), FontStyle.Bold, Color.white);

        lbDiffText = Label(leaderboardPanel.transform, "难度：困难（仅困难模式有排行榜）", 28, new Vector2(-380, 350), new Vector2(700, 45), FontStyle.Normal, new Color(1f, 0.86f, 0.35f), TextAnchor.MiddleLeft);

        // 昵称
        lbNameInput = InputField(leaderboardPanel.transform, new Vector2(-120, 285), new Vector2(420, 56), "输入昵称（最多 16 字）", GameSettings.PlayerName);
        Button(leaderboardPanel.transform, "保存昵称", new Vector2(230, 285), SaveNickname, 220f);
        lbNameStatus = Label(leaderboardPanel.transform, "", 22, new Vector2(0, 240), new Vector2(1200, 36), FontStyle.Normal, new Color(0.8f, 0.85f, 0.8f));

        lbMapText = Label(leaderboardPanel.transform, "地图：5×5×5", 30, new Vector2(-380, 185), new Vector2(420, 45), FontStyle.Normal, Color.white, TextAnchor.MiddleLeft);
        Button(leaderboardPanel.transform, "切地图", new Vector2(200, 185), () =>
        {
            lbMap = lbMap == 3 ? 5 : lbMap == 5 ? 7 : 3;
            RefreshLeaderboard();
        }, 240f);

        lbListText = Label(leaderboardPanel.transform, "加载中…", 30, new Vector2(0, -30), new Vector2(1200, 340), FontStyle.Normal, new Color(0.9f, 0.95f, 1f), TextAnchor.UpperLeft);
        lbStatusText = Label(leaderboardPanel.transform, "", 24, new Vector2(0, -250), new Vector2(1200, 60), FontStyle.Normal, new Color(0.8f, 0.8f, 0.8f), TextAnchor.MiddleCenter);

        Button(leaderboardPanel.transform, "刷新", new Vector2(-200, -340), RefreshLeaderboard, 240f);
        Button(leaderboardPanel.transform, "返回", new Vector2(200, -340), ShowMenu, 240f);

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
            // 全屏压暗
            var img = go.AddComponent<Image>();
            img.color = new Color(0.02f, 0.03f, 0.05f, 0.72f);

            // 中间圆角卡片
            var card = new GameObject("Card", typeof(RectTransform), typeof(Image));
            card.transform.SetParent(go.transform, false);
            var crt = card.GetComponent<RectTransform>();
            crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
            crt.sizeDelta = new Vector2(1620, 980);

            var cardImg = card.GetComponent<Image>();
            cardImg.sprite = roundedSprite;
            cardImg.type = Image.Type.Sliced;
            cardImg.color = new Color(0.62f, 0.64f, 0.68f, 0.10f);   // 很淡的灰


            // 卡片描边
            var border = new GameObject("Border", typeof(RectTransform), typeof(Image));
            border.transform.SetParent(card.transform, false);
            border.transform.SetAsFirstSibling();
            var brt = border.GetComponent<RectTransform>();
            brt.anchorMin = Vector2.zero;
            brt.anchorMax = Vector2.one;
            brt.offsetMin = new Vector2(-3, -3);
            brt.offsetMax = new Vector2(3, 3);

            var bImg = border.GetComponent<Image>();
            bImg.sprite = roundedSprite;
            bImg.type = Image.Type.Sliced;
            bImg.color = new Color(0.75f, 0.77f, 0.80f, 0.10f);
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
        img.sprite = roundedSprite;
        img.type = Image.Type.Sliced;
        img.color = new Color(0.22f, 0.24f, 0.27f, 0.94f);

        var btn = go.GetComponent<Button>();
        var colors = btn.colors;
        colors.normalColor = new Color(0.22f, 0.24f, 0.27f, 1f);
        colors.highlightedColor = new Color(0.34f, 0.37f, 0.41f, 1f);
        colors.pressedColor = new Color(0.16f, 0.17f, 0.19f, 1f);
        colors.selectedColor = new Color(0.34f, 0.37f, 0.41f, 1f);
        colors.fadeDuration = 0.08f;
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
        t.color = color ?? new Color(0.92f, 0.93f, 0.95f);
        t.alignment = anchor;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }

    private InputField InputField(Transform parent, Vector2 pos, Vector2 size, string placeholder, string initial)
    {
        var go = new GameObject("Input", typeof(RectTransform), typeof(Image), typeof(InputField));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        go.GetComponent<Image>().color = new Color(0.10f, 0.13f, 0.16f, 0.95f);

        var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
        textGo.transform.SetParent(go.transform, false);
        var trt = textGo.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(12, 6); trt.offsetMax = new Vector2(-12, -6);
        var t = textGo.GetComponent<Text>();
        t.font = font; t.fontSize = 28; t.color = Color.white;
        t.alignment = TextAnchor.MiddleLeft;
        t.supportRichText = false;

        var phGo = new GameObject("Placeholder", typeof(RectTransform), typeof(Text));
        phGo.transform.SetParent(go.transform, false);
        var prt = phGo.GetComponent<RectTransform>();
        prt.anchorMin = Vector2.zero; prt.anchorMax = Vector2.one;
        prt.offsetMin = new Vector2(12, 6); prt.offsetMax = new Vector2(-12, -6);
        var ph = phGo.GetComponent<Text>();
        ph.font = font; ph.fontSize = 26; ph.color = new Color(0.6f, 0.65f, 0.7f);
        ph.alignment = TextAnchor.MiddleLeft;
        ph.text = placeholder;

        var input = go.GetComponent<InputField>();
        input.textComponent = t;
        input.placeholder = ph;
        input.targetGraphic = go.GetComponent<Image>();
        input.characterLimit = 16;
        input.text = initial ?? "";
        return input;
    }

    private Slider Slider(Transform parent, Vector2 pos, float min, float max, float value, System.Action<float> onChange)
    {
        var go = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(pos.x, pos.y);
        rt.sizeDelta = new Vector2(480, 26);

        // 背景条
        var bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(go.transform, false);
        var bgRt = bg.GetComponent<RectTransform>();
        bgRt.anchorMin = bgRt.anchorMax = new Vector2(0.5f, 0.5f);
        bgRt.sizeDelta = new Vector2(480, 16);
        var bgImg = bg.GetComponent<Image>();
        bgImg.sprite = roundedSprite;
        bgImg.type = Image.Type.Sliced;
        bgImg.color = new Color(0.14f, 0.15f, 0.17f, 0.92f);

        // 填充条
        var fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(go.transform, false);
        var faRt = fillArea.GetComponent<RectTransform>();
        faRt.anchorMin = faRt.anchorMax = new Vector2(0.5f, 0.5f);
        faRt.sizeDelta = new Vector2(480, 16);

        var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(fillArea.transform, false);
        var fillRt = fill.GetComponent<RectTransform>();
        fillRt.sizeDelta = Vector2.zero;
        var fillImg = fill.GetComponent<Image>();
        fillImg.sprite = roundedSprite;
        fillImg.type = Image.Type.Sliced;
        fillImg.color = new Color(0.78f, 0.80f, 0.84f, 1f);

        // 滑块
        var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
        handleArea.transform.SetParent(go.transform, false);
        var haRt = handleArea.GetComponent<RectTransform>();
        haRt.anchorMin = haRt.anchorMax = new Vector2(0.5f, 0.5f);
        haRt.sizeDelta = new Vector2(480, 30);

        var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handle.transform.SetParent(handleArea.transform, false);
        var hRt = handle.GetComponent<RectTransform>();
        hRt.sizeDelta = new Vector2(34, 34);
        var handleImg = handle.GetComponent<Image>();
        handleImg.sprite = circleSprite;
        handleImg.color = new Color(0.95f, 0.96f, 0.98f, 1f);

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
