using UnityEngine;

/// <summary>皮肤：头 / 身 / 描边 / 眼睛 + 可选花纹。</summary>
public class Skin
{
    public string name;
    public Color head;
    public Color body;
    public Color outline;
    public Color eye;
    public Texture2D pattern;     // 花纹（可为空）
    public Texture2D face;        // 头脸贴图（可为空）
    public float tiling = 1f;
    public bool noEyes;           // 不画眼睛

    public Skin(string n, Color h, Color b, Color o, Color e, Texture2D p = null, float t = 1f)
    {
        name = n;
        head = h;
        body = b;
        outline = o;
        eye = e;
        pattern = p;
        tiling = t;
    }

    static Color HslToRgb(float h, float s, float l)
    {
        h = (h % 360f) / 360f;
        float c = (1f - Mathf.Abs(2f * l - 1f)) * s;
        float x = c * (1f - Mathf.Abs((h * 6f) % 2f - 1f));
        float m = l - c / 2f;

        float r = 0f, g = 0f, b = 0f;
        switch (Mathf.FloorToInt(h * 6f) % 6)
        {
            case 0: r = c; g = x; break;
            case 1: r = x; g = c; break;
            case 2: g = c; b = x; break;
            case 3: g = x; b = c; break;
            case 4: r = x; b = c; break;
            default: r = c; b = x; break;
        }
        return new Color(r + m, g + m, b + m, 1f);
    }

    public static Color H(float h, float s, float l) => HslToRgb(h, s, l);

    // ---------------- 花纹生成（全部程序画，不依赖外部图片） ----------------
    public enum Pattern { None, Stripes, Checker, Dots, Zigzag, Diagonal }

    public static Texture2D MakePattern(Pattern p, int size = 64)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Bilinear;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)size;
                float v = y / (float)size;
                float val = 1f;

                switch (p)
                {
                    case Pattern.Stripes:
                        val = (Mathf.Repeat(u * 6f, 1f) < 0.5f) ? 0.35f : 1f;
                        break;
                    case Pattern.Checker:
                        val = ((Mathf.FloorToInt(u * 4f) + Mathf.FloorToInt(v * 4f)) % 2 == 0) ? 0.3f : 1f;
                        break;
                    case Pattern.Dots:
                        {
                            float cx = Mathf.Repeat(u * 4f, 1f) - 0.5f;
                            float cy = Mathf.Repeat(v * 4f, 1f) - 0.5f;
                            val = (cx * cx + cy * cy < 0.045f) ? 0.25f : 1f;
                            break;
                        }
                    case Pattern.Zigzag:
                        {
                            float zig = Mathf.Abs(Mathf.Repeat(u * 4f + v * 2f, 1f) - 0.5f);
                            val = zig < 0.18f ? 0.3f : 1f;
                            break;
                        }
                    case Pattern.Diagonal:
                        val = (Mathf.Repeat((u + v) * 5f, 1f) < 0.35f) ? 0.4f : 1f;
                        break;
                }

                tex.SetPixel(x, y, new Color(val, val, val, 1f));
            }
        }
        tex.Apply();
        return tex;
    }
}

public static class Skins
{
    static Texture2D P(Skin.Pattern p) => Skin.MakePattern(p);

    public static readonly Skin[] All =
    {
        // 0：初始皮肤
        new Skin("纯白",
                 new Color(0.95f, 0.96f, 0.97f),
                 new Color(0.42f, 0.45f, 0.49f),
                 new Color(0.97f, 0.98f, 1.00f),
                 new Color(0.42f, 0.45f, 0.49f)),

        // 1：牢大（脸贴图 + 棕色身体，无眼睛）
        new Skin("牢大",
                 new Color(0.55f, 0.38f, 0.26f),
                 new Color(0.42f, 0.28f, 0.18f),
                 new Color(0.78f, 0.62f, 0.48f),
                 new Color(0.42f, 0.28f, 0.18f))
        {
            face = Resources.Load<Texture2D>("laoda"),
            noEyes = true
        }
    };

    /// <summary>临时：全部解锁用于试色。改回 false 即恢复"只开第一款"。</summary>
    public const bool UnlockAllForTesting = true;

    /// <summary>目前只解锁第一款；后续解锁逻辑接在这里。</summary>
    public static bool IsUnlocked(int index) => UnlockAllForTesting || index == 0;

    public static Skin Get(int index) =>
        (index >= 0 && index < All.Length) ? All[index] : All[0];
}
