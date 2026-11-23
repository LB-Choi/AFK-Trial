#if DEVELOPMENT_BUILD
using System;
using System.IO;
using System.Collections;
using System.Reflection;
using UnityEngine;
namespace AFKTrial {
public sealed class AudioReviewProbe : MonoBehaviour {
const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
TrialRunner runner;
object Get(string k)=>typeof(TrialRunner).GetField(k,F).GetValue(runner);
void Set(string k,object v)=>typeof(TrialRunner).GetField(k,F).SetValue(runner,v);
object Call(string k,params object[] v)=>typeof(TrialRunner).GetMethod(k,F).Invoke(runner,v);
void Require(bool v,string message){if(!v)throw new Exception(message);}
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
static void Init(){if(Array.IndexOf(Environment.GetCommandLineArgs(),"-audio-review")>=0)new GameObject("Audio review").AddComponent<AudioReviewProbe>();}
IEnumerator Start(){yield return null;
runner=FindAnyObjectByType<TrialRunner>();
var args=Environment.GetCommandLineArgs();string folder=args[Array.IndexOf(args,"-audio-review")+1];Directory.CreateDirectory(folder);
Set("settingsPath",Path.Combine(folder,"settings.json")); Set("atTitle",false);Set("activeGuide",-1);
Require((bool)Call("SetAudioVolumes",.25f,.5f),"save audio");Call("EnsureAudio");
Require(((AudioSource)Get("musicAudio")).clip==null,"no unapproved music");
foreach(var cue in new[]{TrialBattle.SoundCue.Hit,TrialBattle.SoundCue.BossWarning,TrialBattle.SoundCue.BossSpecial,TrialBattle.SoundCue.Reward}){
Call("PlayBattleSound",cue);yield return null;
var source=(AudioSource)Get(cue==TrialBattle.SoundCue.Hit?"combatAudio":cue==TrialBattle.SoundCue.Reward?"uiAudio":"priorityAudio");
Require(source.isPlaying && Mathf.Approximately(source.volume,.5f),"play cue "+cue);
yield return new WaitForSecondsRealtime(.1f);
}
Require((bool)Call("SetAudioVolumes",.25f,0f),"mute save");yield return null;
Require(((AudioSource)Get("combatAudio")).volume==0 && ((AudioSource)Get("priorityAudio")).volume==0,"mute live sources");
Call("LoadDisplaySettings");Require((float)Get("effectsVolume")==0,"mute reload");
File.WriteAllText(Path.Combine(folder,"runtime.txt"),"PASS: four cues play on intended AudioSources, volumes applied, mute persisted; background intentionally unassigned. This is not a listening-quality test.");
Application.Quit();
}
}}
#endif
