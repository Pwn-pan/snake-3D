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
    private AudioSource eatSource;
    private AudioClip bgmClip, eatClip, turnClip, overClip, countClip, lastClip;
    private AudioClip bgmDefault, bgmLaoda;      // 两套背景乐
    private AudioClip eatDefault, eatLaoda;      // 两套吃食物音效
    private AudioClip boostInClip, boostOutClip, winClip, bumpClip, laodaTurnClip;
    private AudioClip boostInLaoda, boostOutLaoda, overLaoda, winLaoda;

    // 渐入渐出
    private float bgmTargetVolume;      // 目标音量（= 设置值）
    private float bgmFadeMul = 1f;      // 当前淡入淡出系数 0~1
    private float bgmFadeSpeed = 0f;    // 每秒变化量
    private float overFade = 1f;        // 结算音效的淡入淡出系数
    private float overFadeSpeed = 0f;   // 恒正
    private int overFadeDir = 0;        // 1=淡入 -1=淡出 0=保持
    private AudioSource overSource;     // 结算用独立音源
    private AudioSource boostInReverse; // 倒放加速音效

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        bgmFadeMul = 0f;              // 一开始就是静音，等 Start 淡入
        bgm = gameObject.AddComponent<AudioSource>();
        bgm.loop = true;
        bgm.playOnAwake = false;
        bgm.spatialBlend = 0f;

        sfx = gameObject.AddComponent<AudioSource>();
        sfx.loop = false;
        sfx.playOnAwake = false;
        sfx.spatialBlend = 0f;

        // 倒放音源（加速音效倒着播）
        boostInReverse = gameObject.AddComponent<AudioSource>();
        boostInReverse.loop = false;
        boostInReverse.playOnAwake = false;
        boostInReverse.spatialBlend = 0f;

        // 结算音：独立音源，支持淡入淡出
        overSource = gameObject.AddComponent<AudioSource>();
        overSource.loop = false;
        overSource.playOnAwake = false;
        overSource.spatialBlend = 0f;
        overSource.volume = 0f;

        // 吃食物：叠加播放，不打断上一声
        eatSource = gameObject.AddComponent<AudioSource>();
        eatSource.loop = false;
        eatSource.playOnAwake = false;
        eatSource.spatialBlend = 0f;

        // 两套 BGM：程序生成的原版 + 外部导入的
        bgmDefault = BuildBgm();
        bgmLaoda = Resources.Load<AudioClip>("bgm");

        // 两套吃食物音效
        eatDefault = BuildBlip("eat", new[] { 660f, 880f }, 0.14f, 0.55f);
        eatLaoda = Resources.Load<AudioClip>("eat_sound");

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
        bgmClip = IsLaodaSkin() ? bgmLaoda : bgmDefault;
        if (bgmClip == null) bgmClip = bgmDefault;

        bgm.clip = bgmClip;
        bgmFadeMul = 0f;              // 从静音开始
        bgm.volume = 0f;
        bgm.Play();
        FadeInBgm();                  // 渐入
    }

    void Update()
    {
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);   // 限幅：避免开场长帧一帧完成淡入

        // BGM：目标音量 × 淡入淡出系数（速度恒为正，方向由目标决定）
        if (bgmFadeSpeed > 0.0001f)
        {
            bgmFadeMul = Mathf.MoveTowards(bgmFadeMul, bgmFadeTarget, bgmFadeSpeed * dt);
            if (Mathf.Approximately(bgmFadeMul, bgmFadeTarget)) bgmFadeSpeed = 0f;
        }

        if (bgm != null)
        {
            bgm.volume = GameSettings.BgmVolume * bgmFadeMul;

            // 淡出到 0 后如果是换曲待办，这里执行切换
            if (bgmFadeMul <= 0.001f && pendingBgm != null)
            {
                bgmClip = pendingBgm;
                pendingBgm = null;
                bgm.clip = bgmClip;
                bgm.Play();
                FadeInBgm();
            }
        }

        // 结算音：淡入 → 保持 → 末尾淡出
        if (overSource != null && overSource.isPlaying)
        {
            if (overFadeDir > 0)
            {
                overFade = Mathf.MoveTowards(overFade, 1f, overFadeSpeed * dt);
                if (overFade >= 0.999f) overFadeDir = 0;
            }
            else if (overFadeDir < 0)
            {
                overFade = Mathf.MoveTowards(overFade, 0f, overFadeSpeed * dt);
                if (overFade <= 0.001f) { overSource.Stop(); overFadeDir = 0; }
            }
            else
            {
                float remain = overSource.clip != null ? overSource.clip.length - overSource.time : 99f;
                if (remain <= overFadeOutLen) { overFadeDir = -1; overFadeSpeed = 1f / overFadeOutLen; }
            }

            overSource.volume = GameSettings.SfxVolume * overFade;
        }

        if (sfx != null) sfx.volume = GameSettings.SfxVolume;
    }

    private float bgmFadeTarget = 1f;
    private AudioClip pendingBgm;
    private const float BgmFadeLen = 1.6f;
    private const float OverFadeInLen = 0.35f;
    private float overFadeOutLen = 1.2f;

    void FadeInBgm()
    {
        bgmFadeTarget = 1f;
        bgmFadeSpeed = 1f / BgmFadeLen;
    }

    /// <summary>BGM 淡出到静音后切到指定曲目，再淡入。</summary>
    void SwitchBgmFaded(AudioClip next)
    {
        if (next == null) next = bgmDefault;

        if (bgm == null) { bgmClip = next; return; }

        // 目标就是当前这首：取消待切换，直接淡回来（快速来回切时关键）
        if (bgmClip == next)
        {
            pendingBgm = null;
            if (bgmFadeMul < 0.999f)
            {
                bgmFadeTarget = 1f;
                bgmFadeSpeed = 1f / BgmFadeLen;
            }
            return;
        }

        pendingBgm = next;
        bgmFadeTarget = 0f;
        bgmFadeSpeed = 1f / (BgmFadeLen * 0.6f);    // 恒正，方向靠 target=0
    }

    public void PlayEat()
    {
        var clip = IsLaodaSkin() ? eatLaoda : eatDefault;
        if (clip == null || eatSource == null) return;

        eatSource.volume = Mathf.Clamp01(GameSettings.SfxVolume) * 0.9f;
        eatSource.PlayOneShot(clip);         // 独立音源：不会覆盖/打断
    }

    /// <summary>按当前皮肤切换 BGM（切皮肤后调用）。</summary>
    public void RefreshBgm()
    {
        var want = IsLaodaSkin() ? bgmLaoda : bgmDefault;
        SwitchBgmFaded(want);
    }
    public void PlayTurn()
    {
        // 牢大皮肤：用专属点击音效
        if (IsLaodaSkin())
        {
            if (laodaTurnClip == null) laodaTurnClip = Resources.Load<AudioClip>("turn_laoda");
            if (laodaTurnClip != null) { OneShot(laodaTurnClip, 0.9f); return; }
        }
        OneShot(turnClip, 0.6f);
    }

    static bool IsLaodaSkin() => Skins.Get(GameSettings.SkinIndex).name == "牢大";
    /// <summary>用支持淡入淡出的音源播放一段音频。</summary>
    void PlayFaded(AudioClip clip)
    {
        if (clip == null || overSource == null) return;

        overSource.Stop();
        overSource.clip = clip;
        overFade = 0f;
        overFadeDir = 1;
        overFadeSpeed = 1f / OverFadeInLen;
        overFadeOutLen = Mathf.Min(1.2f, clip.length * 0.35f);
        overSource.volume = 0f;
        overSource.Play();
    }

    public void PlayOver()
    {
        AudioClip clip = null;

        if (IsLaodaSkin())
        {
            if (overLaoda == null) overLaoda = Resources.Load<AudioClip>("win_laoda");
            clip = overLaoda;
        }
        if (clip == null) clip = overClip;

        if (clip == null || overSource == null) return;

        // 渐入 + 末尾自动渐出
        overSource.Stop();
        overSource.clip = clip;
        overFade = 0f;
        overFadeDir = 1;
        overFadeSpeed = 1f / OverFadeInLen;
        overFadeOutLen = Mathf.Min(1.2f, clip.length * 0.35f);
        overSource.volume = 0f;
        overSource.Play();
    }
    public void PlayWin()
    {
        if (IsLaodaSkin())
        {
            if (winLaoda == null) winLaoda = Resources.Load<AudioClip>("over_laoda");
            if (winLaoda != null)
            {
                // 走带淡入淡出的独立音源
                PlayFaded(winLaoda);
                return;
            }
        }
        OneShot(winClip, 1f);
    }
    public void PlayBoostIn()
    {
        if (IsLaodaSkin())
        {
            if (boostInLaoda == null) boostInLaoda = Resources.Load<AudioClip>("boost_in_laoda");
            if (boostInLaoda != null)
            {
                if (boostInReverse != null) boostInReverse.pitch = 1f;
                OneShot(boostInLaoda, 0.9f);
                return;
            }
        }
        OneShot(boostInClip, 0.7f);
    }
    public void PlayBoostOut()
    {
        if (IsLaodaSkin())
        {
            // 倒放「加速」那段，形成"收回去"的效果
            if (boostInLaoda == null) boostInLaoda = Resources.Load<AudioClip>("boost_in_laoda");
            if (boostInLaoda != null && boostInReverse != null)
            {
                boostInReverse.Stop();
                boostInReverse.clip = boostInLaoda;
                boostInReverse.pitch = -1f;                        // 负数 = 倒放
                boostInReverse.time = boostInLaoda.length - 0.02f; // 关键：播放头先跳到末尾
                boostInReverse.volume = GameSettings.SfxVolume * 0.9f;
                boostInReverse.Play();
                return;
            }
        }
        OneShot(boostOutClip, 0.5f);
    }
    public void PlayBump()
    {
        if (IsLaodaSkin())
        {
            if (laodaTurnClip == null) laodaTurnClip = Resources.Load<AudioClip>("turn_laoda");
            if (laodaTurnClip != null) { OneShot(laodaTurnClip, 1f); return; }
        }
        OneShot(bumpClip, 0.9f);
    }

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
