using System;
using System.Reflection;
using UnityEngine;

namespace AFKTrial.Editor
{
    internal static class TrialHeroCatalogChecks
    {
        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException("Hero catalog: " + message); }

        public static void Check(TrialData data)
        {
            var battle = new TrialBattle(data);
            battle.CreditIdle(100000, 100000);
            string before = JsonUtility.ToJson(battle.CaptureProgress());
            var host = new GameObject("Isolated hero catalog check");
            host.SetActive(false);
            var runner = host.AddComponent<TrialRunner>();
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
            object Call(string name, params object[] args) => typeof(TrialRunner).GetMethod(name, flags).Invoke(runner, args);
            object Get(string name) => typeof(TrialRunner).GetField(name, flags).GetValue(runner);
            typeof(TrialRunner).GetField("battle", flags).SetValue(runner, battle);
            int missing = 0;
            try
            {
                for (int hero = 0; hero < data.Heroes.Count; hero++)
                {
                    if (battle.Owned(hero)) continue;
                    missing++;
                    Call("ShowKnightDossier", hero);
                    Require((int)Get("featureHero") == hero && Get("heroView").ToString() == "Info", "locked hero opens details");
                    string route = (string)Call("HeroAcquisitionText", hero);
                    Require(route.Contains("소환") && route.Contains(data.SummonRates[hero].ToString("0.####", System.Globalization.CultureInfo.InvariantCulture) + "%"), "rate uses live table");
                    bool recruit = false;
                    foreach (var reward in data.FirstClearRecruits)
                        if (reward.Value == hero)
                        { recruit = true; Require(route.Contains("메인 " + reward.Key + "스테이지"), "recruit path uses live table"); }
                    Require(recruit || !route.Contains("최초 클리어"), "no invented clear reward");
                    Require(!battle.UpgradeHero(hero), "locked level-up rejected");
                    for (int slot = 0; slot < 3; slot++)
                    {
                        Require(battle.SkillPreview(hero, slot, 1).Count > 0, "level-one preview available");
                        Require(!battle.Train(hero, slot), "locked skill upgrade rejected");
                    }
                    int[] draft = (int[])Get("draft");
                    int old = draft[0];
                    Call("PlaceHero", hero, 0);
                    Require(draft[0] == old, "locked placement leaves draft unchanged");
                    Call("BeginHeroPlacement");
                    Require(Get("heroView").ToString() == "Info", "locked formation screen blocked");
                    bool rejected = false;
                    try { battle.ApplyFormation(new[] { hero, -1, -1 }); }
                    catch (ArgumentException) { rejected = true; }
                    Require(rejected, "model also rejects locked formation");
                }
                Require(missing > 0 && JsonUtility.ToJson(battle.CaptureProgress()) == before, "viewing and blocked actions never mutate progress");
                int owned = battle.FirstDeployedHero;
                Call("ShowKnightDossier", owned);
                Call("BeginHeroPlacement");
                Require(Get("heroView").ToString() == "Formation" && battle.UpgradeHero(owned), "owned growth and formation preserved");
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }
    }
}
