using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 一键生成可运行场景并打包 Windows 版本。
/// 菜单：Snake → Setup Scene / Build Windows
/// 命令行：-executeMethod SnakeBuild.BuildWindows
/// </summary>
public static class SnakeBuild
{
    private const string ScenePath = "Assets/Scenes/Game.unity";
    private static readonly string BuildDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds", "Win64"));
    private const string ExeName = "Snake3D.exe";

    [MenuItem("Snake/Setup Scene")]
    public static void SetupScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // 方向光
        var lightGo = new GameObject("Directional Light");
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.15f;
        light.shadows = LightShadows.Soft;
        lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        // 相机（含环绕 + 跟随光源）
        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.03f, 0.04f, 0.06f);
        camGo.transform.position = new Vector3(0f, 7f, -12f);
        camGo.AddComponent<AudioListener>();
        camGo.AddComponent<CameraOrbit>();
        camGo.AddComponent<LightFollowCamera>();

        // 游戏总管
        var game = new GameObject("Game");
        game.AddComponent<SnakeGame>();
        game.AddComponent<MenuUI>();
        game.AddComponent<AudioManager>();
        game.AddComponent<LeaderboardClient>();

        Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[SnakeBuild] 场景已生成：" + ScenePath);
    }

    [MenuItem("Snake/Build Windows")]
    public static void BuildWindows()
    {
        SetupScene();

        PlayerSettings.productName = "Snake3D";
        PlayerSettings.companyName = "pwn";
        PlayerSettings.defaultScreenWidth = 1600;
        PlayerSettings.defaultScreenHeight = 900;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.resizableWindow = true;
        PlayerSettings.runInBackground = true;

        Directory.CreateDirectory(BuildDir);
        Debug.Log("[SnakeBuild] 输出目录 -> " + BuildDir);

        var options = new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = Path.Combine(BuildDir, ExeName),
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        };

        var report = BuildPipeline.BuildPlayer(options);
        var summary = report.summary;

        Debug.Log($"[SnakeBuild] 结果={summary.result} 大小={summary.totalSize} 耗时={summary.totalTime}");

        if (summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
        {
            Debug.LogError("[SnakeBuild] 打包失败");
            EditorApplication.Exit(1);
        }
        EditorApplication.Exit(0);
    }
}
