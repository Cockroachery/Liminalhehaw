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

[Serializable]
internal sealed class PoolroomCrackPresetValues
{
    public float glow;
    public float halo;
    public float haloWidth;
    public float castBrightness;
    public float castReach;
    public float beamBrightness;
    public float beamLength;
}

[Serializable]
internal sealed class PoolroomLightingPreset
{
    public string presetName;
    public float roomBrightness;
    public float poolBrightness = 1f;
    public float overallBloom;
    public float chromaticAberration;
    public float visualNoise;
    public float noiseResponse;
    public int noiseScale;
    public float fisheyeStrength;
    public float cameraFieldOfView;
    public Color cameraColorFilter = Color.white;
    public float cameraSaturation;
    public float cameraContrast;
    public float cameraHueShift;
    public float cameraExposure;
    public float cameraTemperature;
    public float cameraTint;
    public float cameraVignette;
    public PoolroomCrackPresetValues wallCracks = new PoolroomCrackPresetValues();
    public PoolroomCrackPresetValues floorCracks = new PoolroomCrackPresetValues();
}

[FilePath("ProjectSettings/PoolroomLightingControls.asset", FilePathAttribute.Location.ProjectFolder)]
internal sealed class PoolroomLightingState : ScriptableSingleton<PoolroomLightingState>
{
    public int dataVersion;
    public float roomBrightness = 1f;
    public float poolBrightness = 1f;
    public List<PoolroomRoomLightBaseline> roomLightBaselines = new List<PoolroomRoomLightBaseline>();
    public List<PoolroomRoomLightBaseline> poolLightBaselines = new List<PoolroomRoomLightBaseline>();

    public void SaveState()
    {
        Save(true);
    }
}

[FilePath("ProjectSettings/PoolroomLightingPresets.asset", FilePathAttribute.Location.ProjectFolder)]
internal sealed class PoolroomLightingPresetState : ScriptableSingleton<PoolroomLightingPresetState>
{
    public int dataVersion;
    public bool hasLastSavedValues;
    public PoolroomLightingPreset lastSavedValues;
    public List<PoolroomLightingPreset> namedPresets = new List<PoolroomLightingPreset>();

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
    private const string PoolSurfaceMaterialPath = "Assets/Poolroom/Materials/Pool Interior Light Sheet.mat";
    private const string PoolCornerMaterialPath = "Assets/Poolroom/Materials/Pool Interior Corner Fill.mat";
    private const string PoolInteriorLightSheetRootName = "Pool Interior Light Sheets";
    private const string SelectedPageKey = "LiminalPoolroom.LightingControls.SelectedPage";
    private const int SignedFisheyePresetVersion = 1;
    private const int SeparatePoolLightingPresetVersion = 2;
    private const int SeparatePoolLightingStateVersion = 1;
    private const int PoolSurfaceGlowStateVersion = 2;
    private const int PoolInteriorLightSheetStateVersion = 3;

    private static readonly string[] PageNames =
    {
        "Room",
        "Cracks",
        "Camera",
        "Presets"
    };

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
    private int selectedPage;
    private float overallBloom;
    private float chromaticAberration;
    private float visualNoise;
    private float noiseResponse;
    private FilmGrainLookup noiseScale;
    private float fisheyeStrength;
    private float cameraFieldOfView = 60f;
    private Color cameraColorFilter = Color.white;
    private float cameraSaturation;
    private float cameraContrast;
    private float cameraHueShift;
    private float cameraExposure;
    private float cameraTemperature;
    private float cameraTint;
    private float cameraVignette;
    private bool showColorFilters = true;
    private int selectedFilterPreset;
    private int selectedNamedPreset;
    private string newPresetName = "My Poolroom Look";
    private string presetMessage;
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

    private static readonly string[] FilterPresetNames =
    {
        "Neutral",
        "Creepy Red",
        "Cold Liminal",
        "Sickly Green",
        "Faded VHS"
    };

    private static void MigrateLightingState()
    {
        PoolroomLightingState state = PoolroomLightingState.instance;
        if (state.dataVersion >= PoolInteriorLightSheetStateVersion)
            return;

        state.roomLightBaselines ??= new List<PoolroomRoomLightBaseline>();
        state.poolLightBaselines ??= new List<PoolroomRoomLightBaseline>();

        if (state.dataVersion < SeparatePoolLightingStateVersion)
        {
            state.poolBrightness = state.roomBrightness;
            HashSet<string> poolLightIds = new HashSet<string>(FindPoolLights()
                .Select(light => GlobalObjectId.GetGlobalObjectIdSlow(light).ToString()));
            foreach (PoolroomRoomLightBaseline baseline in state.roomLightBaselines.Where(
                         baseline => poolLightIds.Contains(baseline.globalObjectId)).ToList())
            {
                state.poolLightBaselines.Add(new PoolroomRoomLightBaseline
                {
                    globalObjectId = baseline.globalObjectId,
                    intensity = baseline.intensity
                });
            }
            state.roomLightBaselines.RemoveAll(baseline => poolLightIds.Contains(baseline.globalObjectId));
        }

        if (state.dataVersion < PoolSurfaceGlowStateVersion)
            state.poolBrightness = ReadCurrentPoolBrightness(state);

        state.dataVersion = PoolInteriorLightSheetStateVersion;
        state.SaveState();
    }

    private static void MigratePresetValues()
    {
        PoolroomLightingPresetState state = PoolroomLightingPresetState.instance;
        if (state.dataVersion >= SeparatePoolLightingPresetVersion)
            return;

        List<PoolroomLightingPreset> presets = new List<PoolroomLightingPreset>();
        if (state.lastSavedValues != null)
            presets.Add(state.lastSavedValues);
        if (state.namedPresets != null)
            presets.AddRange(state.namedPresets.Where(preset => preset != null));

        foreach (PoolroomLightingPreset preset in presets)
        {
            if (state.dataVersion < SignedFisheyePresetVersion)
                preset.fisheyeStrength = Mathf.Clamp(-preset.fisheyeStrength, -1f, 1f);
            preset.poolBrightness = preset.roomBrightness;
        }

        state.dataVersion = SeparatePoolLightingPresetVersion;
        state.SaveState();
    }

    [MenuItem("Liminal Poolroom/Lighting Controls", false, 1)]
    internal static void OpenWindow()
    {
        PoolroomLightingControls window = GetWindow<PoolroomLightingControls>();
        window.titleContent = new GUIContent("Poolroom Lighting");
        window.minSize = new Vector2(500f, 650f);
        window.Show();
    }

    private void OnEnable()
    {
        MigrateLightingState();
        MigratePresetValues();
        selectedPage = Mathf.Clamp(EditorPrefs.GetInt(SelectedPageKey, 0), 0, PageNames.Length - 1);
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
        EnsurePoolLightBaselines();
        ApplyPoolSurfaceGlow(PoolroomLightingState.instance.poolBrightness, false);
        EnsureLastSavedValues();
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Liminal Poolroom Lighting", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Choose a page below. Every slider previews immediately, and each crack group remains separate from the others.",
            MessageType.Info);

        bool sceneIsOpen = SceneManager.GetActiveScene().path == ScenePath;
        if (!sceneIsOpen)
        {
            EditorGUILayout.HelpBox("Open the poolroom scene to adjust the room lights.", MessageType.Warning);
            if (GUILayout.Button("Open Poolroom Scene"))
                OpenPoolroomScene();
        }

        int newPage = GUILayout.Toolbar(selectedPage, PageNames, GUILayout.Height(26f));
        if (newPage != selectedPage)
        {
            selectedPage = newPage;
            scroll = Vector2.zero;
            EditorPrefs.SetInt(SelectedPageKey, selectedPage);
            GUI.FocusControl(null);
        }

        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.Space(8f);

        switch (selectedPage)
        {
            case 0:
                DrawRoomSection(sceneIsOpen);
                break;
            case 1:
                DrawCracksSection();
                break;
            case 2:
                DrawCameraSection(sceneIsOpen);
                break;
            case 3:
                DrawPresetSection();
                break;
        }

        EditorGUILayout.Space(12f);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Reload Saved Values", GUILayout.Height(28f)))
                ReloadSavedValues();
            if (GUILayout.Button("Save Scene and Assets", GUILayout.Height(28f)))
                SaveEverything();
        }

        EditorGUILayout.Space(8f);
        EditorGUILayout.EndScrollView();
    }

    private void DrawCracksSection()
    {
        EditorGUILayout.HelpBox(
            "Wall and floor cracks remain separate below, so changing one group will not change the other.",
            MessageType.Info);

        DrawCrackSection(wallValues, new Color(1f, 0.72f, 0.72f));
        EditorGUILayout.Space(8f);
        DrawCrackSection(floorValues, new Color(1f, 0.86f, 0.72f));
    }

    private void DrawRoomSection(bool sceneIsOpen)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Room Lights", EditorStyles.boldLabel);
            int roomLightCount = sceneIsOpen ? FindRoomLights().Count : 0;
            EditorGUILayout.LabelField(sceneIsOpen
                ? $"Adjusts {roomLightCount} regular room lights. Pool and crack lights are excluded."
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
        }

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Pool Surface Lighting", EditorStyles.boldLabel);
            int poolSheetCount = sceneIsOpen ? FindPoolInteriorLightSheets().Count : 0;
            EditorGUILayout.LabelField(sceneIsOpen
                ? $"Adjusts {poolSheetCount} thin light sheets placed only on the underwater floor and inner walls. Exterior and crack lighting are excluded."
                : "Pool-light controls become available when the poolroom scene is open.", EditorStyles.wordWrappedMiniLabel);

            using (new EditorGUI.DisabledScope(!sceneIsOpen))
            {
                float oldPoolBrightness = PoolroomLightingState.instance.poolBrightness;
                float newPoolBrightness = EditorGUILayout.Slider(
                    new GUIContent("Pool Brightness", "Controls only the transparent light sheets lining the inside of the underwater pool."),
                    oldPoolBrightness, 0f, 2.5f);
                if (!Mathf.Approximately(oldPoolBrightness, newPoolBrightness))
                    ApplyPoolBrightness(newPoolBrightness);

                if (GUILayout.Button("Reset Pool Brightness to 100%"))
                    ApplyPoolBrightness(1f);
            }
        }
    }

    private void DrawCameraSection(bool sceneIsOpen)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Player Camera Effects", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "Adjusts the player's lens, viewing width, moving noise, and final color mood.",
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
            float newFisheyeStrength = EditorGUILayout.Slider(
                new GUIContent("Fisheye Strength", "Negative values bend the view outward like a wide fisheye lens. Positive values pinch the view inward. Zero turns distortion off."),
                fisheyeStrength, -1f, 1f);
            float newFieldOfView = cameraFieldOfView;
            using (new EditorGUI.DisabledScope(!sceneIsOpen))
            {
                newFieldOfView = EditorGUILayout.Slider(
                    new GUIContent("Field of View", "Controls how wide the player's view is. Higher values show more of the room; lower values feel zoomed in."),
                    cameraFieldOfView, 40f, 120f);
            }

            Color newColorFilter = cameraColorFilter;
            float newSaturation = cameraSaturation;
            float newContrast = cameraContrast;
            float newHueShift = cameraHueShift;
            float newExposure = cameraExposure;
            float newTemperature = cameraTemperature;
            float newTint = cameraTint;
            float newVignette = cameraVignette;

            EditorGUILayout.Space(5f);
            showColorFilters = EditorGUILayout.Foldout(showColorFilters, "Color Filter & Mood", true);
            if (showColorFilters)
            {
                EditorGUILayout.LabelField(
                    "Tint and reshape every color seen through the player camera. White and zero values are neutral.",
                    EditorStyles.wordWrappedMiniLabel);
                newColorFilter = EditorGUILayout.ColorField(
                    new GUIContent("Filter Color", "Multiplies the finished camera image by this color. White leaves colors unchanged."),
                    cameraColorFilter,
                    true,
                    false,
                    false);
                newSaturation = EditorGUILayout.Slider(
                    new GUIContent("Saturation", "Negative values remove color; positive values make colors stronger."),
                    cameraSaturation, -100f, 100f);
                newContrast = EditorGUILayout.Slider(
                    new GUIContent("Contrast", "Changes the difference between dark and bright areas."),
                    cameraContrast, -100f, 100f);
                newHueShift = EditorGUILayout.Slider(
                    new GUIContent("Hue Shift", "Rotates every color around the color wheel."),
                    cameraHueShift, -180f, 180f);
                newExposure = EditorGUILayout.Slider(
                    new GUIContent("Camera Exposure", "Brightens or darkens the final camera image without changing the actual lights."),
                    cameraExposure, -3f, 3f);
                newTemperature = EditorGUILayout.Slider(
                    new GUIContent("Temperature", "Negative values feel colder and blue; positive values feel warmer and orange."),
                    cameraTemperature, -100f, 100f);
                newTint = EditorGUILayout.Slider(
                    new GUIContent("Green / Magenta Tint", "Negative values add green; positive values add magenta."),
                    cameraTint, -100f, 100f);
                newVignette = EditorGUILayout.Slider(
                    new GUIContent("Darkened Edges", "Adds a soft dark frame around the edges of the player's view."),
                    cameraVignette, 0f, 1f);
            }

            if (EditorGUI.EndChangeCheck())
            {
                chromaticAberration = newChromaticAberration;
                visualNoise = newVisualNoise;
                noiseScale = NoiseScales[Mathf.Clamp(newNoiseScale, 0, NoiseScales.Length - 1)];
                noiseResponse = newNoiseResponse;
                fisheyeStrength = newFisheyeStrength;
                bool fieldOfViewChanged = !Mathf.Approximately(cameraFieldOfView, newFieldOfView);
                cameraFieldOfView = newFieldOfView;
                cameraColorFilter = newColorFilter;
                cameraSaturation = newSaturation;
                cameraContrast = newContrast;
                cameraHueShift = newHueShift;
                cameraExposure = newExposure;
                cameraTemperature = newTemperature;
                cameraTint = newTint;
                cameraVignette = newVignette;
                ApplyPlayerCameraEffects();
                if (fieldOfViewChanged)
                    ApplyPlayerCameraFieldOfView();
            }

            if (showColorFilters)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    selectedFilterPreset = EditorGUILayout.Popup(
                        new GUIContent("Mood Preset", "Choose a ready-made color style, then click Apply."),
                        selectedFilterPreset,
                        FilterPresetNames);
                    if (GUILayout.Button("Apply", GUILayout.Width(70f)))
                        ApplyFilterPreset(selectedFilterPreset);
                }

                if (GUILayout.Button("Reset Only Color Filter to Neutral"))
                {
                    SetNeutralColorFilter();
                    ApplyPlayerCameraEffects();
                }
            }

            if (GUILayout.Button("Reset All Camera Effects to Off"))
            {
                chromaticAberration = 0f;
                visualNoise = 0f;
                noiseScale = FilmGrainLookup.Thin1;
                noiseResponse = 0f;
                fisheyeStrength = 0f;
                cameraFieldOfView = 60f;
                SetNeutralColorFilter();
                ApplyPlayerCameraEffects();
                if (sceneIsOpen)
                    ApplyPlayerCameraFieldOfView();
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

    private void DrawPresetSection()
    {
        PoolroomLightingPresetState presetState = PoolroomLightingPresetState.instance;
        if (presetState.namedPresets == null)
            presetState.namedPresets = new List<PoolroomLightingPreset>();

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Named Lighting Presets", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "A preset captures every page: separate room and pool brightness, bloom, camera effects, and both crack groups.",
                EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.HelpBox(
                "Applying a preset is a live preview. Click Save Scene and Assets afterward if you want Reload Saved Values to return to it.",
                MessageType.Info);

            newPresetName = EditorGUILayout.TextField(
                new GUIContent("Preset Name", "The name shown in the saved preset list."),
                newPresetName);
            if (GUILayout.Button("Save Current Values as Named Preset", GUILayout.Height(28f)))
                SaveCurrentNamedPreset();

            EditorGUILayout.Space(8f);
            if (presetState.namedPresets.Count == 0)
            {
                EditorGUILayout.HelpBox("No named presets have been saved yet.", MessageType.None);
            }
            else
            {
                selectedNamedPreset = Mathf.Clamp(selectedNamedPreset, 0, presetState.namedPresets.Count - 1);
                string[] names = presetState.namedPresets.Select(preset => preset.presetName).ToArray();
                selectedNamedPreset = EditorGUILayout.Popup("Saved Preset", selectedNamedPreset, names);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Apply Selected"))
                        ApplySelectedNamedPreset();
                    if (GUILayout.Button("Overwrite Selected"))
                        OverwriteSelectedNamedPreset();
                    if (GUILayout.Button("Delete Selected"))
                        DeleteSelectedNamedPreset();
                }
            }

            if (!string.IsNullOrEmpty(presetMessage))
            {
                EditorGUILayout.Space(5f);
                EditorGUILayout.HelpBox(presetMessage, MessageType.None);
            }
        }
    }

    private void EnsureLastSavedValues()
    {
        PoolroomLightingPresetState presetState = PoolroomLightingPresetState.instance;
        if (presetState.namedPresets == null)
            presetState.namedPresets = new List<PoolroomLightingPreset>();

        if (presetState.hasLastSavedValues && presetState.lastSavedValues != null)
            return;

        presetState.lastSavedValues = CaptureCurrentValues("Last Saved Values");
        presetState.hasLastSavedValues = true;
        presetState.SaveState();
    }

    private PoolroomLightingPreset CaptureCurrentValues(string presetName)
    {
        return new PoolroomLightingPreset
        {
            presetName = presetName,
            roomBrightness = PoolroomLightingState.instance.roomBrightness,
            poolBrightness = PoolroomLightingState.instance.poolBrightness,
            overallBloom = overallBloom,
            chromaticAberration = chromaticAberration,
            visualNoise = visualNoise,
            noiseResponse = noiseResponse,
            noiseScale = (int)noiseScale,
            fisheyeStrength = fisheyeStrength,
            cameraFieldOfView = cameraFieldOfView,
            cameraColorFilter = cameraColorFilter,
            cameraSaturation = cameraSaturation,
            cameraContrast = cameraContrast,
            cameraHueShift = cameraHueShift,
            cameraExposure = cameraExposure,
            cameraTemperature = cameraTemperature,
            cameraTint = cameraTint,
            cameraVignette = cameraVignette,
            wallCracks = CaptureCrackValues(wallValues),
            floorCracks = CaptureCrackValues(floorValues)
        };
    }

    private static PoolroomCrackPresetValues CaptureCrackValues(CrackValues values)
    {
        return new PoolroomCrackPresetValues
        {
            glow = values.glow,
            halo = values.halo,
            haloWidth = values.haloWidth,
            castBrightness = values.castBrightness,
            castReach = values.castReach,
            beamBrightness = values.beamBrightness,
            beamLength = values.beamLength
        };
    }

    private void ApplyValues(PoolroomLightingPreset values)
    {
        if (values == null)
            return;

        overallBloom = values.overallBloom;
        chromaticAberration = values.chromaticAberration;
        visualNoise = values.visualNoise;
        noiseResponse = values.noiseResponse;
        noiseScale = (FilmGrainLookup)values.noiseScale;
        fisheyeStrength = values.fisheyeStrength;
        cameraFieldOfView = values.cameraFieldOfView;
        cameraColorFilter = values.cameraColorFilter;
        cameraSaturation = values.cameraSaturation;
        cameraContrast = values.cameraContrast;
        cameraHueShift = values.cameraHueShift;
        cameraExposure = values.cameraExposure;
        cameraTemperature = values.cameraTemperature;
        cameraTint = values.cameraTint;
        cameraVignette = values.cameraVignette;
        CopyCrackValues(values.wallCracks, wallValues);
        CopyCrackValues(values.floorCracks, floorValues);

        bool sceneIsOpen = SceneManager.GetActiveScene().path == ScenePath;
        if (sceneIsOpen)
        {
            ApplyRoomBrightness(values.roomBrightness);
            ApplyPoolBrightness(values.poolBrightness);
            ApplyPlayerCameraFieldOfView();
        }

        ApplyBloom();
        ApplyPlayerCameraEffects();
        ApplyCrackValues(wallValues);
        ApplyCrackValues(floorValues);
        Repaint();
    }

    private static void CopyCrackValues(PoolroomCrackPresetValues source, CrackValues destination)
    {
        if (source == null || destination == null)
            return;

        destination.glow = source.glow;
        destination.halo = source.halo;
        destination.haloWidth = source.haloWidth;
        destination.castBrightness = source.castBrightness;
        destination.castReach = source.castReach;
        destination.beamBrightness = source.beamBrightness;
        destination.beamLength = source.beamLength;
    }

    private void ReloadSavedValues()
    {
        PoolroomLightingPresetState presetState = PoolroomLightingPresetState.instance;
        if (!presetState.hasLastSavedValues || presetState.lastSavedValues == null)
        {
            EnsureLastSavedValues();
            presetState = PoolroomLightingPresetState.instance;
        }

        ApplyValues(presetState.lastSavedValues);
        presetMessage = "Restored the values from the last time Save Scene and Assets was clicked.";
    }

    private void SaveCurrentNamedPreset()
    {
        string trimmedName = (newPresetName ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(trimmedName))
        {
            presetMessage = "Enter a name before saving the preset.";
            return;
        }

        PoolroomLightingPresetState presetState = PoolroomLightingPresetState.instance;
        if (presetState.namedPresets == null)
            presetState.namedPresets = new List<PoolroomLightingPreset>();

        int existingIndex = presetState.namedPresets.FindIndex(
            preset => string.Equals(preset.presetName, trimmedName, StringComparison.OrdinalIgnoreCase));
        PoolroomLightingPreset captured = CaptureCurrentValues(trimmedName);
        if (existingIndex >= 0)
        {
            presetState.namedPresets[existingIndex] = captured;
            selectedNamedPreset = existingIndex;
            presetMessage = $"Updated the named preset '{trimmedName}'.";
        }
        else
        {
            presetState.namedPresets.Add(captured);
            selectedNamedPreset = presetState.namedPresets.Count - 1;
            presetMessage = $"Saved a new preset named '{trimmedName}'.";
        }

        presetState.SaveState();
    }

    private void ApplySelectedNamedPreset()
    {
        PoolroomLightingPresetState presetState = PoolroomLightingPresetState.instance;
        if (!TryGetSelectedPreset(presetState, out PoolroomLightingPreset preset))
            return;

        ApplyValues(preset);
        presetMessage = $"Previewing '{preset.presetName}'. Click Save Scene and Assets to keep it as the reload point.";
    }

    private void OverwriteSelectedNamedPreset()
    {
        PoolroomLightingPresetState presetState = PoolroomLightingPresetState.instance;
        if (!TryGetSelectedPreset(presetState, out PoolroomLightingPreset existing))
            return;

        presetState.namedPresets[selectedNamedPreset] = CaptureCurrentValues(existing.presetName);
        presetState.SaveState();
        presetMessage = $"Overwrote '{existing.presetName}' with the current values.";
    }

    private void DeleteSelectedNamedPreset()
    {
        PoolroomLightingPresetState presetState = PoolroomLightingPresetState.instance;
        if (!TryGetSelectedPreset(presetState, out PoolroomLightingPreset selected))
            return;

        if (!EditorUtility.DisplayDialog(
                "Delete Lighting Preset",
                $"Delete the preset '{selected.presetName}'?",
                "Delete",
                "Cancel"))
            return;

        presetState.namedPresets.RemoveAt(selectedNamedPreset);
        selectedNamedPreset = Mathf.Clamp(selectedNamedPreset, 0, Mathf.Max(0, presetState.namedPresets.Count - 1));
        presetState.SaveState();
        presetMessage = $"Deleted the preset '{selected.presetName}'.";
    }

    private bool TryGetSelectedPreset(
        PoolroomLightingPresetState presetState,
        out PoolroomLightingPreset preset)
    {
        preset = null;
        if (presetState.namedPresets == null || presetState.namedPresets.Count == 0)
        {
            presetMessage = "There is no saved preset to use yet.";
            return false;
        }

        selectedNamedPreset = Mathf.Clamp(selectedNamedPreset, 0, presetState.namedPresets.Count - 1);
        preset = presetState.namedPresets[selectedNamedPreset];
        return preset != null;
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
        SceneView.RepaintAll();
    }

    private void LoadPlayerCameraEffects()
    {
        VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(BloomProfilePath);
        if (profile != null &&
            profile.TryGet(out ChromaticAberration chromatic) &&
            profile.TryGet(out FilmGrain grain) &&
            profile.TryGet(out LensDistortion lensDistortion) &&
            profile.TryGet(out ColorAdjustments colorAdjustments) &&
            profile.TryGet(out WhiteBalance whiteBalance) &&
            profile.TryGet(out Vignette vignette))
        {
            chromaticAberration = chromatic.intensity.value;
            visualNoise = grain.intensity.value;
            noiseScale = grain.type.value;
            noiseResponse = grain.response.value;
            fisheyeStrength = lensDistortion.intensity.value;
            cameraColorFilter = colorAdjustments.colorFilter.value;
            cameraSaturation = colorAdjustments.saturation.value;
            cameraContrast = colorAdjustments.contrast.value;
            cameraHueShift = colorAdjustments.hueShift.value;
            cameraExposure = colorAdjustments.postExposure.value;
            cameraTemperature = whiteBalance.temperature.value;
            cameraTint = whiteBalance.tint.value;
            cameraVignette = vignette.intensity.value;
            Camera playerCamera = FindPlayerCamera();
            if (playerCamera != null)
                cameraFieldOfView = playerCamera.fieldOfView;
            return;
        }

        chromaticAberration = 0f;
        visualNoise = 0f;
        noiseScale = FilmGrainLookup.Medium3;
        noiseResponse = 0.8f;
        fisheyeStrength = 0f;
        SetNeutralColorFilter();
        Camera fallbackCamera = FindPlayerCamera();
        cameraFieldOfView = fallbackCamera != null ? fallbackCamera.fieldOfView : 60f;
    }

    private void ApplyPlayerCameraEffects()
    {
        VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(BloomProfilePath);
        if (profile == null ||
            !profile.TryGet(out ChromaticAberration chromatic) ||
            !profile.TryGet(out FilmGrain grain) ||
            !profile.TryGet(out LensDistortion lensDistortion) ||
            !profile.TryGet(out ColorAdjustments colorAdjustments) ||
            !profile.TryGet(out WhiteBalance whiteBalance) ||
            !profile.TryGet(out Vignette vignette))
        {
            Debug.LogError("Poolroom Lighting Controls could not find the player camera effects in the global poolroom profile.");
            return;
        }

        Undo.RecordObjects(
            new UnityEngine.Object[] { chromatic, grain, lensDistortion, colorAdjustments, whiteBalance, vignette },
            "Adjust Player Camera Effects");
        chromatic.intensity.value = chromaticAberration;
        grain.intensity.value = visualNoise;
        grain.type.value = noiseScale;
        grain.response.value = noiseResponse;
        lensDistortion.intensity.value = fisheyeStrength;
        colorAdjustments.colorFilter.value = cameraColorFilter;
        colorAdjustments.saturation.value = cameraSaturation;
        colorAdjustments.contrast.value = cameraContrast;
        colorAdjustments.hueShift.value = cameraHueShift;
        colorAdjustments.postExposure.value = cameraExposure;
        whiteBalance.temperature.value = cameraTemperature;
        whiteBalance.tint.value = cameraTint;
        vignette.intensity.value = cameraVignette;
        EditorUtility.SetDirty(chromatic);
        EditorUtility.SetDirty(grain);
        EditorUtility.SetDirty(lensDistortion);
        EditorUtility.SetDirty(colorAdjustments);
        EditorUtility.SetDirty(whiteBalance);
        EditorUtility.SetDirty(vignette);
        EditorUtility.SetDirty(profile);
        SceneView.RepaintAll();
    }

    private void ApplyFilterPreset(int preset)
    {
        SetNeutralColorFilter();

        switch (preset)
        {
            case 1:
                cameraColorFilter = new Color(1f, 0.72f, 0.72f, 1f);
                cameraSaturation = -10f;
                cameraContrast = 18f;
                cameraExposure = -0.1f;
                cameraTemperature = 18f;
                cameraTint = 12f;
                cameraVignette = 0.25f;
                break;
            case 2:
                cameraColorFilter = new Color(0.72f, 0.86f, 1f, 1f);
                cameraSaturation = -12f;
                cameraContrast = 8f;
                cameraExposure = -0.05f;
                cameraTemperature = -25f;
                cameraTint = -5f;
                cameraVignette = 0.12f;
                break;
            case 3:
                cameraColorFilter = new Color(0.8f, 1f, 0.72f, 1f);
                cameraSaturation = -18f;
                cameraContrast = 12f;
                cameraExposure = -0.1f;
                cameraTemperature = -8f;
                cameraTint = -18f;
                cameraVignette = 0.18f;
                break;
            case 4:
                cameraColorFilter = new Color(0.95f, 0.88f, 1f, 1f);
                cameraSaturation = -35f;
                cameraContrast = -10f;
                cameraHueShift = 6f;
                cameraExposure = 0.05f;
                cameraTemperature = 8f;
                cameraTint = 12f;
                cameraVignette = 0.2f;
                break;
        }

        ApplyPlayerCameraEffects();
        Repaint();
    }

    private void SetNeutralColorFilter()
    {
        cameraColorFilter = Color.white;
        cameraSaturation = 0f;
        cameraContrast = 0f;
        cameraHueShift = 0f;
        cameraExposure = 0f;
        cameraTemperature = 0f;
        cameraTint = 0f;
        cameraVignette = 0f;
    }

    private void ApplyPlayerCameraFieldOfView()
    {
        Camera playerCamera = FindPlayerCamera();
        if (playerCamera == null)
        {
            Debug.LogError("Poolroom Lighting Controls could not find the player's Main Camera.");
            return;
        }

        Undo.RecordObject(playerCamera, "Adjust Player Camera Field of View");
        playerCamera.fieldOfView = cameraFieldOfView;
        EditorUtility.SetDirty(playerCamera);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        SceneView.RepaintAll();
    }

    private static Camera FindPlayerCamera()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != ScenePath)
            return null;

        return scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Camera>(true))
            .FirstOrDefault(camera => camera.gameObject.name == "Main Camera");
    }

    private static List<Light> FindRoomLights()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != ScenePath)
            return new List<Light>();

        return scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Light>(true))
            .Where(light => !IsCrackLight(light.transform) && !IsPoolLight(light))
            .ToList();
    }

    private static List<Light> FindPoolLights()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != ScenePath)
            return new List<Light>();

        return scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Light>(true))
            .Where(light => !IsCrackLight(light.transform) && IsPoolLight(light))
            .ToList();
    }

    private static List<Renderer> FindPoolInteriorLightSheets()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != ScenePath)
            return new List<Renderer>();

        return scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Renderer>(true))
            .Where(renderer => HasNamedAncestor(renderer.transform, PoolInteriorLightSheetRootName))
            .ToList();
    }

    private static bool HasNamedAncestor(Transform transform, string objectName)
    {
        for (Transform current = transform; current != null; current = current.parent)
        {
            if (current.name == objectName)
                return true;
        }
        return false;
    }

    private static bool IsPoolLight(Light light)
    {
        return light != null &&
               light.gameObject.name.EndsWith("Bright Pool Light", StringComparison.Ordinal);
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

    private static void EnsurePoolLightBaselines()
    {
        List<Light> lights = FindPoolLights();
        if (lights.Count == 0)
            return;

        PoolroomLightingState state = PoolroomLightingState.instance;
        state.poolLightBaselines ??= new List<PoolroomRoomLightBaseline>();
        float safeScale = Mathf.Max(state.poolBrightness, 0.0001f);
        bool changed = false;
        foreach (Light light in lights)
        {
            string id = GlobalObjectId.GetGlobalObjectIdSlow(light).ToString();
            if (state.poolLightBaselines.Any(record => record.globalObjectId == id))
                continue;

            state.poolLightBaselines.Add(new PoolroomRoomLightBaseline
            {
                globalObjectId = id,
                intensity = light.intensity / safeScale
            });
            changed = true;
        }

        if (changed)
            state.SaveState();
    }

    private static float ReadCurrentPoolBrightness(PoolroomLightingState state)
    {
        List<float> brightnessValues = new List<float>();
        foreach (Light light in FindPoolLights())
        {
            string id = GlobalObjectId.GetGlobalObjectIdSlow(light).ToString();
            PoolroomRoomLightBaseline baseline = state.poolLightBaselines.FirstOrDefault(record => record.globalObjectId == id);
            if (baseline != null && baseline.intensity > 0.0001f)
                brightnessValues.Add(light.intensity / baseline.intensity);
        }

        return brightnessValues.Count > 0
            ? Mathf.Clamp(brightnessValues.Average(), 0f, 2.5f)
            : Mathf.Clamp(state.poolBrightness, 0f, 2.5f);
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
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        SceneView.RepaintAll();
    }

    private static void ApplyPoolBrightness(float brightness)
    {
        PoolroomLightingState state = PoolroomLightingState.instance;
        state.poolBrightness = Mathf.Clamp(brightness, 0f, 2.5f);
        ApplyPoolSurfaceGlow(state.poolBrightness, true);
        SceneView.RepaintAll();
    }

    private static void ApplyPoolSurfaceGlow(float brightness, bool recordUndo)
    {
        Material[] materials =
        {
            AssetDatabase.LoadAssetAtPath<Material>(PoolSurfaceMaterialPath),
            AssetDatabase.LoadAssetAtPath<Material>(PoolCornerMaterialPath)
        };
        Material[] changedMaterials = materials
            .Where(material => material != null &&
                               !Mathf.Approximately(material.GetFloat("_Brightness"), Mathf.Clamp(brightness, 0f, 2.5f)))
            .ToArray();
        if (changedMaterials.Length == 0)
            return;

        float clampedBrightness = Mathf.Clamp(brightness, 0f, 2.5f);
        if (recordUndo)
            Undo.RecordObjects(changedMaterials, "Adjust Underwater Pool Light Sheets");
        foreach (Material material in changedMaterials)
        {
            material.SetFloat("_Brightness", clampedBrightness);
            EditorUtility.SetDirty(material);
        }
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

    private void SaveEverything()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.IsValid() && scene.path == ScenePath && scene.isDirty)
            EditorSceneManager.SaveScene(scene);

        PoolroomLightingState.instance.SaveState();
        PoolroomLightingPresetState presetState = PoolroomLightingPresetState.instance;
        presetState.lastSavedValues = CaptureCurrentValues("Last Saved Values");
        presetState.hasLastSavedValues = true;
        presetState.SaveState();
        AssetDatabase.SaveAssets();
        presetMessage = "Saved the current values. Reload Saved Values will now return to this setup.";
        Debug.Log("POOLROOM_LIGHTING_SAVED: Saved the separate room and pool lights, camera effects, wall-crack, floor-crack, and reload snapshot settings.");
    }
}
