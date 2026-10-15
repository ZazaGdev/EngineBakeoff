using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// One-off project setup, run in batch mode:
//   Unity -batchmode -quit -projectPath unity -executeMethod Setup.Run
// Also the pre-export method for Unity Build Automation, so the cloud build gets the same settings.
public static class Setup
{
    const string Models = "Assets/Resources/Models/";
    static readonly string[] Characters = { "Knight", "Skeleton_Minion" };
    static readonly string[] Props = { "floor_tile_large", "wall", "pillar" };
    static readonly string[] States = { "Idle", "Running_A", "1H_Melee_Attack_Chop" };
    static readonly string[] Looping = { "Idle", "Running_A" };

    public static void Run()
    {
        ConfigureModels();
        MakeController("Knight", "Knight");
        MakeController("Skeleton_Minion", "Skeleton");
        ConfigureRenderPipeline();
        ConfigurePlayer();
        MakeScene();
        AssetDatabase.SaveAssets();
        Debug.Log("Setup done");
    }

    static void ConfigureModels()
    {
        foreach (var name in Characters)
        {
            var path = Models + name + ".fbx";
            var imp = (ModelImporter)AssetImporter.GetAtPath(path);
            imp.animationType = ModelImporterAnimationType.Generic;
            imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            imp.importAnimation = true;
            imp.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            var clips = imp.defaultClipAnimations;
            foreach (var c in clips) c.loopTime = Looping.Contains(c.name);
            imp.clipAnimations = clips;
            imp.SaveAndReimport();
        }
        foreach (var name in Props)
        {
            var imp = (ModelImporter)AssetImporter.GetAtPath(Models + name + ".fbx");
            imp.animationType = ModelImporterAnimationType.None;
            imp.importAnimation = false;
            imp.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            imp.SaveAndReimport();
        }
    }

    static void MakeController(string model, string controllerName)
    {
        var clips = AssetDatabase.LoadAllAssetsAtPath(Models + model + ".fbx").OfType<AnimationClip>()
            .Where(c => !c.name.StartsWith("__preview__")).ToDictionary(c => c.name);
        var path = "Assets/Resources/" + controllerName + ".controller";
        AssetDatabase.DeleteAsset(path);
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
        var sm = ctrl.layers[0].stateMachine;
        foreach (var s in States)
        {
            var state = sm.AddState(s);
            state.motion = clips[s];
            if (s == "Idle") sm.defaultState = state;
        }
    }

    static void ConfigureRenderPipeline()
    {
        const string rendererPath = "Assets/Settings/Renderer.asset";
        const string pipelinePath = "Assets/Settings/URP.asset";
        Directory.CreateDirectory("Assets/Settings");
        var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
        if (renderer == null)
        {
            renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
            AssetDatabase.CreateAsset(renderer, rendererPath);
        }
        var urp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelinePath);
        if (urp == null)
        {
            urp = UniversalRenderPipelineAsset.Create(renderer);
            AssetDatabase.CreateAsset(urp, pipelinePath);
        }
        urp.msaaSampleCount = 1;
        urp.renderScale = 1f;
        urp.supportsHDR = false;
        urp.shadowDistance = 30f;
        urp.mainLightShadowmapResolution = 2048;
        urp.shadowCascadeCount = 1;
        var urpSo = new SerializedObject(urp);
        urpSo.FindProperty("m_MainLightShadowsSupported").boolValue = true;
        urpSo.FindProperty("m_AdditionalLightsRenderingMode").intValue = (int)LightRenderingMode.Disabled;
        urpSo.ApplyModifiedProperties();
        EditorUtility.SetDirty(urp);
        GraphicsSettings.defaultRenderPipeline = urp;
        for (int i = 0; i < QualitySettings.names.Length; i++)
        {
            QualitySettings.SetQualityLevel(i, false);
            QualitySettings.renderPipeline = urp;
            QualitySettings.vSyncCount = 0;
        }
    }

    static void ConfigurePlayer()
    {
        PlayerSettings.companyName = "zazag";
        PlayerSettings.productName = "Bakeoff Unity";
        PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.iOS, "dev.zazag.bakeoffunity");
        PlayerSettings.bundleVersion = "1.0";
        PlayerSettings.runInBackground = true; // desktop check runs unfocused
        PlayerSettings.defaultIsNativeResolution = false;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.iOS.targetOSVersionString = "15.0";
        PlayerSettings.iOS.appleEnableAutomaticSigning = true;
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
        PlayerSettings.allowedAutorotateToPortrait = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true;
        PlayerSettings.allowedAutorotateToLandscapeRight = true;
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.iOS, true);
        // Old input manager: the HUD reads touches directly.
        var ps = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0];
        var so = new SerializedObject(ps);
        var input = so.FindProperty("activeInputHandler");
        if (input != null) { input.intValue = 0; so.ApplyModifiedProperties(); }

        var icon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/icon.png");
        if (icon != null) PlayerSettings.SetIcons(UnityEditor.Build.NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
    }

    static void MakeScene()
    {
        const string scenePath = "Assets/Main.unity";
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        new GameObject("Main").AddComponent<Main>();
        EditorSceneManager.SaveScene(scene, scenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scenePath, true) };
    }

    // Local check: Unity -batchmode -quit -projectPath unity -executeMethod Setup.BuildWindows
    public static void BuildWindows()
    {
        var r = BuildPipeline.BuildPlayer(new[] { "Assets/Main.unity" }, "Builds/Win/BakeoffUnity.exe",
            BuildTarget.StandaloneWindows64, BuildOptions.None);
        if (r.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) EditorApplication.Exit(1);
    }

    // Local check that the iPhone Xcode project exports: -executeMethod Setup.BuildIOS
    public static void BuildIOS()
    {
        var r = BuildPipeline.BuildPlayer(new[] { "Assets/Main.unity" }, "Builds/iOS",
            BuildTarget.iOS, BuildOptions.None);
        Debug.Log($"iOS export {r.summary.result}, {r.summary.totalSize} bytes");
        if (r.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) EditorApplication.Exit(1);
    }
}
