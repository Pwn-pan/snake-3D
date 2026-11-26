using UnityEngine;

/// <summary>
/// 程序生成音频：一段循环 BGM + 若干短音效。
/// 不需要任何音频素材，音量跟随 GameSettings。
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    private AudioSource bgm;
    private AudioSource sfx;
    private AudioClip bgmClip, eatClip, turnClip, overClip, countClip, lastClip;
    private AudioClip boostInClip, boostOutClip, winClip, bumpClip;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        bgm = gameObject.AddComponent<AudioSource>();
        bgm.loop = true;
        bgm.playOnAwake = false;
        bgm.spatialBlend = 0f;

        sfx = gameObject.AddComponent<AudioSource>();
        sfx.loop = false;
        sfx.playOnAwake = false;
        sfx.spatialBlend = 0f;

        bgmClip = BuildBgm();
        eatClip = BuildBlip("eat", new[] { 660f, 880f }, 0.14f, 0.55f);
        turnClip = BuildBlip("turn", new[] { 420f }, 0.06f, 0.22f);
        countClip = BuildBlip("count", new[] { 520f }, 0.12f, 0.4f);
        lastClip = BuildBlip("go", new[] { 780f, 1040f }, 0.25f, 0.5f);
        overClip = BuildBlip("over", new[] { 440f, 330f, 220f }, 0.6f, 0.5f);
        boostInClip = BuildBlip("boostIn", new[] { 520f, 780f, 1040f }, 0.22f, 0.42f);
        boostOutClip = BuildBlip("boostOut", new[] { 1040f, 780f, 520f }, 0.22f, 0.32f);
        winClip = BuildBlip("win", new[] { 523.25f, 659.25f, 783.99f, 1046.5f }, 0.9f, 0.55f);
        bumpClip = BuildBlip("bump", new[] { 150f, 95f }, 0.14f, 0.5f);
    }

    void Start()
    {
        bgm.clip = bgmClip;
        bgm.volume = GameSettings.BgmVolume;
        bgm.Play();
    }

    void Update()
    {
        if (bgm != null) bgm.volume = GameSettings.BgmVolume;
        if (sfx != null) sfx.volume = GameSettings.SfxVolume;
    }

    public void PlayEat() => OneShot(eatClip, 0.9f);
    public void PlayTurn() => OneShot(turnClip, 0.6f);
    public void PlayOver() => OneShot(overClip, 1f);
    public void PlayWin() => OneShot(winClip, 1f);
    public void PlayBoostIn() => OneShot(boostInClip, 0.7f);
    public void PlayBoostOut() => OneShot(boostOutClip, 0.5f);
    public void PlayBump() => OneShot(bumpClip, 0.9f);
    public void PlayCount() => OneShot(countClip, 0.8f);
    public void PlayGo() => OneShot(lastClip, 1f);

    private void OneShot(AudioClip clip, float scale)
    {
        if (clip == null || sfx == null) return;
        sfx.PlayOneShot(clip, Mathf.Clamp01(scale));
    }

    // ---------------- 合成 ----------------
    private const int Rate = 44100;

    /// <summary>生成一段 16 秒的多层循环：铺底和弦 + 贝斯 + 旋律 + 轻打击。</summary>
    private static AudioClip BuildBgm()
    {
        const float bpm = 78f;
        const float beat = 60f / bpm;          // 一拍时长
        const int bars = 8;                    // 8 小节
        const int beatsPerBar = 4;
        float totalSec = bars * beatsPerBar * beat;
        int total = Mathf.RoundToInt(totalSec * Rate);

        var data = new float[total];
        var rng = new System.Random(20251126);

        // 和弦进行：Am - F - C - G（每两小节换一次）
        float[][] chords =
        {
            new[] { 220.00f, 261.63f, 329.63f },   // Am
            new[] { 174.61f, 220.00f, 261.63f },   // F
            new[] { 261.63f, 329.63f, 392.00f },   // C
            new[] { 196.00f, 246.94f, 293.66f }    // G
        };
        float[] bassRoots = { 110.00f, 87.31f, 130.81f, 98.00f };   // A2 F2 C3 G2

        // 五声音阶（A 小调）：A C D E G
        float[] scale = { 440.00f, 523.25f, 587.33f, 659.25f, 783.99f, 880.00f };
        int[] melody = { 0, 2, 4, 3, 2, 0, 3, 4, 5, 4, 2, 3, 1, 0, 2, 3 };
        int[] restMask = { 1, 0, 1, 1, 0, 1, 1, 0, 1, 1, 0, 1, 1, 0, 1, 1 }; // 0=休止

        int eighth = Mathf.RoundToInt(beat * 0.5f * Rate);

        for (int bar = 0; bar < bars; bar++)
        {
            int chordIdx = (bar / 2) % chords.Length;
            float[] chord = chords[chordIdx];
            float bass = bassRoots[chordIdx];
            int barStart = Mathf.RoundToInt(bar * beatsPerBar * beat * Rate);

            // ---- 铺底和弦（整小节，慢起慢落）----
            int barLen = Mathf.RoundToInt(beatsPerBar * beat * Rate);
            for (int i = 0; i < barLen && barStart + i < total; i++)
            {
                float t = i / (float)Rate;
                float env = Mathf.Min(1f, t / 0.35f) * Mathf.Min(1f, (barLen / (float)Rate - t) / 0.5f);
                float v = 0f;
                foreach (float f in chord)
                {
                    v += Mathf.Sin(2f * Mathf.PI * f * t) * 0.5f;
                    v += Mathf.Sin(2f * Mathf.PI * f * 2f * t) * 0.12f;
                }
                data[barStart + i] += v * env * 0.075f;
            }

            // ---- 贝斯（每拍一下，第 1、3 拍更重）----
            for (int b = 0; b < beatsPerBar; b++)
            {
                int bs = Mathf.RoundToInt((bar * beatsPerBar + b) * beat * Rate);
                int len = Mathf.RoundToInt(beat * 0.85f * Rate);
                float amp = (b % 2 == 0) ? 0.20f : 0.13f;
                for (int i = 0; i < len && bs + i < total; i++)
                {
                    float t = i / (float)Rate;
                    float env = Mathf.Exp(-3.2f * t) * Mathf.Min(1f, t * 90f);
                    float v = Mathf.Sin(2f * Mathf.PI * bass * t)
                            + Mathf.Sin(2f * Mathf.PI * bass * 2f * t) * 0.35f;
                    data[bs + i] += v * env * amp;
                }
            }

            // ---- 旋律（八分音符，带休止）----
            for (int n = 0; n < 8; n++)
            {
                int idx = (n + bar * 8) % melody.Length;
                if (restMask[idx] == 0) continue;

                float freq = scale[melody[idx]];
                int ms = barStart + n * eighth;
                int len = Mathf.RoundToInt(eighth * 0.9f);
                float amp = 0.14f;

                for (int i = 0; i < len && ms + i < total; i++)
                {
                    float t = i / (float)Rate;
                    float env = Mathf.Exp(-4.5f * t) * Mathf.Min(1f, t * 120f);
                    float v = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.8f
                            + Mathf.Sin(2f * Mathf.PI * freq * 2f * t) * 0.15f;
                    data[ms + i] += v * env * amp;
                }
            }

            // ---- 轻打击：底鼓 + 沙锤 ----
            for (int b = 0; b < beatsPerBar; b++)
            {
                int ks = Mathf.RoundToInt((bar * beatsPerBar + b) * beat * Rate);
                if (b == 0 || b == 2)
                {
                    int len = Mathf.RoundToInt(0.16f * Rate);
                    for (int i = 0; i < len && ks + i < total; i++)
                    {
                        float t = i / (float)Rate;
                        float env = Mathf.Exp(-22f * t);
                        float freq = 70f - 25f * t;    // 轻微下滑
                        data[ks + i] += Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.16f;
                    }
                }

                // 反拍沙锤（噪声 + 极短包络）
                int sh = Mathf.RoundToInt((bar * beatsPerBar + b + 0.5f) * beat * Rate);
                int shLen = Mathf.RoundToInt(0.045f * Rate);
                for (int i = 0; i < shLen && sh + i < total; i++)
                {
                    float t = i / (float)Rate;
                    float env = Mathf.Exp(-45f * t);
                    float n = (float)(rng.NextDouble() * 2.0 - 1.0);
                    data[sh + i] += n * env * 0.05f;
                }
            }
        }

        // 归一化 + 首尾淡入淡出，保证循环不爆音
        float peak = 0.0001f;
        for (int i = 0; i < total; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        float gain = 0.85f / peak;
        for (int i = 0; i < total; i++) data[i] *= gain;

        int fade = Mathf.RoundToInt(0.03f * Rate);
        for (int i = 0; i < fade; i++)
        {
            float k = i / (float)fade;
            data[i] *= k;
            data[total - 1 - i] *= k;
        }

        var clip = AudioClip.Create("bgm", total, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>生成一个短促的音效（可多个频率依次播放）。</summary>
    private static AudioClip BuildBlip(string name, float[] freqs, float duration, float amp)
    {
        int perNote = Mathf.RoundToInt(duration / freqs.Length * Rate);
        int total = perNote * freqs.Length;
        var data = new float[total];

        for (int n = 0; n < freqs.Length; n++)
        {
            for (int i = 0; i < perNote; i++)
            {
                float t = i / (float)Rate;
                float env = Mathf.Exp(-6f * t) * Mathf.Min(1f, t * 200f);
                float v = Mathf.Sin(2f * Mathf.PI * freqs[n] * t) * amp;
                data[n * perNote + i] = v * env;
            }
        }

        var clip = AudioClip.Create(name, total, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
