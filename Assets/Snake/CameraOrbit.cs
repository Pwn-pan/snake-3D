using UnityEngine;

/// <summary>
/// 相机：
///   · 固定视角：跟在蛇头斜后方，蛇头始终在屏幕中心，转向时平滑跟随
///   · 自由视角：左键拖动环绕，滚轮缩放
/// 当前模式由 GameSettings.FixedView 决定（设置面板可切换）。
/// </summary>
public class CameraOrbit : MonoBehaviour
{
    [Header("固定视角")]
    public float followDistance = 7f;     // 相机到蛇头的水平距离
    public float followHeight = 3.5f;     // 相机高度
    public float followSmoothing = 4.5f;  // 平滑度（越大跟得越紧）
    public float followMin = 0.2f;        // 可以无限近（只保留一个安全值）
    public float followMax = 20f;         // 滚轮最远

    [Header("相机视锥（四棱台）")]
    public float cameraFov = 80f;      // 垂直张角，越大看得越广
    public float nearClip = 0.05f;     // 近平面，越小越贴近相机
    public float farClip = 5000f;      // 远平面，越大看得越远

    [Header("自由视角")]
    public float distance = 16f;
    public float yaw = 35f;
    public float pitch = 35f;
    public float rotateSpeed = 4f;
    public float zoomSpeed = 12f;
    public float minDistance = 6f;
    public float maxDistance = 45f;

    private SnakeGame game;
    private Vector3 followVelocity;      // 位置平滑用
    private float smoothYaw;             // 当前环绕角（平滑值）
    private bool yawInitialized;
    private float currentYaw, currentPitch;
    private int lastGridSize = -1;

    void Start()
    {
        followMin = 0.2f;    // 强制覆盖：场景里存的是旧值 3
        followMax = Mathf.Max(2f, followMax);

        // 应用视锥参数
        var cam = GetComponent<Camera>();
        if (cam != null)
        {
            cam.fieldOfView = cameraFov;
            cam.nearClipPlane = nearClip;
            cam.farClipPlane = farClip;
        }

        game = FindObjectOfType<SnakeGame>();
        currentYaw = yaw;
        currentPitch = pitch;

        var euler = transform.eulerAngles;
        currentYaw = euler.y;
        currentPitch = Mathf.Clamp(WrapAngle(euler.x), -85f, 85f);
    }

    static float WrapAngle(float a) => a > 180f ? a - 360f : a;

    /// <summary>切换视角后立刻对齐（由设置面板调用）。</summary>
    public void SnapToMode()
    {
        if (GameSettings.FixedView && game != null)
        {
            Vector3 head = game.HeadWorldPosition;
            Vector3 forward = game.HeadForward;
            smoothYaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
            yawInitialized = true;
            Quaternion orbit = Quaternion.Euler(0f, smoothYaw, 0f);
            transform.position = head + orbit * (Vector3.back * followDistance) + Vector3.up * followHeight;
            transform.rotation = Quaternion.LookRotation((head - transform.position).normalized, Vector3.up);
        }
        else
        {
            yaw = currentYaw;
            pitch = currentPitch;
            Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
            transform.position = rot * Vector3.back * distance;
            transform.rotation = rot;
        }
    }

    void LateUpdate()
    {
        if (GameSettings.FixedView) UpdateFixed();
        else UpdateFree();
    }

    // ---------------- 固定视角 ----------------
    void UpdateFixed()
    {
        if (game == null) game = FindObjectOfType<SnakeGame>();
        if (game == null) return;

        // 滚轮调远近
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.0001f)
        {
            followDistance -= scroll * zoomSpeed;
            if (followDistance > followMax) followDistance = followMax;
            if (followDistance < 0.05f) followDistance = 0.05f;
        }

        Vector3 head = game.HeadWorldPosition;
        Vector3 forward = game.HeadForward;

        // 目标环绕角：蛇头朝向的偏航角
        float targetYaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;

        if (!yawInitialized)
        {
            smoothYaw = targetYaw;
            yawInitialized = true;
        }

        // 旋转角速度：0.5 档 ≈ 105°/s，20 档 ≈ 690°/s
        // 用固定角速度而不是指数插值，最大档也有 0.13 秒的过渡，不会瞬移
        float smoothing = Mathf.Clamp(GameSettings.CameraSmoothing, 0.5f, 20f);
        float degPerSec = 90f + smoothing * 30f;
        smoothYaw = Mathf.MoveTowardsAngle(smoothYaw, targetYaw, degPerSec * Time.deltaTime);

        // 圆形轨道：以蛇头为圆心，半径固定
        Quaternion orbit = Quaternion.Euler(0f, smoothYaw, 0f);
        Vector3 offset = orbit * (Vector3.back * followDistance) + Vector3.up * followHeight;
        transform.position = head + offset;

        // 始终看向蛇头 → 蛇头永远在屏幕中心
        Vector3 lookDir = head - transform.position;
        if (lookDir.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(lookDir.normalized, Vector3.up);
    }

    // ---------------- 自由视角 ----------------
    void UpdateFree()
    {
        bool overUI = UnityEngine.EventSystems.EventSystem.current != null &&
                      UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();

        if (Input.GetMouseButton(0) && !overUI)
        {
            currentYaw += Input.GetAxis("Mouse X") * rotateSpeed;
            currentPitch -= Input.GetAxis("Mouse Y") * rotateSpeed;
            currentPitch = Mathf.Clamp(currentPitch, -85f, 85f);
        }

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.0001f)
            distance = Mathf.Clamp(distance - scroll * zoomSpeed, minDistance, maxDistance);

        // 换地图时自动调整自由视角的默认距离
        if (game != null && game.gridSize != lastGridSize)
        {
            lastGridSize = game.gridSize;
            distance = Mathf.Clamp(game.gridSize * 2.6f, minDistance, maxDistance);
        }

        yaw = currentYaw;
        pitch = currentPitch;

        Vector3 center = game != null ? game.HeadWorldPosition : Vector3.zero;
        // 自由视角以场地中心为轴，菜单里看整体
        center = Vector3.zero;

        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        transform.position = center + rot * Vector3.back * distance;
        transform.rotation = rot;
    }
}
