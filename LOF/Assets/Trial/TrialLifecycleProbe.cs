#if DEVELOPMENT_BUILD && UNITY_STANDALONE_WIN
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEngine;
namespace AFKTrial {
internal sealed class TrialLifecycleProbe : MonoBehaviour {
 const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
 TrialRunner runner;string folder;float next;
 [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
 static void Init(){if(Array.IndexOf(Environment.GetCommandLineArgs(),"-afk-lifecycle-check")>=0)new GameObject("Isolated lifecycle probe").AddComponent<TrialLifecycleProbe>();}
 object Get(string key)=>typeof(TrialRunner).GetField(key,Flags).GetValue(runner);
 void Set(string key,object value)=>typeof(TrialRunner).GetField(key,Flags).SetValue(runner,value);
 IEnumerator Start(){
 yield return null;runner=UnityEngine.Object.FindAnyObjectByType<TrialRunner>();
 string[] args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"-afk-check-output");
 if(runner==null||index<0||index+1>=args.Length){Application.Quit(2);yield break;}
 folder=Path.GetFullPath(args[index+1]);Directory.CreateDirectory(folder);
 var battle=(TrialBattle)Get("battle");
 typeof(TrialRunner).GetMethod("OpenProgressSessionAt",Flags).Invoke(runner,new object[]{Path.Combine(folder,"profile")});
 var p=battle.CaptureProgress();
 if(p.highest==0){p.highest=5;p.stage=6;p.guideSeen=p.guideReady=63;battle.RestoreProgress(p);}
 Set("atTitle",false);
 foreach(var u in battle.Units)if(!u.Ally)u.Hp=100000000;
 }
 void Update(){
 if(runner==null||folder==null||Time.realtimeSinceStartup<next)return;next=Time.realtimeSinceStartup+.25f;
 var b=(TrialBattle)Get("battle");
 File.WriteAllText(Path.Combine(folder,"state.txt"),"utc="+DateTimeOffset.UtcNow.ToUnixTimeSeconds()+"\nsuspended="+Get("suspended")+"\nelapsed="+b.Elapsed+"\nhp="+b.Units[0].Hp+"\ngold="+b.Gold+"\npendingSeconds="+Get("pendingSeconds")+"\nlastSeenUtc="+Get("lastSeenUtc")+"\nrestart="+Get("restartAfterAway")+"\nsystemMenu="+Get("systemMenu")+"\nguide="+Get("activeGuide")+"\nerror="+Get("idleSaveError"));
 }
}
}
#endif
