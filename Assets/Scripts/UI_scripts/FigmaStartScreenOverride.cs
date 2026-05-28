using UnityEngine;

[DisallowMultipleComponent]
public class FigmaStartScreenOverride : MonoBehaviour
{
    private void OnEnable()
    {
        enabled = false;
    }
}
