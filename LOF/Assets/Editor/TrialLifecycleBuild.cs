using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
namespace AFKTrial.Editor {
public static class TrialLifecycleBuild {
 public static void Run(){
 TrialChecks.Run();
 if(!File.ReadAllText("Logs/TrialChecks.txt").StartsWith("PASS")){EditorApplication.Exit(1);return;}
 var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{
 scenes=new[]{"Assets/Scenes/SideBattle.unity"},
 locationPathName="Builds/LifecycleReview/AFK-Trial.exe",
 target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
 File.WriteAllText("../output/lifecycle-build-result.txt",report.summary.result+"\n"+report.summary.totalSize);
 EditorApplication.Exit(report.summary.result==BuildResult.Succeeded?0:1);
 }
}
}
