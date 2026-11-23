using UnityEditor;
using UnityEditor.Build.Reporting;
public static class AudioReviewBuild {
public static void Run(){var r=BuildPipeline.BuildPlayer(new BuildPlayerOptions {
scenes=new[]{"Assets/Scenes/SideBattle.unity"},locationPathName="Builds/AudioReview/AFK-Trial.exe",
target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
EditorApplication.Exit(r.summary.result==BuildResult.Succeeded?0:1);}
}
