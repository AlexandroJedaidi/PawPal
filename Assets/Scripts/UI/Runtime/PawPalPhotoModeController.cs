using System;
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PawPalPhotoModeController : MonoBehaviour
{
    private readonly PawPalPhotoAlbumStore albumStore = new PawPalPhotoAlbumStore();

    private AppShellController shell;
    private PawPalPhotoModeView view;
    private PawPalPhotoDogCommandDirector photoDogCommands;
    private DogCycleCamera dogCamera;
    private DogRoomAgent activeDog;
    private Texture2D pendingCapture;
    private Coroutine captureRoutine;
    private bool active;
    private bool captureBusy;

    public event Action PhotoModeExited;

    public bool IsActive
    {
        get { return active; }
    }

    public void Initialize(AppShellController ownerShell, PawPalPhotoModeView photoView)
    {
        shell = ownerShell;
        view = photoView;
        photoDogCommands = GetComponent<PawPalPhotoDogCommandDirector>();
        if (photoDogCommands == null)
        {
            photoDogCommands = gameObject.AddComponent<PawPalPhotoDogCommandDirector>();
        }

        albumStore.Load();

        if (view == null)
        {
            return;
        }

        view.CloseRequested += HandleCloseRequested;
        view.ShutterRequested += HandleShutterRequested;
        view.AlbumRequested += HandleAlbumRequested;
        view.PoseSelected += HandlePoseSelected;
        view.AttentionRequested += HandleAttentionRequested;
        view.KeepRequested += HandleKeepRequested;
        view.RetakeRequested += HandleRetakeRequested;
        view.DeletePreviewRequested += HandleDeletePreviewRequested;
        view.AlbumBackRequested += HandleAlbumBackRequested;
        view.AlbumPhotoSelected += HandleAlbumPhotoSelected;
        view.AlbumFavoriteToggled += HandleAlbumFavoriteToggled;
        view.AlbumShareRequested += HandleAlbumShareRequested;
        view.AlbumDeleteRequested += HandleAlbumDeleteRequested;
        view.AlbumTitleChanged += HandleAlbumTitleChanged;
        view.DetailBackRequested += HandleDetailBackRequested;
        view.DetailDeleteRequested += HandleDetailDeleteRequested;
        view.DetailFavoriteToggled += HandleDetailFavoriteToggled;
        view.ZoomChanged += HandleZoomChanged;
    }

    public bool TryEnterPhotoMode()
    {
        dogCamera = ResolveDogCamera();
        activeDog = ResolvePreferredDog(dogCamera);

        if (dogCamera == null || activeDog == null)
        {
            if (view != null)
            {
                view.ShowToast("Photo mode needs a dog camera.");
            }

            return false;
        }

        if (!dogCamera.EnterPhotoMode(activeDog))
        {
            if (view != null)
            {
                view.ShowToast("Photo mode could not start.");
            }

            return false;
        }

        active = true;
        captureBusy = false;
        albumStore.Load();
        DestroyPendingCapture();

        if (view != null)
        {
            view.ShowLiveMode();
            view.SetZoom01(dogCamera.PhotoZoom01);
        }

        RequestDogAttention(1.75f);
        return true;
    }

    public void ExitPhotoMode()
    {
        if (!active && pendingCapture == null)
        {
            HideView();
            return;
        }

        if (captureRoutine != null)
        {
            StopCoroutine(captureRoutine);
            captureRoutine = null;
        }

        captureBusy = false;
        DestroyPendingCapture();
        if (photoDogCommands != null)
        {
            photoDogCommands.CancelActiveCommand(true);
        }

        if (dogCamera != null)
        {
            dogCamera.ExitPhotoMode();
        }

        dogCamera = null;
        activeDog = null;
        active = false;
        HideView();

        Action handler = PhotoModeExited;
        if (handler != null)
        {
            handler();
        }
    }

    private void HideView()
    {
        if (view != null)
        {
            view.HideAll();
        }
    }

    private void HandleCloseRequested()
    {
        ExitPhotoMode();
    }

    private void HandleShutterRequested()
    {
        if (!active || captureBusy || view == null)
        {
            return;
        }

        captureRoutine = StartCoroutine(CapturePhotoRoutine());
    }

    private IEnumerator CapturePhotoRoutine()
    {
        captureBusy = true;
        DestroyPendingCapture();

        if (view != null)
        {
            view.SetCaptureHudVisible(false);
        }

        yield return new WaitForEndOfFrame();

        pendingCapture = ScreenCapture.CaptureScreenshotAsTexture();

        if (view != null)
        {
            view.SetCaptureHudVisible(true);
            view.ShowPreview(pendingCapture);
        }

        captureBusy = false;
        captureRoutine = null;
    }

    private void HandleAlbumRequested()
    {
        if (!active || view == null)
        {
            return;
        }

        albumStore.Load();
        view.ShowAlbum(albumStore);
    }

    private void HandlePoseSelected(PawPalPhotoPoseId poseId)
    {
        if (!active || dogCamera == null)
        {
            return;
        }

        activeDog = ResolvePreferredDog(dogCamera);
        dogCamera.FocusPhotoModeOnActiveDog();

        string failureReason;
        if (photoDogCommands != null && !photoDogCommands.TryPose(activeDog, ResolvePhotoCamera(), poseId, out failureReason) && view != null)
        {
            view.ShowToast(string.IsNullOrEmpty(failureReason) ? "That pose is not available." : failureReason);
        }

        if (view != null)
        {
            view.SetZoom01(dogCamera.PhotoZoom01);
        }
    }

    private void HandleAttentionRequested()
    {
        if (!active)
        {
            return;
        }

        activeDog = ResolvePreferredDog(dogCamera);
        string failureReason = string.Empty;
        if (photoDogCommands != null && photoDogCommands.TryWhistle(activeDog, ResolvePhotoCamera(), out failureReason))
        {
            RequestDogAttention(4f);
            return;
        }

        if (view != null)
        {
            view.ShowToast(string.IsNullOrEmpty(failureReason) ? "Whistle did not reach your dog." : failureReason);
        }
    }

    private void HandleKeepRequested()
    {
        if (pendingCapture == null)
        {
            if (view != null)
            {
                view.ShowLiveMode();
            }

            return;
        }

        PawPalDogState dogState = PawPalGameRuntime.Instance != null ? PawPalGameRuntime.Instance.ActiveDog : null;
        string dogId = dogState != null ? dogState.Id : activeDog != null ? activeDog.DogId : string.Empty;
        string dogName = dogState != null ? dogState.DisplayName : activeDog != null ? activeDog.name : "Dog";

        PawPalPhotoRecord record = albumStore.SavePhoto(pendingCapture, dogId, dogName);
        DestroyPendingCapture();

        if (view != null)
        {
            view.ShowLiveMode();
            view.ShowToast(record != null ? "Saved " + record.FileName : "Photo save failed.");
        }
    }

    private void HandleRetakeRequested()
    {
        DestroyPendingCapture();
        if (view != null)
        {
            view.ShowLiveMode();
        }
    }

    private void HandleDeletePreviewRequested()
    {
        DestroyPendingCapture();
        if (view != null)
        {
            view.ShowLiveMode();
        }
    }

    private void HandleAlbumBackRequested()
    {
        if (view != null)
        {
            view.ShowLiveMode();
            view.SetZoom01(dogCamera != null ? dogCamera.PhotoZoom01 : 0.35f);
        }
    }

    private void HandleAlbumPhotoSelected(PawPalPhotoRecord record)
    {
        if (view == null || record == null)
        {
            return;
        }

        view.ShowDetail(albumStore, record);
    }

    private void HandleDetailBackRequested()
    {
        if (view == null)
        {
            return;
        }

        albumStore.Load();
        view.ShowAlbum(albumStore);
    }

    private void HandleDetailDeleteRequested(PawPalPhotoRecord record)
    {
        if (record == null)
        {
            return;
        }

        albumStore.DeletePhoto(record);
        albumStore.Load();

        if (view != null)
        {
            view.ShowAlbum(albumStore);
            view.ShowToast("Photo deleted.");
        }
    }

    private void HandleAlbumFavoriteToggled(PawPalPhotoRecord record)
    {
        if (record == null)
        {
            return;
        }

        albumStore.SetFavorite(record, !record.Favorite);
        albumStore.Load();

        if (view != null)
        {
            view.ShowAlbum(albumStore);
        }
    }

    private void HandleAlbumShareRequested(PawPalPhotoRecord record)
    {
        if (record == null)
        {
            if (view != null)
            {
                view.ShowToast("Pick a photo first.");
            }

            return;
        }

        string resultMessage;
        string title = BuildShareTitle(record);
        string message = "A PawFriends photo" + (string.IsNullOrWhiteSpace(record.DogName) ? "." : " of " + record.DogName + ".");
        PawPalPhotoShareService.ShareImage(albumStore.GetPhotoPath(record), title, message, out resultMessage);

        if (view != null)
        {
            view.ShowToast(resultMessage);
        }
    }

    private void HandleAlbumDeleteRequested(PawPalPhotoRecord record)
    {
        if (record == null)
        {
            if (view != null)
            {
                view.ShowToast("Pick a photo first.");
            }

            return;
        }

        albumStore.DeletePhoto(record);
        albumStore.Load();

        if (view != null)
        {
            view.ShowAlbum(albumStore);
            view.ShowToast("Photo deleted.");
        }
    }

    private void HandleAlbumTitleChanged(PawPalPhotoRecord record, string title)
    {
        if (record == null)
        {
            return;
        }

        albumStore.SetTitle(record, title);
        albumStore.Load();

        PawPalPhotoRecord refreshed = FindRecord(record.Id);
        if (view != null)
        {
            if (refreshed != null)
            {
                view.ShowDetail(albumStore, refreshed);
            }

            view.ShowToast("Caption saved.");
        }
    }

    private void HandleDetailFavoriteToggled(PawPalPhotoRecord record)
    {
        if (record == null)
        {
            return;
        }

        albumStore.SetFavorite(record, !record.Favorite);
        albumStore.Load();

        PawPalPhotoRecord refreshed = FindRecord(record.Id);
        if (view != null && refreshed != null)
        {
            view.ShowDetail(albumStore, refreshed);
        }
    }

    private static string BuildShareTitle(PawPalPhotoRecord record)
    {
        if (record == null)
        {
            return "PawFriends Photo";
        }

        if (!string.IsNullOrWhiteSpace(record.Title))
        {
            return record.Title.Trim();
        }

        return string.IsNullOrWhiteSpace(record.DogName) ? "PawFriends Photo" : record.DogName + " photo";
    }

    private void HandleZoomChanged(float zoom01)
    {
        if (dogCamera != null)
        {
            dogCamera.SetPhotoZoom01(zoom01);
        }
    }

    private void RequestDogAttention(float duration)
    {
        DogRoomAgent dog = activeDog != null ? activeDog : ResolvePreferredDog(dogCamera);
        if (dog == null)
        {
            return;
        }

        DogCameraAttention attention = dog.GetComponentInChildren<DogCameraAttention>(true);
        if (attention != null)
        {
            attention.RequestCameraAttention(duration);
        }
    }

    private PawPalPhotoRecord FindRecord(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return null;
        }

        for (int i = 0; i < albumStore.Records.Count; i++)
        {
            PawPalPhotoRecord record = albumStore.Records[i];
            if (record != null && string.Equals(record.Id, id, StringComparison.Ordinal))
            {
                return record;
            }
        }

        return null;
    }

    private DogCycleCamera ResolveDogCamera()
    {
        Camera mainCamera = Camera.main;
        DogCycleCamera resolvedCamera = mainCamera != null ? mainCamera.GetComponent<DogCycleCamera>() : null;
        if (resolvedCamera != null)
        {
            return resolvedCamera;
        }

        resolvedCamera = FindFirstObjectByType<DogCycleCamera>();
        if (resolvedCamera != null)
        {
            return resolvedCamera;
        }

        if (mainCamera != null && mainCamera.GetComponent<CameraFollow>() == null && HasSceneDogs())
        {
            return mainCamera.gameObject.AddComponent<DogCycleCamera>();
        }

        return null;
    }

    private Camera ResolvePhotoCamera()
    {
        if (dogCamera != null)
        {
            Camera camera = dogCamera.GetComponent<Camera>();
            if (camera != null)
            {
                return camera;
            }
        }

        return Camera.main;
    }

    private DogRoomAgent ResolvePreferredDog(DogCycleCamera camera)
    {
        PawPalGameRuntime runtime = PawPalGameRuntime.Instance;
        DogRoomAgent[] agents = FindObjectsByType<DogRoomAgent>(FindObjectsSortMode.InstanceID);

        if (runtime != null && runtime.ActiveDog != null && agents != null)
        {
            string activeDogId = runtime.ActiveDog.Id;
            if (!string.IsNullOrEmpty(activeDogId))
            {
                for (int i = 0; i < agents.Length; i++)
                {
                    DogRoomAgent agent = agents[i];
                    if (agent != null && agent.HasExplicitDogId && string.Equals(agent.DogId, activeDogId, StringComparison.OrdinalIgnoreCase))
                    {
                        return agent;
                    }
                }
            }

            if (runtime.ActiveDogIndex >= 0 && runtime.ActiveDogIndex < agents.Length)
            {
                return agents[runtime.ActiveDogIndex];
            }
        }

        Transform activeTarget = camera != null ? camera.ActiveTarget : null;
        DogRoomAgent cameraDog = activeTarget != null ? activeTarget.GetComponentInParent<DogRoomAgent>() : null;
        if (cameraDog != null)
        {
            return cameraDog;
        }

        return agents != null && agents.Length > 0 ? agents[0] : null;
    }

    private bool HasSceneDogs()
    {
        DogRoomAgent[] agents = FindObjectsByType<DogRoomAgent>(FindObjectsSortMode.None);
        return agents != null && agents.Length > 0;
    }

    private void DestroyPendingCapture()
    {
        if (pendingCapture != null)
        {
            Destroy(pendingCapture);
            pendingCapture = null;
        }
    }
}
