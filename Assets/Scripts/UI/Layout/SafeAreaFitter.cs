using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(RectTransform))]
public class SafeAreaFitter : MonoBehaviour
{
    [SerializeField] private RectTransform target;
    [SerializeField] private bool ignoreLeftInset;
    [SerializeField] private bool ignoreRightInset;
    [SerializeField] private bool ignoreTopInset;
    [SerializeField] private bool ignoreBottomInset;

    private Rect lastSafeArea;
    private Vector2Int lastScreenSize;

    public RectTransform Target
    {
        get { return target != null ? target : target = transform as RectTransform; }
    }

    public bool IgnoreLeftInset
    {
        get { return ignoreLeftInset; }
        set { ignoreLeftInset = value; }
    }

    public bool IgnoreRightInset
    {
        get { return ignoreRightInset; }
        set { ignoreRightInset = value; }
    }

    public bool IgnoreTopInset
    {
        get { return ignoreTopInset; }
        set { ignoreTopInset = value; }
    }

    public bool IgnoreBottomInset
    {
        get { return ignoreBottomInset; }
        set { ignoreBottomInset = value; }
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
        float screenWidth = Mathf.Max(Screen.width, 1);
        float screenHeight = Mathf.Max(Screen.height, 1);

        anchorMin.x /= screenWidth;
        anchorMin.y /= screenHeight;
        anchorMax.x /= screenWidth;
        anchorMax.y /= screenHeight;

        if (ignoreLeftInset)
        {
            anchorMin.x = 0f;
        }

        if (ignoreBottomInset)
        {
            anchorMin.y = 0f;
        }

        if (ignoreRightInset)
        {
            anchorMax.x = 1f;
        }

        if (ignoreTopInset)
        {
            anchorMax.y = 1f;
        }

        Target.anchorMin = anchorMin;
        Target.anchorMax = anchorMax;
        Target.offsetMin = Vector2.zero;
        Target.offsetMax = Vector2.zero;
    }
}
