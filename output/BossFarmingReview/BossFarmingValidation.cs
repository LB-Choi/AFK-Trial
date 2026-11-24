using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
public static class BossFarmingValidation
{
    [InitializeOnLoadMethod]
    static void Schedule()
    {
        EditorApplication.delayCall += Run;
        EditorApplication.playModeStateChanged += state => {
            if (state == PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall += Run;
        };
    }
    static void Run()
    {
        const string folder = "../output/BossFarmingReview";
        if (!File.Exists(folder + "/request.txt") || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(folder + "/request.txt");
        try
        {
            AFKTrial.Editor.TrialChecks.Run();
            File.Copy("Logs/TrialChecks.txt", folder + "/checks.txt", true);
            if (!File.ReadAllText("Logs/TrialChecks.txt").StartsWith("PASS")) return;
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { "Assets/Scenes/SideBattle.unity" },
                locationPathName = "Builds/BossFarmingReview/AFK-Trial.exe",
                target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development
            });
            File.WriteAllText(folder + "/build.txt", report.summary.result.ToString());
        }
        catch (Exception error) { File.WriteAllText(folder + "/error.txt", error.ToString()); }
    }
}
