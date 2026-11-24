#if DEVELOPMENT_BUILD
using System;
using System.IO;
using System.Collections;
using System.Reflection;
using UnityEngine;
namespace AFKTrial {
public sealed class AVReviewProbe : MonoBehaviour {
const BindingFlags F=BindingFlags.NonPublic|BindingFlags.Instance;
TrialRunner runner;string folder,last="";float next; bool hold;
object Get(string k)=>typeof(TrialRunner).GetField(k,F).GetValue(runner);
void Set(string k,object v)=>typeof(TrialRunner).GetField(k,F).SetValue(runner,v);
object Call(string k,params object[] args)=>typeof(TrialRunner).GetMethod(k,F).Invoke(runner,args);
void Menu(string name)=>Set("systemMenu",Enum.Parse(typeof(TrialRunner).GetNestedType("SystemMenu",BindingFlags.NonPublic),name));
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] static void Init(){if(Array.IndexOf(Environment.GetCommandLineArgs(),"-av-review")>=0)new GameObject("Isolated AV review").AddComponent<AVReviewProbe>();}
IEnumerator Start(){yield return null;runner=FindAnyObjectByType<TrialRunner>();var a=Environment.GetCommandLineArgs();folder=a[Array.IndexOf(a,"-av-review")+1];Directory.CreateDirectory(folder);Set("settingsPath",Path.Combine(folder,"settings.json"));Call("LoadDisplaySettings");Call("OpenProgressSessionAt",Path.Combine(folder,"profile"));
var b=(TrialBattle)Get("battle");var p=b.CaptureProgress();p.guideReady=p.guideSeen=63;b.RestoreProgress(p);Set("activeGuide",-1);Set("atTitle",true);Menu("Settings");Screen.SetResolution(960,540,FullScreenMode.Windowed);
FindAnyObjectByType<AudioListener>().gameObject.AddComponent<AVMeter>();
}
void Update(){if(runner==null||folder==null)return; var b=(TrialBattle)Get("battle");
string file=Path.Combine(folder,"command.txt");string command=File.Exists(file)?File.ReadAllText(file).Trim():"";
if(command!=last){last=command;
if(command.StartsWith("boss")){int stage=command.Contains("10")?10:5;var p=b.CaptureProgress();p.highest=stage-1;p.stage=stage;p.farming=false;p.repeatStage=0;p.guideReady=p.guideSeen=63;b.RestoreProgress(p);foreach(var u in b.Units){u.Hp=u.MaxHp=100000;if(u.Ally)u.Skills.StunnedUntil=10000;}Set("atTitle",false);Set("editing",false);Set("activeGuide",-1);Menu("Closed");Time.timeScale=1;hold=true;}
if(command=="resume"){Time.timeScale=1;hold=false;}
if(command=="max"){Call("SetAudioVolumes",1f,1f);}
if(command=="defaults"){Call("SetAudioVolumes",.3f,.65f);}
if(command=="quit")Application.Quit();
}
if(hold && b.ActiveBoss?.SpecialAttackPending==true){Time.timeScale=0;hold=false;}
if(Time.realtimeSinceStartup<next)return;next=Time.realtimeSinceStartup+.3f;
var bg=(AudioSource)Get("musicAudio");
File.WriteAllText(Path.Combine(folder,"state.txt"),"menu="+Get("systemMenu")+"\nelapsed="+b.Elapsed+"\nwarning="+b.ActiveBoss?.SpecialAttackPending+"\nmusic="+Get("musicVolume")+"\neffects="+Get("effectsVolume")+"\ndraftMusic="+Get("draftMusic")+"\ndraftEffects="+Get("draftEffects")+"\nbackgroundGain="+bg.volume+"\nbackgroundTime="+bg.time+"\npeak="+AVMeter.Peak+"\nrms="+AVMeter.Rms+"\nsamples="+AVMeter.Count);
}
}
public sealed class AVMeter:MonoBehaviour {
public static float Peak;public static long Count; static double sum; public static double Rms=>Count==0?0:Math.Sqrt(sum/Count);
void OnAudioFilterRead(float[] data,int channels){foreach(float value in data){Peak=Math.Max(Peak,Math.Abs(value));sum+=(double)value*value;}Count+=data.Length;}
}
}
#endif
