#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

public static class PawPalVoiceCommandBuildPostprocess
{
    private const string MicrophoneUsageDescription = "PawFriends uses the microphone so your dogs can respond to voice commands.";
    private const string SpeechUsageDescription = "PawFriends uses speech recognition so your dogs can react when you say their names and learned tricks.";

    [PostProcessBuild(250)]
    public static void OnPostprocessBuild(BuildTarget target, string pathToBuiltProject)
    {
        if (target != BuildTarget.iOS || string.IsNullOrWhiteSpace(pathToBuiltProject))
        {
            return;
        }

        UpdateProject(pathToBuiltProject);
        UpdateInfoPlist(pathToBuiltProject);
    }

    private static void UpdateProject(string pathToBuiltProject)
    {
        string projectPath = PBXProject.GetPBXProjectPath(pathToBuiltProject);
        PBXProject project = new PBXProject();
        project.ReadFromFile(projectPath);

        string mainTargetGuid = project.GetUnityMainTargetGuid();
        string frameworkTargetGuid = project.GetUnityFrameworkTargetGuid();

        project.AddFrameworkToProject(frameworkTargetGuid, "Speech.framework", false);
        project.AddFrameworkToProject(frameworkTargetGuid, "AVFoundation.framework", false);
        project.AddFrameworkToProject(mainTargetGuid, "Speech.framework", false);
        project.AddFrameworkToProject(mainTargetGuid, "AVFoundation.framework", false);

        project.WriteToFile(projectPath);
    }

    private static void UpdateInfoPlist(string pathToBuiltProject)
    {
        string plistPath = Path.Combine(pathToBuiltProject, "Info.plist");
        PlistDocument plist = new PlistDocument();
        plist.ReadFromFile(plistPath);

        PlistElementDict root = plist.root;
        root.SetString("NSMicrophoneUsageDescription", MicrophoneUsageDescription);
        root.SetString("NSSpeechRecognitionUsageDescription", SpeechUsageDescription);

        plist.WriteToFile(plistPath);
    }
}
#endif
