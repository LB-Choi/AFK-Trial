using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine;

namespace AFKTrial.Editor
{
    // Editor-only setup. No gameplay code or changes to the template scene.
    internal static class TrialProjectSetup
    {
        private const string Receipt = "Library/AFKTrialSetup-v2.txt";

        [InitializeOnLoadMethod]
        private static void ScheduleSetup()
        {
            EditorApplication.delayCall += ApplyOnFirstImport;
            EditorApplication.delayCall += ConfigurePlayScene;
            EditorSceneManager.activeSceneChangedInEditMode += OnActiveSceneChanged;
        }

        private static void OnActiveSceneChanged(Scene previous, Scene current)
        {
            ConfigurePlayScene();
        }

        private static void ConfigurePlayScene()
        {
            string path = SceneManager.GetActiveScene().path == "Assets/Scenes/SampleScene.unity"
                ? "Assets/Scenes/SampleScene.unity" : "Assets/Scenes/SideBattle.unity";
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
        }

        private static void ApplyOnFirstImport()
        {
            if (File.Exists(Receipt) || EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            Apply();
        }

        [MenuItem("AFK Trial/Apply Test Project Settings")]
        private static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[AFK Trial] Exit Play Mode before applying project settings.");
                return;
            }

            EditorSettings.serializationMode = SerializationMode.ForceText;
            PlayerSettings.productName = "Lords of Fame";
            PlayerSettings.defaultScreenWidth = 960;
            PlayerSettings.defaultScreenHeight = 540;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = false;
            AssetDatabase.SaveAssets();

            Directory.CreateDirectory("Library");
            File.WriteAllText(Receipt, "Applied test project settings with Unity " + Application.unityVersion);
            Debug.Log("[AFK Trial] Test setup applied: 960x540 windowed, Force Text. " +
                          "This confirms editor setup only, not gameplay or build validation.");
        }
    }
}
