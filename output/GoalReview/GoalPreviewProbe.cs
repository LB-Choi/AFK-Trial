#if DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEngine;
namespace AFKTrial {
public sealed class GoalPreviewProbe : MonoBehaviour {
 const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
 TrialRunner runner;
 [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
 static void Init(){if(Array.IndexOf(Environment.GetCommandLineArgs(),"-goal-preview")>=0)new GameObject("Goal preview").AddComponent<GoalPreviewProbe>();}
 void Set(string k,object v)=>typeof(TrialRunner).GetField(k,F).SetValue(runner,v);
 IEnumerator Start(){
 yield return null;
 runner=UnityEngine.Object.FindAnyObjectByType<TrialRunner>();
 var b=(TrialBattle)typeof(TrialRunner).GetField("battle",F).GetValue(runner);
 string[] args=Environment.GetCommandLineArgs(); string folder=args[Array.IndexOf(args,"-goal-preview")+1];
 Directory.CreateDirectory(folder);
 var p=b.CaptureProgress();p.guideSeen=p.guideReady=63;b.RestoreProgress(p);
 Set("atTitle",false);Set("activeGuide",-1);Set("heroView",Enum.Parse(typeof(TrialRunner).GetNestedType("HeroView",BindingFlags.NonPublic),"Menu"));
 var menu=typeof(TrialRunner).GetNestedType("SystemMenu",BindingFlags.NonPublic);
 Screen.SetResolution(480,270,FullScreenMode.Windowed); yield return new WaitForSecondsRealtime(2); for(int view=0;view<2;view++){
 Set("sideBattle",view==0); Screen.SetResolution(view==0?960:540,view==0?540:960,FullScreenMode.Windowed);
 yield return new WaitForSecondsRealtime(2);
 for(int state=0;state<4;state++){
 Set("editing",state==1);Set("systemMenu",Enum.Parse(menu,state>=2?"Goals":"Closed"));
 if(state==3){p=b.CaptureProgress();p.completedGoals=63;b.RestoreProgress(p);}
 yield return new WaitForSecondsRealtime(.3f); yield return new WaitForEndOfFrame();
 var texture=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(Path.Combine(folder,"view-"+view+"-"+state+".png"),texture.EncodeToPNG());Destroy(texture);
 }
 p.completedGoals=0;b.RestoreProgress(p);
 }
 Application.Quit();
 }
}
}
#endif
