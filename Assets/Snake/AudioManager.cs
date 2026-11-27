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

    /// <summary>欢快的循环 BGM：C 大调、108 BPM。</summary>
    private static AudioClip BuildBgm()
    {
        const float bpm = 108f;
        const float beat = 60f / bpm;
        const int bars = 8;
        const int beatsPerBar = 4;
        float totalSec = bars * beatsPerBar * beat;
        int total = Mathf.RoundToInt(totalSec * Rate);

        var data = new float[total];
        var rng = new System.Random(20261128);

        float[][] chords =
        {
            new[] { 261.63f, 329.63f, 392.00f },
            new[] { 196.00f, 246.94f, 293.66f },
            new[] { 220.00f, 261.63f, 329.63f },
            new[] { 174.61f, 220.00f, 261.63f }
        };
        float[] bassRoots = { 130.81f, 98.00f, 110.00f, 87.31f };

        float[] scale = { 523.25f, 587.33f, 659.25f, 783.99f, 880.00f, 1046.50f };
        int[] melody = { 0, 2, 4, 2, 3, 4, 5, 4, 3, 2, 1, 2, 3, 4, 2, 0 };
        int[] restMask = { 1, 1, 0, 1, 1, 0, 1, 1, 1, 0, 1, 1, 0, 1, 1, 1 };

        int eighth = Mathf.RoundToInt(beat * 0.5f * Rate);

        for (int bar = 0; bar < bars; bar++)
        {
            int chordIdx = bar % chords.Length;
            float[] chord = chords[chordIdx];
            float bass = bassRoots[chordIdx];
            int barStart = Mathf.RoundToInt(bar * beatsPerBar * beat * Rate);
            int barLen = Mathf.RoundToInt(beatsPerBar * beat * Rate);

            for (int i = 0; i < barLen && barStart + i < total; i++)
            {
                float t = i / (float)Rate;
                float env = Mathf.Min(1f, t / 0.12f) * Mathf.Min(1f, (barLen / (float)Rate - t) / 0.25f);
                float v = 0f;
                foreach (float f in chord)
                {
                    v += Mathf.Sin(2f * Mathf.PI * f * t) * 0.45f;
                    v += Mathf.Sin(2f * Mathf.PI * f * 2f * t) * 0.18f;
                }
                data[barStart + i] += v * env * 0.06f;
            }

            for (int n = 0; n < 8; n++)
            {
                int bs = barStart + n * eighth;
                int len = Mathf.RoundToInt(eighth * 0.7f);
                float freq = (n % 4 == 2) ? bass * 2f : bass;
                float amp = (n % 2 == 0) ? 0.16f : 0.10f;

                for (int i = 0; i < len && bs + i < total; i++)
                {
                    float t = i / (float)Rate;
                    float env = Mathf.Exp(-9f * t) * Mathf.Min(1f, t * 200f);
                    float v = Mathf.Sin(2f * Mathf.PI * freq * t)
                            + Mathf.Sin(2f * Mathf.PI * freq * 2f * t) * 0.3f;
                    data[bs + i] += v * env * amp;
                }
            }

            for (int n = 0; n < 8; n++)
            {
                int idx = (n + bar * 8) % melody.Length;
                if (restMask[idx] == 0) continue;

                float freq = scale[melody[idx]];
                int ms = barStart + n * eighth;
                int len = Mathf.RoundToInt(eighth * 0.85f);

                for (int i = 0; i < len && ms + i < total; i++)
                {
                    float t = i / (float)Rate;
                    float env = Mathf.Exp(-6.5f * t) * Mathf.Min(1f, t * 250f);
                    float v = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.75f
                            + Mathf.Sin(2f * Mathf.PI * freq * 2f * t) * 0.22f
                            + Mathf.Sin(2f * Mathf.PI * freq * 3f * t) * 0.08f;
                    data[ms + i] += v * env * 0.13f;
                }
            }

            for (int b = 0; b < beatsPerBar; b++)
            {
                int ks = Mathf.RoundToInt((bar * beatsPerBar + b) * beat * Rate);

                if (b == 0 || b == 2)
                {
                    int len = Mathf.RoundToInt(0.14f * Rate);
                    for (int i = 0; i < len && ks + i < total; i++)
                    {
                        float t = i / (float)Rate;
                        float env = Mathf.Exp(-26f * t);
                        float freq = 85f - 30f * t;
                        data[ks + i] += Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.17f;
                    }
                }

                if (b == 1 || b == 3)
                {
                    int len = Mathf.RoundToInt(0.09f * Rate);
                    for (int i = 0; i < len && ks + i < total; i++)
                    {
                        float t = i / (float)Rate;
                        float env = Mathf.Exp(-30f * t);
                        float n2 = (float)(rng.NextDouble() * 2.0 - 1.0);
                        data[ks + i] += n2 * env * 0.07f;
                    }
                }

                int hs = Mathf.RoundToInt((bar * beatsPerBar + b + 0.5f) * beat * Rate);
                int hLen = Mathf.RoundToInt(0.04f * Rate);
                for (int i = 0; i < hLen && hs + i < total; i++)
                {
                    float t = i / (float)Rate;
                    float env = Mathf.Exp(-55f * t);
                    float n2 = (float)(rng.NextDouble() * 2.0 - 1.0);
                    data[hs + i] += n2 * env * 0.045f;
                }
            }
        }

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
