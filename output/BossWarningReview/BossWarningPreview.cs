#if DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEngine;
namespace AFKTrial {
public sealed class BossWarningPreview : MonoBehaviour {
const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic;
TrialRunner runner;
void Set(string k, object v) => typeof(TrialRunner).GetField(k,F).SetValue(runner,v);
void Menu(string name) => Set("systemMenu",Enum.Parse(typeof(TrialRunner).GetNestedType("SystemMenu",BindingFlags.NonPublic),name));
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
static void Init(){ if(Array.IndexOf(Environment.GetCommandLineArgs(),"-boss-preview")>=0)new GameObject("Boss preview").AddComponent<BossWarningPreview>(); }
IEnumerator Start(){
yield return null;
runner=FindAnyObjectByType<TrialRunner>();
var args=Environment.GetCommandLineArgs(); string folder=args[Array.IndexOf(args,"-boss-preview")+1];
Directory.CreateDirectory(folder);
typeof(TrialRunner).GetMethod("OpenProgressSessionAt",F).Invoke(runner,new object[]{Path.Combine(folder,"profile")});
var battle=(TrialBattle)typeof(TrialRunner).GetField("battle",F).GetValue(runner);
Set("atTitle",false);Set("editing",false);Set("activeGuide",-1);
Screen.SetResolution(960,540,FullScreenMode.Windowed);
yield return new WaitForSecondsRealtime(2);
foreach(int stage in new[]{5,10}) {
var p=battle.CaptureProgress();p.highest=stage-1;p.stage=stage;p.guideSeen=p.guideReady=63;battle.RestoreProgress(p); Set("activeGuide",-1);
foreach(var unit in battle.Units){unit.Hp=unit.MaxHp=unit==battle.ActiveBoss || unit==battle.Units[0] ? 100000 : 0; if(unit.Ally)unit.Skills.StunnedUntil=10000;}
var boss=battle.ActiveBoss;
float deadline=Time.realtimeSinceStartup+25;
while(!boss.SpecialAttackPending && Time.realtimeSinceStartup<deadline)yield return null;
if(!boss.SpecialAttackPending)throw new Exception("No warning observed");
Time.timeScale=0;
yield return new WaitForEndOfFrame();
var t=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(Path.Combine(folder,"warning-"+stage+".png"),t.EncodeToPNG());Destroy(t);
Menu("BossInfo");Time.timeScale=1;
float elapsed=battle.Elapsed, cooldown=boss.Cooldown;
yield return new WaitForSecondsRealtime(.5f);
if(battle.Elapsed!=elapsed || boss.Cooldown!=cooldown || !boss.SpecialAttackPending)throw new Exception("Menu did not pause warning");
yield return new WaitForEndOfFrame();
t=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(Path.Combine(folder,"info-"+stage+".png"),t.EncodeToPNG());Destroy(t);
Menu("Closed");
}
File.WriteAllText(Path.Combine(folder,"runtime.txt"),"PASS: both boss warnings rendered; information menu pauses pending warning and cooldown.");
Application.Quit();
}
}}
#endif


