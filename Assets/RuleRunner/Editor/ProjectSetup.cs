using RuleRunner.App;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RuleRunner.EditorTools
{
    /// <summary>Creates the single game scene and applies the Player Settings the WebGL build needs.</summary>
    public static class ProjectSetup
    {
        public const string ScenePath = "Assets/RuleRunner/Scenes/Main.unity";

        [MenuItem("Rule Runner/Create Main Scene")]
        public static void CreateMainScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)) { tag = "MainCamera" };
            var cam = camGo.GetComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color32(0x1d, 0x23, 0x2e, 0xff);
            camGo.transform.position = new Vector3(0, 0, -10);

            new GameObject("Game", typeof(GameController));

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Debug.Log("[RuleRunner] Created " + ScenePath + " and set it as the only build scene.");
        }

        [MenuItem("Rule Runner/Apply Player Settings")]
        public static void ApplyPlayerSettings()
        {
            PlayerSettings.companyName = "CSCI526 Team";
            PlayerSettings.productName = "Rule Runner";
            // GitHub Pages cannot send Unity's Content-Encoding headers, so ship uncompressed files.
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            PlayerSettings.WebGL.decompressionFallback = false;
            PlayerSettings.WebGL.dataCaching = false;
            PlayerSettings.defaultWebScreenWidth = 1280;
            PlayerSettings.defaultWebScreenHeight = 720;
            PlayerSettings.runInBackground = true;
            PlayerSettings.SplashScreen.showUnityLogo = false;
            // Full-window 16:9 canvas with a loading bar (Assets/WebGLTemplates/RuleRunner).
            PlayerSettings.WebGL.template = "PROJECT:RuleRunner";
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL, ManagedStrippingLevel.Medium);
            Debug.Log("[RuleRunner] Player Settings applied (WebGL compression disabled).");
        }
    }
}
