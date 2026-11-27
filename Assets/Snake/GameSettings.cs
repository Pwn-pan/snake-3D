using UnityEngine;

/// <summary>
/// 全局设置：BGM 音量、音效音量、蛇的速度。用 PlayerPrefs 持久化。
/// </summary>
public static class GameSettings
{
    private const string K_BGM = "snake.bgmVolume";
    private const string K_SFX = "snake.sfxVolume";
    private const string K_SPEED = "snake.moveInterval";
    private const string K_VIEW = "snake.fixedView";
    private const string K_CAM = "snake.cameraSmoothing";
    private const string K_NAME = "snake.playerName";
    private const string K_HEAD = "snake.headColor";
    private const string K_BODY = "snake.bodyColor";
    private const string K_OUTLINE = "snake.outlineColor";
    private const string K_MAP = "snake.mapSize";
    private const string K_DIFF = "snake.difficulty";

    public const float MinInterval = 0.15f;   // 最快
    public const float MaxInterval = 1.20f;   // 最慢

    public static float BgmVolume
    {
        get => PlayerPrefs.GetFloat(K_BGM, 0.55f);
        set { PlayerPrefs.SetFloat(K_BGM, Mathf.Clamp01(value)); PlayerPrefs.Save(); }
    }

    public static float SfxVolume
    {
        get => PlayerPrefs.GetFloat(K_SFX, 0.75f);
        set { PlayerPrefs.SetFloat(K_SFX, Mathf.Clamp01(value)); PlayerPrefs.Save(); }
    }

    /// <summary>true = 固定视角（相机跟在蛇头斜后方）；false = 自由视角（左键环绕）。</summary>
    public static bool FixedView
    {
        get => PlayerPrefs.GetInt(K_VIEW, 1) == 1;
        set { PlayerPrefs.SetInt(K_VIEW, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    /// <summary>难度 0=简单(20%) 1=普通(30%) 2=困难(50%)。</summary>
    public static int Difficulty
    {
        get
        {
            int v = PlayerPrefs.GetInt(K_DIFF, 1);
            return Mathf.Clamp(v, 0, 2);
        }
        set { PlayerPrefs.SetInt(K_DIFF, Mathf.Clamp(value, 0, 2)); PlayerPrefs.Save(); }
    }

    public static string DifficultyName =>
        Difficulty == 0 ? "简单 20%" : Difficulty == 1 ? "普通 30%" : "困难 50%";

    /// <summary>初始蛇长：简单 1、普通 3、困难 5。</summary>
    public static int InitialLength => Difficulty == 0 ? 1 : Difficulty == 1 ? 3 : 5;

    /// <summary>目标分数占总格数的比例。</summary>
    public static float DifficultyPercent =>
        Difficulty == 0 ? 0.20f : Difficulty == 1 ? 0.30f : 0.50f;

    private static Color GetColor(string key, Color fallback)
    {
        string hex = PlayerPrefs.GetString(key, "");
        if (string.IsNullOrEmpty(hex)) return fallback;
        return ColorUtility.TryParseHtmlString(hex, out var c) ? c : fallback;
    }

    private static void SetColor(string key, Color c) =>
        PlayerPrefs.SetString(key, "#" + ColorUtility.ToHtmlStringRGB(c));

    public static Color HeadColor
    {
        get => GetColor(K_HEAD, new Color(0.52f, 0.90f, 0.62f));
        set { SetColor(K_HEAD, value); PlayerPrefs.Save(); }
    }

    public static Color BodyColor
    {
        get => GetColor(K_BODY, new Color(0.30f, 0.66f, 0.88f));
        set { SetColor(K_BODY, value); PlayerPrefs.Save(); }
    }

    public static Color OutlineColor
    {
        get => GetColor(K_OUTLINE, new Color(0.85f, 0.16f, 0.16f));
        set { SetColor(K_OUTLINE, value); PlayerPrefs.Save(); }
    }

    /// <summary>玩家昵称（排行榜显示用）。</summary>
    public static string PlayerName
    {
        get => PlayerPrefs.GetString(K_NAME, "");
        set { PlayerPrefs.SetString(K_NAME, value ?? ""); PlayerPrefs.Save(); }
    }

    /// <summary>镜头跟随速度：越小越稳（拖沓），越大越跟手（容易晃）。</summary>
    public static float CameraSmoothing
    {
        get => Mathf.Clamp(PlayerPrefs.GetFloat(K_CAM, 4.5f), 0.5f, 20f);
        set { PlayerPrefs.SetFloat(K_CAM, Mathf.Clamp(value, 0.5f, 20f)); PlayerPrefs.Save(); }
    }

    /// <summary>地图边长：3 / 5 / 7。</summary>
    public static int MapSize
    {
        get
        {
            int v = PlayerPrefs.GetInt(K_MAP, 5);
            return (v == 3 || v == 5 || v == 7) ? v : 5;
        }
        set
        {
            int v = (value == 3 || value == 5 || value == 7) ? value : 5;
            PlayerPrefs.SetInt(K_MAP, v);
            PlayerPrefs.Save();
        }
    }

    /// <summary>每格移动间隔（秒）：越小越快。</summary>
    public static float MoveInterval
    {
        get => Mathf.Clamp(PlayerPrefs.GetFloat(K_SPEED, 0.5f), MinInterval, MaxInterval);
        set { PlayerPrefs.SetFloat(K_SPEED, Mathf.Clamp(value, MinInterval, MaxInterval)); PlayerPrefs.Save(); }
    }

    /// <summary>把 0~1 的滑块值映射成间隔：滑块越大 → 速度越快 → 间隔越小。</summary>
    public static float SliderToInterval(float t01) => Mathf.Lerp(MaxInterval, MinInterval, Mathf.Clamp01(t01));

    /// <summary>反向映射，用于把已保存的间隔还原成滑块位置。</summary>
    public static float IntervalToSlider(float interval) =>
        Mathf.InverseLerp(MaxInterval, MinInterval, Mathf.Clamp(interval, MinInterval, MaxInterval));
}
