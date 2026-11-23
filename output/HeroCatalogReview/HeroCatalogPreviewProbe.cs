#if DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEngine;
namespace AFKTrial
{
    public sealed class HeroCatalogPreviewProbe : MonoBehaviour
    {
        const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic;
        TrialRunner runner;
        void Set(string key, object value) => typeof(TrialRunner).GetField(key, F).SetValue(runner, value);
        object Call(string key, params object[] args) => typeof(TrialRunner).GetMethod(key, F).Invoke(runner, args);
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-catalog-preview") >= 0)
                new GameObject("Isolated catalog preview").AddComponent<HeroCatalogPreviewProbe>();
        }
        IEnumerator Start()
        {
            yield return null;
            runner = FindAnyObjectByType<TrialRunner>();
            var args = Environment.GetCommandLineArgs();
            string folder = args[Array.IndexOf(args, "-catalog-preview") + 1];
            Directory.CreateDirectory(folder);
            Call("OpenProgressSessionAt", Path.Combine(folder, "profile"));
            var battle = (TrialBattle)typeof(TrialRunner).GetField("battle", F).GetValue(runner);
            var saved = battle.CaptureProgress(); saved.guideSeen = saved.guideReady = 63;
            battle.RestoreProgress(saved);
            Set("atTitle", false); Set("editing", true); Set("activeGuide", -1);
            foreach (var unit in battle.Units) if (!unit.Ally) unit.Hp = 100000000;
            Screen.SetResolution(960, 540, FullScreenMode.Windowed);
            yield return new WaitForSecondsRealtime(2);
            int hero = battle.Data.Heroes.FindIndex(h => h.Id == "axe_warrior");
            Call("ShowKnightDossier", hero);
            for (int view = 0; view < 3; view++)
            {
                Set("selectedSkill", view == 1 ? 0 : -1);
                if (view == 2) Set("heroView", Enum.Parse(typeof(TrialRunner).GetNestedType("HeroView", BindingFlags.NonPublic), "List"));
                yield return new WaitForSecondsRealtime(.3f);
                yield return new WaitForEndOfFrame();
                var texture = ScreenCapture.CaptureScreenshotAsTexture();
                File.WriteAllBytes(Path.Combine(folder, "view-" + view + ".png"), texture.EncodeToPNG());
                Destroy(texture);
            }
            File.WriteAllText(Path.Combine(folder, "capture-complete.txt"), "Landscape dossier, skill, catalog captured.");
            Application.Quit();
        }
    }
}
#endif
