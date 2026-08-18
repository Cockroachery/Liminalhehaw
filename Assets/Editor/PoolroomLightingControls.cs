using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;

[Serializable]
internal sealed class PoolroomRoomLightBaseline
{
    public string globalObjectId;
    public float intensity;
}

[FilePath("ProjectSettings/PoolroomLightingControls.asset", FilePathAttribute.Location.ProjectFolder)]
internal sealed class PoolroomLightingState : ScriptableSingleton<PoolroomLightingState>
{
    public float roomBrightness = 1f;
    public List<PoolroomRoomLightBaseline> roomLightBaselines = new List<PoolroomRoomLightBaseline>();

    public void SaveState()
    {
        Save(true);
    }
}

internal sealed class PoolroomLightingControls : EditorWindow
{
    private const string ScenePath = "Assets/OutdoorsScene.unity";
    private const string BloomProfilePath = "Assets/Poolroom/Materials/Underwater Red Bloom.asset";
    private const string WallMaterialPath = "Assets/Poolroom/Cracks/Underwater Crack Wall Glow.mat";
    private const string FloorMaterialPath = "Assets/Poolroom/Cracks/Underwater Crack Sprite Glow.mat";
    private const string WallTubePrefabPath = "Assets/Poolroom/Cracks/Lighting/Wall Crack Tube Light.prefab";
    private const string FloorTubePrefabPath = "Assets/Poolroom/Cracks/Lighting/Floor Crack Tube Light.prefab";
    private const string WallBeamPrefabPath = "Assets/Poolroom/Cracks/Lighting/Wall Crack Light Beam.prefab";
    private const string FloorBeamPrefabPath = "Assets/Poolroom/Cracks/Lighting/Floor Crack Light Beam.prefab";

    private sealed class CrackValues
    {
        public string title;
        public string description;
        public string materialPath;
        public string tubePrefabPath;
        public string beamPrefabPath;
        public float glow;
        public float halo;
        public float haloWidth;
        public float castBrightness;
        public float castReach;
        public float beamBrightness;
        public float beamLength;
    }

    private Vector2 scroll;
    private float overallBloom;
    private float chromaticAberration;
    private float visualNoise;
    private float noiseResponse;
    private FilmGrainLookup noiseScale;
    private CrackValues wallValues;
    private CrackValues floorValues;

    private static readonly string[] NoiseScaleNames =
    {
        "Fine 1",
        "Fine 2",
        "Medium 1",
        "Medium 2",
        "Medium 3",
        "Medium 4",
        "Medium 5",
        "Medium 6",
        "Large 1",
        "Large 2"
    };

    private static readonly FilmGrainLookup[] NoiseScales =
    {
        FilmGrainLookup.Thin1,
        FilmGrainLookup.Thin2,
        FilmGrainLookup.Medium1,
        FilmGrainLookup.Medium2,
        FilmGrainLookup.Medium3,
        FilmGrainLookup.Medium4,
        FilmGrainLookup.Medium5,
        FilmGrainLookup.Medium6,
        FilmGrainLookup.Large01,
        FilmGrainLookup.Large02
    };

    [MenuItem("Liminal Poolroom/Lighting Controls", false, 1)]
    internal static void OpenWindow()
    {
        PoolroomLightingControls window = GetWindow<PoolroomLightingControls>();
        window.titleContent = new GUIContent("Poolroom Lighting");
        window.minSize = new Vector2(420f, 650f);
        window.Show();
    }

    private void OnEnable()
    {
        wallValues = new CrackValues
        {
            title = "Wall Cracks",
            description = "Only changes the seven cracks mounted on walls.",
            materialPath = WallMaterialPath,
            tubePrefabPath = WallTubePrefabPath,
            beamPrefabPath = WallBeamPrefabPath
        };
        floorValues = new CrackValues
        {
            title = "Floor Cracks",
            description = "Only changes the five cracks lying on the pool floor.",
            materialPath = FloorMaterialPath,
            tubePrefabPath = FloorTubePrefabPath,
            beamPrefabPath = FloorBeamPrefabPath
        };

        LoadSavedValues();
        EnsureRoomLightBaselines();
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Liminal Poolroom Lighting", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "These sliders preview immediately. Wall cracks and floor cracks use separate materials and lights, so one group will not alter the other.",
            MessageType.Info);

        bool sceneIsOpen = SceneManager.GetActiveScene().path == ScenePath;
        if (!sceneIsOpen)
        {
            EditorGUILayout.HelpBox("Open the poolroom scene to adjust the room lights.", MessageType.Warning);
            if (GUILayout.Button("Open Poolroom Scene"))
                OpenPoolroomScene();
        }

        DrawRoomSection(sceneIsOpen);
        EditorGUILayout.Space(8f);
        DrawCrackSection(wallValues, new Color(1f, 0.72f, 0.72f));
        EditorGUILayout.Space(8f);
        DrawCrackSection(floorValues, new Color(1f, 0.86f, 0.72f));
        EditorGUILayout.Space(12f);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Reload Saved Values", GUILayout.Height(28f)))
                LoadSavedValues();
            if (GUILayout.Button("Save Scene and Assets", GUILayout.Height(28f)))
                SaveEverything();
        }

        EditorGUILayout.Space(8f);
        EditorGUILayout.EndScrollView();
    }

    private void DrawRoomSection(bool sceneIsOpen)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Room Lights", EditorStyles.boldLabel);
            int roomLightCount = sceneIsOpen ? FindRoomLights().Count : 0;
            EditorGUILayout.LabelField(sceneIsOpen
                ? $"Adjusts {roomLightCount} regular room and pool lights. Crack lights are excluded."
                : "Room controls become available when the poolroom scene is open.", EditorStyles.wordWrappedMiniLabel);

            using (new EditorGUI.DisabledScope(!sceneIsOpen))
            {
                float oldRoomBrightness = PoolroomLightingState.instance.roomBrightness;
                float newRoomBrightness = EditorGUILayout.Slider(
                    new GUIContent("Room Brightness", "Scales the normal room lights while preserving the differences between them."),
                    oldRoomBrightness, 0f, 2.5f);
                if (!Mathf.Approximately(oldRoomBrightness, newRoomBrightness))
                    ApplyRoomBrightness(newRoomBrightness);

                if (GUILayout.Button("Use Current Room Brightness as 100%"))
                    CaptureCurrentRoomLightsAsBaseline();
            }

            float newBloom = EditorGUILayout.Slider(
                new GUIContent("Overall Bloom", "The soft glow around bright objects throughout the room."),
                overallBloom, 0f, 0.5f);
            if (!Mathf.Approximately(overallBloom, newBloom))
            {
                overallBloom = newBloom;
                ApplyBloom();
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Player Camera Effects", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "Adds subtle lens color separation and moving film-like noise to the player's view.",
                EditorStyles.wordWrappedMiniLabel);

            EditorGUI.BeginChangeCheck();
            float newChromaticAberration = EditorGUILayout.Slider(
                new GUIContent("Color Fringing", "Separates colors near the edges of the player camera. Zero turns it off."),
                chromaticAberration, 0f, 1f);
            float newVisualNoise = EditorGUILayout.Slider(
                new GUIContent("Visual Noise", "Strength of the animated film-grain noise. Zero turns it off."),
                visualNoise, 0f, 1f);
            int currentNoiseScale = Mathf.Max(0, Array.IndexOf(NoiseScales, noiseScale));
            int newNoiseScale = EditorGUILayout.Popup(
                new GUIContent("Noise Scale", "Selects whether the visible noise specks are fine, medium, or large."),
                currentNoiseScale,
                NoiseScaleNames);
            float newNoiseResponse = EditorGUILayout.Slider(
                new GUIContent("Bright-Area Noise Reduction", "Higher values keep bright tiles cleaner while preserving more noise in dark areas."),
                noiseResponse, 0f, 1f);

            if (EditorGUI.EndChangeCheck())
            {
                chromaticAberration = newChromaticAberration;
                visualNoise = newVisualNoise;
                noiseScale = NoiseScales[Mathf.Clamp(newNoiseScale, 0, NoiseScales.Length - 1)];
                noiseResponse = newNoiseResponse;
                ApplyPlayerCameraEffects();
            }

            if (GUILayout.Button("Restore Subtle Camera Effects"))
            {
                chromaticAberration = 0.08f;
                visualNoise = 0.08f;
                noiseScale = FilmGrainLookup.Medium3;
                noiseResponse = 0.8f;
                ApplyPlayerCameraEffects();
            }
        }
    }

    private void DrawCrackSection(CrackValues values, Color labelColor)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            Color previousColor = GUI.color;
            GUI.color = labelColor;
            EditorGUILayout.LabelField(values.title, EditorStyles.boldLabel);
            GUI.color = previousColor;
            EditorGUILayout.LabelField(values.description, EditorStyles.wordWrappedMiniLabel);

            EditorGUI.BeginChangeCheck();
            values.glow = EditorGUILayout.Slider(
                new GUIContent("Crack Brightness", "Brightness of the crack picture itself."), values.glow, 0f, 6f);
            values.halo = EditorGUILayout.Slider(
                new GUIContent("Halo Brightness", "Strength of the soft red edge surrounding the crack shape."), values.halo, 0f, 3f);
            values.haloWidth = EditorGUILayout.Slider(
                new GUIContent("Halo Width", "How far the soft edge spreads away from the crack."), values.haloWidth, 1f, 10f);
            values.castBrightness = EditorGUILayout.Slider(
                new GUIContent("Cast Light", "How strongly the crack illuminates nearby tiles and water."), values.castBrightness, 0f, 200f);
            values.castReach = EditorGUILayout.Slider(
                new GUIContent("Light Reach", "How far the nearby red illumination travels."), values.castReach, 0.25f, 5f);
            values.beamBrightness = EditorGUILayout.Slider(
                new GUIContent("Fog Beam Brightness", "Brightness of the two short rays coming out of each crack."), values.beamBrightness, 0f, 150f);
            values.beamLength = EditorGUILayout.Slider(
                new GUIContent("Fog Beam Length", "How far those short rays travel through the underwater fog."), values.beamLength, 0.2f, 4f);

            if (EditorGUI.EndChangeCheck())
                ApplyCrackValues(values);

            if (GUILayout.Button($"Restore Recommended {values.title} Settings"))
            {
                RestoreRecommendedValues(values);
                ApplyCrackValues(values);
            }
        }
    }

    private void LoadSavedValues()
    {
        overallBloom = ReadBloomIntensity();
        LoadPlayerCameraEffects();
        LoadCrackValues(wallValues);
        LoadCrackValues(floorValues);
        Repaint();
    }

    private static void LoadCrackValues(CrackValues values)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(values.materialPath);
        GameObject tubePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(values.tubePrefabPath);
        GameObject beamPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(values.beamPrefabPath);
        if (material == null || tubePrefab == null || beamPrefab == null)
            return;

        Light tubeLight = tubePrefab.GetComponent<Light>();
        Light beamLight = beamPrefab.GetComponent<Light>();
        values.glow = material.GetFloat("_EmissionIntensity");
        values.halo = material.GetFloat("_HaloIntensity");
        values.haloWidth = material.GetFloat("_HaloRadius");
        values.castBrightness = tubeLight.intensity;
        values.castReach = tubeLight.range;
        values.beamBrightness = beamLight.intensity;
        values.beamLength = beamLight.range;
    }

    private static void ApplyCrackValues(CrackValues values)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(values.materialPath);
        GameObject tubePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(values.tubePrefabPath);
        GameObject beamPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(values.beamPrefabPath);
        if (material == null || tubePrefab == null || beamPrefab == null)
        {
            Debug.LogError($"Poolroom Lighting Controls could not find one or more {values.title} assets.");
            return;
        }

        Light tubeLight = tubePrefab.GetComponent<Light>();
        Light beamLight = beamPrefab.GetComponent<Light>();
        Undo.RecordObjects(new UnityEngine.Object[] { material, tubeLight, beamLight }, $"Adjust {values.title}");
        material.SetFloat("_EmissionIntensity", values.glow);
        material.SetFloat("_HaloIntensity", values.halo);
        material.SetFloat("_HaloRadius", values.haloWidth);
        tubeLight.intensity = values.castBrightness;
        tubeLight.range = values.castReach;
        beamLight.intensity = values.beamBrightness;
        beamLight.range = values.beamLength;
        EditorUtility.SetDirty(material);
        EditorUtility.SetDirty(tubeLight);
        EditorUtility.SetDirty(beamLight);
        AssetDatabase.SaveAssets();
        SceneView.RepaintAll();
    }

    private static void RestoreRecommendedValues(CrackValues values)
    {
        bool isWall = values.materialPath == WallMaterialPath;
        values.glow = isWall ? 1.55f : 2.55f;
        values.halo = isWall ? 0.28f : 0.8f;
        values.haloWidth = isWall ? 2.25f : 3.5f;
        values.castBrightness = isWall ? 45f : 55f;
        values.castReach = isWall ? 1.65f : 1.35f;
        values.beamBrightness = 42f;
        values.beamLength = 1.25f;
    }

    private static float ReadBloomIntensity()
    {
        VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(BloomProfilePath);
        if (profile != null && profile.TryGet(out Bloom bloom))
            return bloom.intensity.value;
        return 0f;
    }

    private void ApplyBloom()
    {
        VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(BloomProfilePath);
        if (profile == null || !profile.TryGet(out Bloom bloom))
            return;

        Undo.RecordObject(bloom, "Adjust Poolroom Bloom");
        bloom.intensity.value = overallBloom;
        EditorUtility.SetDirty(bloom);
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        SceneView.RepaintAll();
    }

    private void LoadPlayerCameraEffects()
    {
        VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(BloomProfilePath);
        if (profile != null &&
            profile.TryGet(out ChromaticAberration chromatic) &&
            profile.TryGet(out FilmGrain grain))
        {
            chromaticAberration = chromatic.intensity.value;
            visualNoise = grain.intensity.value;
            noiseScale = grain.type.value;
            noiseResponse = grain.response.value;
            return;
        }

        chromaticAberration = 0f;
        visualNoise = 0f;
        noiseScale = FilmGrainLookup.Medium3;
        noiseResponse = 0.8f;
    }

    private void ApplyPlayerCameraEffects()
    {
        VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(BloomProfilePath);
        if (profile == null ||
            !profile.TryGet(out ChromaticAberration chromatic) ||
            !profile.TryGet(out FilmGrain grain))
        {
            Debug.LogError("Poolroom Lighting Controls could not find the player camera effects in the global poolroom profile.");
            return;
        }

        Undo.RecordObjects(new UnityEngine.Object[] { chromatic, grain }, "Adjust Player Camera Effects");
        chromatic.intensity.value = chromaticAberration;
        grain.intensity.value = visualNoise;
        grain.type.value = noiseScale;
        grain.response.value = noiseResponse;
        EditorUtility.SetDirty(chromatic);
        EditorUtility.SetDirty(grain);
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        SceneView.RepaintAll();
    }

    private static List<Light> FindRoomLights()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != ScenePath)
            return new List<Light>();

        return scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Light>(true))
            .Where(light => !IsCrackLight(light.transform))
            .ToList();
    }

    private static bool IsCrackLight(Transform transform)
    {
        for (Transform current = transform; current != null; current = current.parent)
        {
            if (current.name == "Tube Light Components" || current.name == "Short Light Beams" ||
                current.name == "Glowing Crack Image" || current.name.StartsWith("Crack Sprite", StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    private static void EnsureRoomLightBaselines()
    {
        List<Light> lights = FindRoomLights();
        if (lights.Count == 0)
            return;

        PoolroomLightingState state = PoolroomLightingState.instance;
        float safeScale = Mathf.Max(state.roomBrightness, 0.0001f);
        bool changed = false;
        foreach (Light light in lights)
        {
            string id = GlobalObjectId.GetGlobalObjectIdSlow(light).ToString();
            if (state.roomLightBaselines.Any(record => record.globalObjectId == id))
                continue;

            state.roomLightBaselines.Add(new PoolroomRoomLightBaseline
            {
                globalObjectId = id,
                intensity = light.intensity / safeScale
            });
            changed = true;
        }

        if (changed)
            state.SaveState();
    }

    private static void ApplyRoomBrightness(float brightness)
    {
        EnsureRoomLightBaselines();
        PoolroomLightingState state = PoolroomLightingState.instance;
        List<Light> lights = FindRoomLights();
        Undo.RecordObjects(lights.Cast<UnityEngine.Object>().ToArray(), "Adjust Poolroom Room Lights");

        foreach (Light light in lights)
        {
            string id = GlobalObjectId.GetGlobalObjectIdSlow(light).ToString();
            PoolroomRoomLightBaseline baseline = state.roomLightBaselines.FirstOrDefault(record => record.globalObjectId == id);
            if (baseline != null)
            {
                light.intensity = baseline.intensity * brightness;
                EditorUtility.SetDirty(light);
            }
        }

        state.roomBrightness = brightness;
        state.SaveState();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        SceneView.RepaintAll();
    }

    private static void CaptureCurrentRoomLightsAsBaseline()
    {
        List<Light> lights = FindRoomLights();
        PoolroomLightingState state = PoolroomLightingState.instance;
        state.roomLightBaselines.Clear();
        foreach (Light light in lights)
        {
            state.roomLightBaselines.Add(new PoolroomRoomLightBaseline
            {
                globalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(light).ToString(),
                intensity = light.intensity
            });
        }
        state.roomBrightness = 1f;
        state.SaveState();
    }

    private static void OpenPoolroomScene()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
    }

    private static void SaveEverything()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.IsValid() && scene.path == ScenePath && scene.isDirty)
            EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("POOLROOM_LIGHTING_SAVED: Saved the room, camera effects, wall-crack, and floor-crack lighting settings.");
    }
}
