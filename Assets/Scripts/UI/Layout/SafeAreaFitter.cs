using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(RectTransform))]
public class SafeAreaFitter : MonoBehaviour
{
    [SerializeField] private RectTransform target;

    private Rect lastSafeArea;
    private Vector2Int lastScreenSize;

    public RectTransform Target
    {
        get { return target != null ? target : target = transform as RectTransform; }
    }

    private void OnEnable()
    {
        Apply();
    }

    private void Update()
    {
        if (Screen.safeArea != lastSafeArea || lastScreenSize.x != Screen.width || lastScreenSize.y != Screen.height)
        {
            Apply();
        }
    }

    public void Apply()
    {
        if (Target == null)
        {
            return;
        }

        Rect safeArea = Screen.safeArea;
        lastSafeArea = safeArea;
        lastScreenSize = new Vector2Int(Screen.width, Screen.height);

        Vector2 anchorMin = safeArea.position;
        Vector2 anchorMax = safeArea.position + safeArea.size;
        anchorMin.x /= Mathf.Max(Screen.width, 1);
        anchorMin.y /= Mathf.Max(Screen.height, 1);
        anchorMax.x /= Mathf.Max(Screen.width, 1);
        anchorMax.y /= Mathf.Max(Screen.height, 1);

        Target.anchorMin = anchorMin;
        Target.anchorMax = anchorMax;
        Target.offsetMin = Vector2.zero;
        Target.offsetMax = Vector2.zero;
    }
}
