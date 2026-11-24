using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
public static class AVAcceptedBuild
{
    [InitializeOnLoadMethod]
    static void Schedule() { EditorApplication.delayCall += Run; }
    public static void Run()
    {
        string folder = Path.GetFullPath("../output/AVReview/accepted");
        string request = Path.Combine(folder, "request.txt");
        if (!File.Exists(request) || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Move(request, Path.Combine(folder, "started.txt"));
        try
        {
            AFKTrial.Editor.TrialChecks.Run();
            File.Copy("Logs/TrialChecks.txt", Path.Combine(folder, "checks.txt"), true);
            if (!File.ReadAllText("Logs/TrialChecks.txt").StartsWith("PASS")) throw new Exception("Model checks failed");
            var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { "Assets/Scenes/SideBattle.unity" },
                locationPathName = "Builds/AVAccepted/AFK-Trial.exe",
                target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development
            });
            File.WriteAllText(Path.Combine(folder, "build.txt"), result.summary.result.ToString());
        }
        catch (Exception error) { File.WriteAllText(Path.Combine(folder, "error.txt"), error.ToString()); }
    }
}
