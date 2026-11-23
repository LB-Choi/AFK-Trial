using UnityEditor;
using UnityEditor.Build.Reporting;
public static class GoalPreviewBuild {
 public static void Run(){
 AFKTrial.Editor.TrialChecks.Run(); if(!System.IO.File.ReadAllText("Logs/TrialChecks.txt").StartsWith("PASS")){EditorApplication.Exit(1);return;}
 var r=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/Scenes/SideBattle.unity"},locationPathName="Builds/GoalReview/AFK-Trial.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
 EditorApplication.Exit(r.summary.result==BuildResult.Succeeded?0:1);
 }
}
