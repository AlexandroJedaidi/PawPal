using UnityEngine;
using UnityEngine.EventSystems;

public static class PawPalEventSystemUtility
{
    public static EventSystem EnsureSingleEventSystem(bool dontDestroyOnLoad)
    {
        EventSystem[] eventSystems = Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        EventSystem primary = ChoosePrimary(eventSystems);

        if (primary == null)
        {
            GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            if (dontDestroyOnLoad)
            {
                Object.DontDestroyOnLoad(eventSystemObject);
            }

            return eventSystemObject.GetComponent<EventSystem>();
        }

        if (dontDestroyOnLoad)
        {
            Object.DontDestroyOnLoad(primary.gameObject);
        }

        for (int i = 0; i < eventSystems.Length; i++)
        {
            EventSystem candidate = eventSystems[i];
            if (candidate == null || candidate == primary)
            {
                continue;
            }

            Object.Destroy(candidate.gameObject);
        }

        return primary;
    }

    private static EventSystem ChoosePrimary(EventSystem[] eventSystems)
    {
        if (eventSystems == null || eventSystems.Length == 0)
        {
            return null;
        }

        if (EventSystem.current != null)
        {
            return EventSystem.current;
        }

        for (int i = 0; i < eventSystems.Length; i++)
        {
            EventSystem candidate = eventSystems[i];
            if (candidate != null && candidate.gameObject.scene.IsValid() && candidate.gameObject.activeInHierarchy)
            {
                return candidate;
            }
        }

        return eventSystems[0];
    }
}
