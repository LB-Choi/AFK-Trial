using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
namespace AFKTrial.Editor {
internal static class TrialSummonChecks {
static void Require(bool ok,string message){if(!ok)throw new InvalidOperationException("Summon: "+message);}
public static void Check(TrialData data){
 string folder=Path.Combine(Application.temporaryCachePath,"summon-"+Guid.NewGuid());Directory.CreateDirectory(folder);
 var host=new GameObject("Isolated summon check");host.SetActive(false);
 const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
 try{
 var runner=host.AddComponent<TrialRunner>();
 void Set(string key,object value)=>typeof(TrialRunner).GetField(key,flags).SetValue(runner,value);
 object Call(string key,params object[] args)=>typeof(TrialRunner).GetMethod(key,flags).Invoke(runner,args);
 string path=Path.Combine(folder,"progress.json");
 Set("progressPath",path);Set("victoryBackupPath",Path.Combine(folder,"victory.json"));
 Set("lastSeenUtc",DateTimeOffset.UtcNow.ToUnixTimeSeconds());
 var battle=new TrialBattle(data);var saved=battle.CaptureProgress();int missing=data.Heroes.Count-1;
 saved.heroes=saved.heroes.Where(h=>h.id!=data.Heroes[missing].Id).ToArray();
 saved.tickets=30;saved.duplicateStreak=9;
 Require(battle.RestoreProgress(saved),"legacy/pity fixture");Set("battle",battle);
 bool Save(TrialBattle.Progress p)=>(bool)Call("TrySaveProgress",p,0L,false);
 string before=JsonUtility.ToJson(battle.CaptureProgress());
 using(var locked=new FileStream(path+".tmp",FileMode.Create,FileAccess.Write,FileShare.None))
 Require(battle.Summon(10,Save)==null&&JsonUtility.ToJson(battle.CaptureProgress())==before,"failed save grants and charges nothing");
 var pulls=battle.Summon(10,Save);
 Require(pulls.Length==10&&pulls[0].Hero==missing&&pulls[0].IsNew&&battle.MissingSummonHeroes==0&&battle.SummonDuplicateStreak==0&&battle.Tickets==20,"guarantee/reset/payment");
 string paid=JsonUtility.ToJson(battle.CaptureProgress());
 Require(battle.Summon(1,Save)==null&&paid==JsonUtility.ToJson(battle.CaptureProgress()),"pending batch cannot be replaced");
 for(int i=0;i<3;i++){battle=new TrialBattle(data);Set("battle",battle);Call("ReadProgress",path);
 Require(JsonUtility.ToJson(battle.CaptureProgress())==paid&&battle.PendingSummons.Length==10,"reconnect does not grant or charge again");}
 using(var locked=new FileStream(path+".tmp",FileMode.Create,FileAccess.Write,FileShare.None))
 Require(!battle.ConfirmSummons(Save)&&JsonUtility.ToJson(battle.CaptureProgress())==paid,"failed acknowledgement preserves result");
 Require(battle.ConfirmSummons(Save)&&!battle.ConfirmSummons(Save),"confirmation idempotent");
 string confirmed=JsonUtility.ToJson(battle.CaptureProgress());Call("ReadProgress",path);
 Require(battle.PendingSummons==null&&battle.Tickets==20&&JsonUtility.ToJson(battle.CaptureProgress())==confirmed,"acknowledgement persists");
 Require(battle.Summon(1,Save).Length==1,"next draw allowed");Call("ReadProgress",path);
 Require(battle.PendingSummons.Length==1&&battle.Tickets==19,"single draw restores");
 var bad=battle.CaptureProgress();bad.pendingSummons[0].heroId="unknown";Require(!battle.RestoreProgress(bad),"bad receipt rejected");
 string tables=Path.Combine(folder,"tables");Directory.CreateDirectory(tables);
 foreach(string file in Directory.GetFiles(Path.Combine(Application.dataPath,"Editor/TrialChecksData~"),"*.csv"))File.Copy(file,Path.Combine(tables,Path.GetFileName(file)));
 string rates=Path.Combine(tables,"SummonRates.csv");
 string valid="heroId,percent\n"+string.Join("\n",data.Heroes.Select((h,i)=>h.Id+","+(i==0?55:5)));
 File.WriteAllText(rates,valid);var weighted=new TrialBattle(new TrialData(tables));
 Require(weighted.Data.SummonRates[0]==55&&weighted.RollSummonHero(.549f)==0&&weighted.RollSummonHero(.551f)==1&&weighted.RollSummonHero(1)==9,"weighted CSV draw boundaries");
 foreach(string invalid in new[]{valid.Replace(",55",",54"),valid.Replace(",55",",-1"),valid.Replace(",55",",NaN"),valid+"\n"+data.Heroes[0].Id+",1",valid.Replace(data.Heroes[0].Id,"unknown"),"heroId,percent\n"+data.Heroes[0].Id+",100"}){
 File.WriteAllText(rates,invalid);bool rejected=false;try{new TrialData(tables);}catch(InvalidDataException){rejected=true;}Require(rejected,"invalid CSV rejected");}
 }finally{UnityEngine.Object.DestroyImmediate(host);Directory.Delete(folder,true);}
}
}
}
