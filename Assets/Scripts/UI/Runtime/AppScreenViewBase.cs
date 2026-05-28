using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public abstract class AppScreenViewBase : MonoBehaviour
{
    protected AppShellController shell;
    protected UiSpriteLibrary sprites;
    protected ScreenContainer container;

    private bool built;

    protected virtual bool UseScreenContainer
    {
        get { return true; }
    }

    public void Initialize(AppShellController appShell, UiSpriteLibrary spriteLibrary)
    {
        shell = appShell;
        sprites = spriteLibrary;

        if (UseScreenContainer)
        {
            container = GetComponent<ScreenContainer>();
            if (container == null)
            {
                container = gameObject.AddComponent<ScreenContainer>();
            }

            container.Initialize();
        }
        else
        {
            container = GetComponent<ScreenContainer>();
            if (container != null)
            {
                container.enabled = false;
            }
        }

        if (!built)
        {
            BuildContent();
            built = true;
        }

        ApplyLayout(shell.CurrentBucket);
    }

    public virtual void ApplyLayout(UiLayoutBucket bucket)
    {
        if (UseScreenContainer && container != null)
        {
            container.ApplyLayout(bucket, shell.BottomNavHeight);
        }
    }

    protected abstract void BuildContent();
}
