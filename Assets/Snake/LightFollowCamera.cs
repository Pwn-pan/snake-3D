using UnityEngine;

/// <summary>
/// 让主光源跟随相机：光的方向始终相对视角固定（左上前方来光）。
/// 挂到 Main Camera 上；也可以挂在任意物体上，会自动找 Camera.main。
/// 优先接管场景里已有的 Directional Light，没有就自己建一个。
/// </summary>
public class LightFollowCamera : MonoBehaviour
{
    [Tooltip("相对相机的光照角度：X 越大越顶光，Y 越大越侧光")]
    public Vector3 relativeAngles = new Vector3(38f, -24f, 0f);

    [Tooltip("平滑跟随，0 表示立即对齐")]
    public float smoothing = 6f;

    [Tooltip("主光强度")]
    public float intensity = 1.15f;

    [Tooltip("是否保留蛇头点光（由 SnakeGame 创建）")]
    public bool keepHeadLight = true;

    private Transform cam;
    private Light sun;
    private Quaternion targetRotation;

    void Start()
    {
        cam = GetComponent<Camera>() != null ? transform : (Camera.main != null ? Camera.main.transform : null);

        // 找一个方向光接管
        foreach (var l in FindObjectsOfType<Light>())
        {
            if (l.type == LightType.Directional) { sun = l; break; }
        }

        if (sun == null)
        {
            var go = new GameObject("Sun (Follow Camera)");
            sun = go.AddComponent<Light>();
            sun.type = LightType.Directional;
        }

        sun.intensity = intensity;
        sun.color = new Color(1f, 0.96f, 0.88f);
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = 0.75f;
        sun.shadowBias = 0.02f;
        sun.shadowNormalBias = 0.2f;

        // 方向光只看旋转，放到相机附近方便在 Scene 视图里看到
        sun.transform.SetParent(cam, false);
        sun.transform.localPosition = new Vector3(0f, 3f, 0f);

        if (cam != null) targetRotation = cam.rotation * Quaternion.Euler(relativeAngles);
    }

    void LateUpdate()
    {
        if (sun == null || cam == null) return;

        targetRotation = cam.rotation * Quaternion.Euler(relativeAngles);

        if (smoothing <= 0f)
            sun.transform.rotation = targetRotation;
        else
            sun.transform.rotation = Quaternion.Slerp(sun.transform.rotation, targetRotation, Time.deltaTime * smoothing);

        sun.intensity = intensity;

        // 阴影跟随视角：保证看哪儿都有影子
        QualitySettings.shadowDistance = Mathf.Max(30f, 60f);
    }
}
