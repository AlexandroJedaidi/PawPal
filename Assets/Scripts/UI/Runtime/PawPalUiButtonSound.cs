using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public sealed class PawPalUiButtonSound : MonoBehaviour
{
    [SerializeField] private PawPalUiClickSoundKind soundKind = PawPalUiClickSoundKind.Auto;

    private Button button;
    private bool listenerBound;

    public void Configure(PawPalUiClickSoundKind preferredSoundKind)
    {
        if (preferredSoundKind != PawPalUiClickSoundKind.Auto || soundKind == PawPalUiClickSoundKind.Auto)
        {
            soundKind = preferredSoundKind;
        }

        Rebind();
    }

    private void Awake()
    {
        button = GetComponent<Button>();
        Rebind();
    }

    private void OnEnable()
    {
        Rebind();
    }

    private void OnDisable()
    {
        Unbind();
    }

    private void OnDestroy()
    {
        Unbind();
    }

    private void Rebind()
    {
        if (button == null)
        {
            button = GetComponent<Button>();
        }

        Unbind();
        if (button == null)
        {
            return;
        }

        button.onClick.AddListener(HandleClicked);
        listenerBound = true;
    }

    private void Unbind()
    {
        if (button == null || !listenerBound)
        {
            return;
        }

        button.onClick.RemoveListener(HandleClicked);
        listenerBound = false;
    }

    private void HandleClicked()
    {
        PawPalUiAudio.PlayClick(soundKind, gameObject);
    }
}
