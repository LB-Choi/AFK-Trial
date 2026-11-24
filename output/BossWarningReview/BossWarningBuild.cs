using UnityEditor;
using UnityEditor.Build.Reporting;
public static class BossWarningBuild {
public static void Run(){var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {
scenes=new[]{"Assets/Scenes/SideBattle.unity"},locationPathName="Builds/BossWarningReview/AFK-Trial.exe",
target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
EditorApplication.Exit(report.summary.result==BuildResult.Succeeded?0:1);}
}
