using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// 腾讯云开发（CloudBase）排行榜客户端。
/// 通过「HTTP 访问服务」调用云函数 snake：GET /top、POST /submit。
///
/// 部署步骤见 docs/排行榜部署.md，拿到访问地址后填到 endpoint（或代码里的 DefaultEndpoint）。
/// </summary>
public class LeaderboardClient : MonoBehaviour
{
    public static LeaderboardClient Instance { get; private set; }

    [Tooltip("云函数 HTTP 访问地址，例如 https://xxx.service.tcloudbase.com/snake")]
    public string endpoint = DefaultEndpoint;

    private const string DefaultEndpoint = "https://straight-cheetah-d4e7wzr06f62287-1542870096.ap-shanghai.app.tcloudbase.com/snake";

    public bool LocalReady => true;                       // 本地榜永远可用
    public bool Ready => !string.IsNullOrEmpty(endpoint) && endpoint.StartsWith("http");
    public string LastSource { get; private set; } = "本地";
    public string Error { get; private set; }
    public string PlayerName { get; private set; } = "玩家";

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (string.IsNullOrEmpty(endpoint)) endpoint = DefaultEndpoint;

        string saved = GameSettings.PlayerName;
        if (!string.IsNullOrWhiteSpace(saved)) PlayerName = Sanitize(saved);

        Debug.Log(Ready ? "[排行榜] 已配置：" + endpoint : "[排行榜] 未配置地址，排行榜不可用");
    }

    public Task<bool> EnsureReadyAsync() => Task.FromResult(Ready);

    // ---------------- 数据结构 ----------------
    [Serializable]
    public class ScoreEntry
    {
        public int rank;
        public string name;
        public int score;
        public float seconds;
    }

    [Serializable]
    private class TopResponse
    {
        public bool ok;
        public string error;
        public ScoreEntry[] entries;
    }

    [Serializable]
    private class SubmitResponse
    {
        public bool ok;
        public string error;
        public bool updated;
    }

    [Serializable]
    private class SubmitPayload
    {
        public string board;
        public string name;
        public int score;
        public float seconds;
    }

    // ---------------- 本地存储 ----------------
    [Serializable]
    private class LocalEntry
    {
        public string board;
        public string name;
        public int score;
        public float seconds;
    }

    [Serializable]
    private class LocalStore
    {
        public List<LocalEntry> entries = new List<LocalEntry>();
    }

    private string LocalPath => System.IO.Path.Combine(Application.persistentDataPath, "leaderboard.json");

    private LocalStore LoadLocal()
    {
        try
        {
            if (!System.IO.File.Exists(LocalPath)) return new LocalStore();
            var text = System.IO.File.ReadAllText(LocalPath);
            return JsonUtility.FromJson<LocalStore>(text) ?? new LocalStore();
        }
        catch { return new LocalStore(); }
    }

    private void SaveLocal(LocalStore store)
    {
        try { System.IO.File.WriteAllText(LocalPath, JsonUtility.ToJson(store)); }
        catch (Exception e) { Debug.LogWarning("[排行榜] 本地保存失败：" + e.Message); }
    }

    private void SaveLocalEntry(string board, string name, int score, float seconds)
    {
        var store = LoadLocal();
        var old = store.entries.Find(e => e.board == board && e.name == name);
        if (old != null)
        {
            bool better = score > old.score || (score == old.score && seconds < old.seconds);
            if (!better) return;
            old.score = score;
            old.seconds = seconds;
        }
        else
        {
            store.entries.Add(new LocalEntry { board = board, name = name, score = score, seconds = seconds });
        }

        // 每个榜最多保留 50 条
        store.entries.Sort((a, b) => a.board != b.board ? string.CompareOrdinal(a.board, b.board)
                              : (b.score != a.score ? b.score - a.score : a.seconds.CompareTo(b.seconds)));
        var counts = new Dictionary<string, int>();
        var keep = new List<LocalEntry>();
        foreach (var e in store.entries)
        {
            counts.TryGetValue(e.board, out int c);
            if (c >= 50) continue;
            counts[e.board] = c + 1;
            keep.Add(e);
        }
        store.entries = keep;
        SaveLocal(store);
    }

    private List<ScoreEntry> LocalTop(string board, int limit)
    {
        var store = LoadLocal();
        var list = store.entries.FindAll(e => e.board == board);
        list.Sort((a, b) => b.score != a.score ? b.score - a.score : a.seconds.CompareTo(b.seconds));

        var result = new List<ScoreEntry>();
        for (int i = 0; i < list.Count && i < limit; i++)
            result.Add(new ScoreEntry { rank = i + 1, name = list[i].name, score = list[i].score, seconds = list[i].seconds });
        return result;
    }

    // ---------------- 接口 ----------------
    public async Task<List<ScoreEntry>> TopAsync(int difficulty, int mapSize, int limit = 10)
    {
        string board = BoardId(difficulty, mapSize);

        if (Ready)
        {
            string url = $"{endpoint.TrimEnd('/')}/top?board={board}&limit={limit}";
            var resp = await SendAsync<TopResponse>(url, null);
            if (resp != null && resp.ok)
            {
                Error = null;
                LastSource = "云端";
                if (resp.entries != null && resp.entries.Length > 0)
                    return new List<ScoreEntry>(resp.entries);
            }
        }

        LastSource = "本地";
        return LocalTop(board, limit);
    }

    public async Task<bool> SubmitAsync(int difficulty, int mapSize, int score, float seconds)
    {
        string board = BoardId(difficulty, mapSize);

        // 本地一定记录（离线也能看）
        SaveLocalEntry(board, PlayerName, score, seconds);

        if (!Ready) { Error = null; return true; }

        var payload = new SubmitPayload
        {
            board = board,
            name = PlayerName,
            score = score,
            seconds = seconds
        };

        string url = $"{endpoint.TrimEnd('/')}/submit";
        var resp = await SendAsync<SubmitResponse>(url, JsonUtility.ToJson(payload));
        if (resp == null) return false;
        if (!resp.ok) { Error = resp.error; return false; }

        Error = null;
        Debug.Log($"[排行榜] 提交 {payload.board} = {score}（{seconds:0.0}s）");
        return true;
    }

    public async Task<bool> SetPlayerNameAsync(string name)
    {
        string clean = Sanitize(name);
        if (string.IsNullOrEmpty(clean)) { Error = "昵称不能为空"; return false; }

        PlayerName = clean;
        GameSettings.PlayerName = clean;
        Error = null;
        await Task.Yield();
        return true;
    }

    public static string BoardId(int difficulty, int mapSize) => $"snake_d{difficulty}_m{mapSize}";

    public static string Sanitize(string name)
    {
        if (string.IsNullOrEmpty(name)) return "";
        name = name.Replace("\n", "").Replace("\r", "").Replace("\t", " ").Trim();
        if (name.Length > 16) name = name.Substring(0, 16);
        return name;
    }

    // ---------------- HTTP ----------------
    private async Task<T> SendAsync<T>(string url, string jsonBody) where T : class
    {
        using var req = new UnityWebRequest(url, jsonBody == null ? "GET" : "POST");
        if (jsonBody != null)
        {
            byte[] body = Encoding.UTF8.GetBytes(jsonBody);
            req.uploadHandler = new UploadHandlerRaw(body);
            req.SetRequestHeader("Content-Type", "application/json");
        }
        req.downloadHandler = new DownloadHandlerBuffer();
        req.timeout = 15;

        var op = req.SendWebRequest();
        while (!op.isDone) await Task.Yield();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Error = $"网络错误：{req.responseCode} {req.error}";
            Debug.LogWarning("[排行榜] " + Error);
            return null;
        }

        try
        {
            return JsonUtility.FromJson<T>(req.downloadHandler.text);
        }
        catch (Exception e)
        {
            Error = "解析失败：" + e.Message;
            Debug.LogWarning("[排行榜] " + Error + " / " + req.downloadHandler.text);
            return null;
        }
    }
}
