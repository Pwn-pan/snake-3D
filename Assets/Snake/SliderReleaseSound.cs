using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 滑块松手时播一次音效（拖动过程中不响）。
/// 挂到任意 Slider 的 GameObject 上即可。
/// </summary>
public class SliderReleaseSound : MonoBehaviour, IPointerUpHandler
{
    public void OnPointerUp(PointerEventData eventData)
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayTurn();
    }
}
