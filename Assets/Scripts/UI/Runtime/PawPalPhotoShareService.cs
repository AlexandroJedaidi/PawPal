using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

public static class PawPalPhotoShareService
{
#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void PawPalShareImage(string imagePath, string title, string message);
#endif

    public static bool ShareImage(string imagePath, string title, string message, out string resultMessage)
    {
        if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
        {
            resultMessage = "Photo file is missing.";
            return false;
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        return ShareImageAndroid(imagePath, title, message, out resultMessage);
#elif UNITY_IOS && !UNITY_EDITOR
        PawPalShareImage(imagePath, title ?? "PawFriends Photo", message ?? string.Empty);
        resultMessage = "Opening share sheet.";
        return true;
#else
        return RevealImageInEditorOrDesktop(imagePath, out resultMessage);
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private static bool ShareImageAndroid(string imagePath, string title, string message, out string resultMessage)
    {
        try
        {
            using (AndroidJavaClass intentClass = new AndroidJavaClass("android.content.Intent"))
            using (AndroidJavaObject intent = new AndroidJavaObject("android.content.Intent"))
            using (AndroidJavaClass uriClass = new AndroidJavaClass("android.net.Uri"))
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                using (AndroidJavaClass strictMode = new AndroidJavaClass("android.os.StrictMode"))
                using (AndroidJavaObject policyBuilder = new AndroidJavaObject("android.os.StrictMode$VmPolicy$Builder"))
                {
                    AndroidJavaObject policy = policyBuilder.Call<AndroidJavaObject>("build");
                    strictMode.CallStatic("setVmPolicy", policy);
                }

                AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                intent.Call<AndroidJavaObject>("setAction", intentClass.GetStatic<string>("ACTION_SEND"));
                intent.Call<AndroidJavaObject>("setType", "image/png");
                intent.Call<AndroidJavaObject>("putExtra", intentClass.GetStatic<string>("EXTRA_SUBJECT"), title ?? "PawFriends Photo");
                intent.Call<AndroidJavaObject>("putExtra", intentClass.GetStatic<string>("EXTRA_TEXT"), message ?? string.Empty);

                AndroidJavaObject uri = uriClass.CallStatic<AndroidJavaObject>("parse", "file://" + imagePath.Replace("\\", "/"));
                intent.Call<AndroidJavaObject>("putExtra", intentClass.GetStatic<string>("EXTRA_STREAM"), uri);
                intent.Call<AndroidJavaObject>("addFlags", intentClass.GetStatic<int>("FLAG_GRANT_READ_URI_PERMISSION"));

                AndroidJavaObject chooser = intentClass.CallStatic<AndroidJavaObject>("createChooser", intent, title ?? "Share photo");
                currentActivity.Call("startActivity", chooser);
            }

            resultMessage = "Opening share sheet.";
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning("PawPalPhotoShareService Android share failed: " + exception.Message);
            resultMessage = "Share is not available on this device.";
            return false;
        }
    }
#endif

    private static bool RevealImageInEditorOrDesktop(string imagePath, out string resultMessage)
    {
        try
        {
#if UNITY_EDITOR
            UnityEditor.EditorUtility.RevealInFinder(imagePath);
            resultMessage = "Photo revealed on disk.";
            return true;
#else
            Application.OpenURL("file://" + imagePath.Replace("\\", "/"));
            resultMessage = "Photo opened on disk.";
            return true;
#endif
        }
        catch (Exception exception)
        {
            Debug.LogWarning("PawPalPhotoShareService desktop fallback failed: " + exception.Message);
            resultMessage = "Could not open the photo file.";
            return false;
        }
    }
}
