using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;

internal sealed class PoolroomGameplayControls : EditorWindow
{
    private const string ScenePath = "Assets/OutdoorsScene.unity";
    private const string BlurProfilePath = "Assets/Poolroom/Underwater Effects/Underwater Soft Focus.asset";
    private const string DustMaterialPath = "Assets/Poolroom/Underwater Effects/Underwater Dust.mat";
    private const string SelectedPageKey = "LiminalPoolroom.GameplayControls.SelectedPage";

    private static readonly string[] PageNames =
    {
        "Player",
        "Swimming",
        "Splash",
        "Underwater",
        "Ladder"
    };

    private int selectedPage;
    private Vector2 scroll;

    [MenuItem("Liminal Poolroom/Open All Control Windows", false, 0)]
    private static void OpenAllControlWindows()
    {
        PoolroomLightingControls.OpenWindow();
        OpenWindow();
    }

    [MenuItem("Liminal Poolroom/Gameplay & Effects Controls", false, 2)]
    internal static void OpenWindow()
    {
        PoolroomGameplayControls window = GetWindow<PoolroomGameplayControls>();
        window.titleContent = new GUIContent("Poolroom Gameplay");
        window.minSize = new Vector2(540f, 650f);
        window.Show();
    }

    private void OnEnable()
    {
        selectedPage = Mathf.Clamp(EditorPrefs.GetInt(SelectedPageKey, 0), 0, PageNames.Length - 1);
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Liminal Poolroom Gameplay & Effects", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "These controls change the real objects in the poolroom scene. Changes preview immediately; use the save button at the bottom when you are happy with them.",
            MessageType.Info);

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorGUILayout.HelpBox("Stop Play Mode before changing these settings so Unity can save them permanently.", MessageType.Warning);
            return;
        }

        bool sceneIsOpen = SceneManager.GetActiveScene().path == ScenePath;
        if (!sceneIsOpen)
        {
            EditorGUILayout.HelpBox("Open the poolroom scene before changing gameplay or effects.", MessageType.Warning);
            if (GUILayout.Button("Open Poolroom Scene", GUILayout.Height(28f)))
                OpenPoolroomScene();
            return;
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
                DrawPlayerPage();
                break;
            case 1:
                DrawSwimmingPage();
                break;
            case 2:
                DrawSplashPage();
                break;
            case 3:
                DrawUnderwaterPage();
                break;
            case 4:
                DrawLadderPage();
                break;
        }

        EditorGUILayout.Space(12f);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Refresh Current Values", GUILayout.Height(28f)))
                Repaint();
            if (GUILayout.Button("Save Scene and Assets", GUILayout.Height(28f)))
                SaveEverything();
        }

        EditorGUILayout.Space(8f);
        EditorGUILayout.EndScrollView();
    }

    private void DrawPlayerPage()
    {
        Player player = FindSceneComponent<Player>();
        if (!DrawObjectHeader(
                "Player Movement and View",
                "Walking, jumping, crouching, and mouse-look settings for PLAYAH.",
                player))
            return;

        SerializedObject serializedPlayer = new SerializedObject(player);
        serializedPlayer.Update();

        BeginSection("Walking", "How quickly the player moves and responds to the movement keys.");
        DrawFloatSlider(serializedPlayer, "moveSpeed", "Walking Speed", "Top walking speed.", 0f, 15f);
        DrawFloatSlider(serializedPlayer, "groundAcceleration", "Ground Responsiveness", "How quickly walking reaches full speed or stops.", 1f, 100f);
        DrawFloatSlider(serializedPlayer, "airAcceleration", "Air Control", "How much the movement keys steer the player while airborne.", 0f, 50f);
        EndSection();

        BeginSection("Jumping", "Jump strength and how steep a surface may be while still counting as ground.");
        DrawFloatSlider(serializedPlayer, "jumpForce", "Jump Strength", "Upward speed added when jumping.", 0f, 20f);
        DrawFloatSlider(serializedPlayer, "minimumGroundNormal", "Steep-Surface Limit", "Higher values require a flatter surface before jumping is allowed.", 0.1f, 1f);
        EndSection();

        BeginSection("Crouching", "Shift crouches on land. These settings control its size, speed, and camera movement.");
        DrawFloatSlider(serializedPlayer, "crouchHeight", "Crouched Height", "How short the player's collision shape becomes.", 0.8f, 2f);
        DrawFloatSlider(serializedPlayer, "crouchTransitionSpeed", "Crouch Smoothness", "How quickly the player lowers and stands back up.", 0.5f, 20f);
        DrawFloatSlider(serializedPlayer, "crouchViewDrop", "Camera Drop", "How far the view lowers while crouched.", 0f, 1.25f);
        DrawFloatSlider(serializedPlayer, "crouchMoveSpeedMultiplier", "Crouched Speed", "Walking speed while crouched, shown as a fraction of normal speed.", 0.1f, 1f);
        EndSection();

        BeginSection("Mouse Look", "How quickly and how far the player can look around.");
        DrawFloatSlider(serializedPlayer, "mouseSensitivity", "Mouse Sensitivity", "How strongly mouse movement turns the camera.", 0.01f, 2f);
        DrawFloatSlider(serializedPlayer, "minimumLookAngle", "Look Down Limit", "The lowest vertical viewing angle.", -89f, 0f);
        DrawFloatSlider(serializedPlayer, "maximumLookAngle", "Look Up Limit", "The highest vertical viewing angle.", 0f, 89f);
        EndSection();

        ApplySceneProperties(serializedPlayer, player);

        if (GUILayout.Button("Restore Recommended Player Settings"))
        {
            Undo.RecordObject(player, "Restore Recommended Player Settings");
            SetFloat(serializedPlayer, "moveSpeed", 6f);
            SetFloat(serializedPlayer, "groundAcceleration", 40f);
            SetFloat(serializedPlayer, "airAcceleration", 15f);
            SetFloat(serializedPlayer, "jumpForce", 8f);
            SetFloat(serializedPlayer, "minimumGroundNormal", 0.6f);
            SetFloat(serializedPlayer, "crouchHeight", 1.25f);
            SetFloat(serializedPlayer, "crouchTransitionSpeed", 6f);
            SetFloat(serializedPlayer, "crouchViewDrop", 0.6f);
            SetFloat(serializedPlayer, "crouchMoveSpeedMultiplier", 0.55f);
            SetFloat(serializedPlayer, "mouseSensitivity", 0.8f);
            SetFloat(serializedPlayer, "minimumLookAngle", -80f);
            SetFloat(serializedPlayer, "maximumLookAngle", 80f);
            ApplySceneProperties(serializedPlayer, player);
        }
    }

    private void DrawSwimmingPage()
    {
        Player player = FindSceneComponent<Player>();
        if (!DrawObjectHeader(
                "Swimming and Floating",
                "Horizontal swimming, manual rising and sinking, and the slow automatic drift toward the surface.",
                player))
            return;

        EditorGUILayout.HelpBox(
            "In water: Space rises, Shift or Ctrl sinks, and releasing both lets the player slowly drift upward.",
            MessageType.None);

        SerializedObject serializedPlayer = new SerializedObject(player);
        serializedPlayer.Update();

        BeginSection("Swimming", "Movement speed and how quickly water movement responds.");
        DrawFloatSlider(serializedPlayer, "swimSpeed", "Swimming Speed", "Top movement speed in water, including manual rising and sinking.", 0f, 12f);
        DrawFloatSlider(serializedPlayer, "swimAcceleration", "Water Responsiveness", "How quickly swimming changes direction or speed.", 0.5f, 30f);
        EndSection();

        BeginSection("Automatic Floating", "Controls the gentle upward drift when no rise or sink key is held.");
        DrawFloatSlider(serializedPlayer, "floatDepth", "Resting Depth", "How far below the surface the player's center tries to settle.", 0f, 2f);
        DrawFloatSlider(serializedPlayer, "floatStrength", "Surface Pull", "How strongly the player aims for the resting depth.", 0f, 12f);
        DrawFloatSlider(serializedPlayer, "passiveRiseSpeed", "Upward Drift Speed", "The maximum speed of the automatic upward drift.", 0f, 3f);
        DrawFloatSlider(serializedPlayer, "passiveRiseAcceleration", "Drift Smoothness", "How gradually the upward drift builds.", 0.05f, 8f);
        EndSection();

        ApplySceneProperties(serializedPlayer, player);

        if (GUILayout.Button("Restore Recommended Swimming Settings"))
        {
            Undo.RecordObject(player, "Restore Recommended Swimming Settings");
            SetFloat(serializedPlayer, "swimSpeed", 4.5f);
            SetFloat(serializedPlayer, "swimAcceleration", 11f);
            SetFloat(serializedPlayer, "floatDepth", 0.45f);
            SetFloat(serializedPlayer, "floatStrength", 4f);
            SetFloat(serializedPlayer, "passiveRiseSpeed", 0.65f);
            SetFloat(serializedPlayer, "passiveRiseAcceleration", 1.25f);
            ApplySceneProperties(serializedPlayer, player);
        }
    }

    private void DrawSplashPage()
    {
        SwimmableWater water = FindSceneComponent<SwimmableWater>();
        if (!DrawObjectHeader(
                "Water Entry Splash",
                "Droplets and expanding rings created when PLAYAH falls into the pool.",
                water))
            return;

        SerializedObject serializedWater = new SerializedObject(water);
        serializedWater.Update();

        BeginSection("Water Surface", "Use this only if the visible top of the water and the splash height no longer line up.");
        DrawFloatSlider(serializedWater, "surfaceOffset", "Splash Surface Offset", "Moves the swimming and splash surface slightly up or down.", -1f, 1f);
        EndSection();

        BeginSection("Splash Droplets", "The small pieces of water thrown upward on entry.");
        DrawIntSlider(serializedWater, "splashDropletCount", "Droplet Amount", "Maximum number of droplets in a strong splash.", 1, 50);
        DrawFloatSlider(serializedWater, "splashHeight", "Splash Height", "How high the strongest droplets can fly.", 0.1f, 10f);
        EndSection();

        BeginSection("Surface Ripples", "The three rings that spread across the surface after entry.");
        DrawFloatSlider(serializedWater, "rippleLifetime", "Ripple Lifetime", "How long each ring remains visible.", 0.1f, 5f);
        DrawFloatSlider(serializedWater, "rippleSpeed", "Ripple Spread Speed", "How quickly the rings expand.", 0.1f, 8f);
        EndSection();

        BeginSection("Appearance", "Materials determine the color and transparency of the droplets and rings.");
        EditorGUILayout.PropertyField(serializedWater.FindProperty("splashMaterial"), new GUIContent("Droplet Material"));
        EditorGUILayout.PropertyField(serializedWater.FindProperty("rippleMaterial"), new GUIContent("Ripple Material"));
        EndSection();

        ApplySceneProperties(serializedWater, water);

        if (GUILayout.Button("Restore Recommended Splash Settings"))
        {
            Undo.RecordObject(water, "Restore Recommended Splash Settings");
            SetFloat(serializedWater, "surfaceOffset", 0f);
            serializedWater.FindProperty("splashDropletCount").intValue = 16;
            SetFloat(serializedWater, "splashHeight", 4.5f);
            SetFloat(serializedWater, "rippleLifetime", 1.6f);
            SetFloat(serializedWater, "rippleSpeed", 2.5f);
            ApplySceneProperties(serializedWater, water);
        }
    }

    private void DrawUnderwaterPage()
    {
        Volume blurVolume = FindNamedSceneComponent<Volume>("Underwater Slight Blur Volume");
        ParticleSystem dust = FindNamedSceneComponent<ParticleSystem>("Slow Drifting Underwater Dust");
        VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(BlurProfilePath);
        Material dustMaterial = AssetDatabase.LoadAssetAtPath<Material>(DustMaterialPath);

        EditorGUILayout.LabelField("Underwater Blur and Dust", EditorStyles.boldLabel);
        EditorGUILayout.LabelField(
            "Controls the soft-focus effect and the tiny drifting particles visible below the water surface.",
            EditorStyles.wordWrappedMiniLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (blurVolume != null && GUILayout.Button("Show Blur Area in Scene"))
                SelectAndPing(blurVolume.gameObject);
            if (dust != null && GUILayout.Button("Show Dust in Scene"))
                SelectAndPing(dust.gameObject);
        }

        if (blurVolume == null || dust == null || profile == null || dustMaterial == null ||
            !profile.TryGet(out DepthOfField depthOfField))
        {
            EditorGUILayout.HelpBox("One or more underwater effect objects or assets could not be found.", MessageType.Error);
            return;
        }

        BeginSection("Slight Blur", "This affects only the camera while it is inside the pool's underwater area.");
        float blurStrength = depthOfField.farMaxBlur;
        float blurStart = depthOfField.farFocusStart.value;
        float blurEnd = depthOfField.farFocusEnd.value;
        float blendDistance = blurVolume.blendDistance;

        EditorGUI.BeginChangeCheck();
        blurStrength = EditorGUILayout.Slider(new GUIContent("Blur Strength", "Maximum underwater softness in pixels."), blurStrength, 0f, 3f);
        blurStart = EditorGUILayout.Slider(new GUIContent("Clear Distance", "Distance from the camera that stays mostly sharp."), blurStart, 0.1f, 5f);
        blurEnd = EditorGUILayout.Slider(new GUIContent("Full Blur Distance", "Distance where the selected blur reaches full strength."), blurEnd, Mathf.Max(blurStart + 0.1f, 1f), 30f);
        blendDistance = EditorGUILayout.Slider(new GUIContent("Surface Transition", "How smoothly the blur fades in as the camera enters the water."), blendDistance, 0f, 2f);
        if (EditorGUI.EndChangeCheck())
            ApplyBlurSettings(depthOfField, profile, blurVolume, blurStrength, blurStart, blurEnd, blendDistance);
        EndSection();

        DrawDustControls(dust, dustMaterial);

        if (GUILayout.Button("Restore Recommended Underwater Settings"))
        {
            ApplyBlurSettings(depthOfField, profile, blurVolume, 1.15f, 0.8f, 12f, 0.35f);
            ApplyDustSettings(dust, dustMaterial, 420, 14f, 0.012f, 0.052f, 18f, 27f, 0.008f, 0.028f, 0.009f, 0.035f, 0.22f);
        }
    }

    private void DrawDustControls(ParticleSystem dust, Material dustMaterial)
    {
        ParticleSystem.MainModule main = dust.main;
        ParticleSystem.EmissionModule emission = dust.emission;
        ParticleSystem.VelocityOverLifetimeModule velocity = dust.velocityOverLifetime;
        ParticleSystem.NoiseModule noise = dust.noise;

        ParticleSystem.MinMaxCurve lifetime = main.startLifetime;
        ParticleSystem.MinMaxCurve size = main.startSize;
        ParticleSystem.MinMaxCurve rate = emission.rateOverTime;
        ParticleSystem.MinMaxCurve rise = velocity.y;
        ParticleSystem.MinMaxCurve side = velocity.x;

        int maximumParticles = main.maxParticles;
        float dustPerSecond = CurveMaximum(rate);
        float minimumSize = CurveMinimum(size);
        float maximumSize = CurveMaximum(size);
        float minimumLifetime = CurveMinimum(lifetime);
        float maximumLifetime = CurveMaximum(lifetime);
        float minimumRise = CurveMinimum(rise);
        float maximumRise = CurveMaximum(rise);
        float sidewaysDrift = Mathf.Max(Mathf.Abs(CurveMinimum(side)), Mathf.Abs(CurveMaximum(side)));
        float turbulence = CurveMaximum(noise.strength);
        Color tint = dustMaterial.HasProperty("_TintColor") ? dustMaterial.GetColor("_TintColor") : Color.white;
        float visibility = tint.a;

        BeginSection("Drifting Dust", "Small red-tinted specks that move slowly through the water.");
        EditorGUI.BeginChangeCheck();
        maximumParticles = EditorGUILayout.IntSlider(new GUIContent("Maximum Dust Pieces", "Safety cap for how many dust specks may exist at once."), maximumParticles, 1, 1000);
        dustPerSecond = EditorGUILayout.Slider(new GUIContent("New Dust Per Second", "How many new specks appear each second."), dustPerSecond, 0f, 50f);
        EditorGUILayout.MinMaxSlider(new GUIContent("Dust Size Range", "Random size range for each dust speck."), ref minimumSize, ref maximumSize, 0.002f, 0.12f);
        EditorGUILayout.LabelField("Smallest / Largest", $"{minimumSize:0.000} m  /  {maximumSize:0.000} m", EditorStyles.miniLabel);
        EditorGUILayout.MinMaxSlider(new GUIContent("Lifetime Range", "How long each dust speck remains before being replaced."), ref minimumLifetime, ref maximumLifetime, 1f, 60f);
        EditorGUILayout.LabelField("Shortest / Longest", $"{minimumLifetime:0.0} sec  /  {maximumLifetime:0.0} sec", EditorStyles.miniLabel);
        EditorGUILayout.MinMaxSlider(new GUIContent("Upward Drift Range", "Random slow upward speed for the dust."), ref minimumRise, ref maximumRise, 0f, 0.2f);
        EditorGUILayout.LabelField("Slowest / Fastest", $"{minimumRise:0.000} m/s  /  {maximumRise:0.000} m/s", EditorStyles.miniLabel);
        sidewaysDrift = EditorGUILayout.Slider(new GUIContent("Sideways Drift", "Maximum gentle motion to either side."), sidewaysDrift, 0f, 0.1f);
        turbulence = EditorGUILayout.Slider(new GUIContent("Wandering", "How much the dust meanders instead of moving in a straight line."), turbulence, 0f, 0.2f);
        visibility = EditorGUILayout.Slider(new GUIContent("Dust Visibility", "Transparency of every dust speck."), visibility, 0f, 1f);
        tint = EditorGUILayout.ColorField(new GUIContent("Dust Color", "Color tint used for the underwater dust."), new Color(tint.r, tint.g, tint.b, 1f));
        if (EditorGUI.EndChangeCheck())
        {
            ApplyDustSettings(
                dust,
                dustMaterial,
                maximumParticles,
                dustPerSecond,
                minimumSize,
                maximumSize,
                minimumLifetime,
                maximumLifetime,
                minimumRise,
                maximumRise,
                sidewaysDrift,
                turbulence,
                visibility,
                tint);
        }
        EndSection();
    }

    private void DrawLadderPage()
    {
        Player player = FindSceneComponent<Player>();
        ClimbableLadder ladder = FindSceneComponent<ClimbableLadder>();
        if (!DrawObjectHeader(
                "Pool Ladder",
                "Climbing speed and the direction the player is pushed when jumping off the ladder.",
                ladder))
            return;

        if (player == null)
        {
            EditorGUILayout.HelpBox("PLAYAH could not be found in the scene.", MessageType.Error);
            return;
        }

        SerializedObject serializedPlayer = new SerializedObject(player);
        SerializedObject serializedLadder = new SerializedObject(ladder);
        serializedPlayer.Update();
        serializedLadder.Update();

        BeginSection("Climbing", "W and S climb vertically. A and D move sideways while attached.");
        DrawFloatSlider(serializedPlayer, "ladderClimbSpeed", "Climbing Speed", "Vertical speed while holding W or S on the ladder.", 0.5f, 10f);
        EndSection();

        BeginSection("Jumping Off", "The direction is measured from the ladder itself: X is sideways, Y is upward, and Z points away or toward the wall.");
        SerializedProperty direction = serializedLadder.FindProperty("localDismountDirection");
        direction.vector3Value = EditorGUILayout.Vector3Field(new GUIContent("Jump-Off Direction", "Usually (0, 0, 1), which pushes the player away from the wall."), direction.vector3Value);
        EndSection();

        ApplySceneProperties(serializedPlayer, player);
        ApplySceneProperties(serializedLadder, ladder);

        if (GUILayout.Button("Restore Recommended Ladder Settings"))
        {
            Undo.RecordObjects(new UnityEngine.Object[] { player, ladder }, "Restore Recommended Ladder Settings");
            SetFloat(serializedPlayer, "ladderClimbSpeed", 3.5f);
            serializedLadder.FindProperty("localDismountDirection").vector3Value = Vector3.forward;
            ApplySceneProperties(serializedPlayer, player);
            ApplySceneProperties(serializedLadder, ladder);
        }
    }

    private static bool DrawObjectHeader(string title, string description, Component target)
    {
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        EditorGUILayout.LabelField(description, EditorStyles.wordWrappedMiniLabel);

        if (target == null)
        {
            EditorGUILayout.HelpBox($"The {title.ToLowerInvariant()} object could not be found in the poolroom scene.", MessageType.Error);
            return false;
        }

        if (GUILayout.Button($"Show {target.gameObject.name} in Scene"))
            SelectAndPing(target.gameObject);
        EditorGUILayout.Space(5f);
        return true;
    }

    private static void BeginSection(string title, string description)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        EditorGUILayout.LabelField(description, EditorStyles.wordWrappedMiniLabel);
    }

    private static void EndSection()
    {
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(5f);
    }

    private static void DrawFloatSlider(
        SerializedObject serializedObject,
        string propertyName,
        string label,
        string tooltip,
        float minimum,
        float maximum)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
            return;
        property.floatValue = EditorGUILayout.Slider(new GUIContent(label, tooltip), property.floatValue, minimum, maximum);
    }

    private static void DrawIntSlider(
        SerializedObject serializedObject,
        string propertyName,
        string label,
        string tooltip,
        int minimum,
        int maximum)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
            return;
        property.intValue = EditorGUILayout.IntSlider(new GUIContent(label, tooltip), property.intValue, minimum, maximum);
    }

    private static void SetFloat(SerializedObject serializedObject, string propertyName, float value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
            property.floatValue = value;
    }

    private static void ApplySceneProperties(SerializedObject serializedObject, UnityEngine.Object target)
    {
        if (!serializedObject.ApplyModifiedProperties())
            return;

        EditorUtility.SetDirty(target);
        MarkPoolroomSceneDirty();
        SceneView.RepaintAll();
    }

    private static void ApplyBlurSettings(
        DepthOfField depthOfField,
        VolumeProfile profile,
        Volume blurVolume,
        float strength,
        float clearDistance,
        float fullBlurDistance,
        float surfaceTransition)
    {
        Undo.RecordObjects(new UnityEngine.Object[] { depthOfField, profile, blurVolume }, "Adjust Underwater Blur");
        depthOfField.farMaxBlur = strength;
        depthOfField.farFocusStart.value = clearDistance;
        depthOfField.farFocusEnd.value = Mathf.Max(clearDistance + 0.1f, fullBlurDistance);
        blurVolume.blendDistance = surfaceTransition;
        EditorUtility.SetDirty(depthOfField);
        EditorUtility.SetDirty(profile);
        EditorUtility.SetDirty(blurVolume);
        AssetDatabase.SaveAssets();
        MarkPoolroomSceneDirty();
        SceneView.RepaintAll();
    }

    private static void ApplyDustSettings(
        ParticleSystem dust,
        Material dustMaterial,
        int maximumParticles,
        float dustPerSecond,
        float minimumSize,
        float maximumSize,
        float minimumLifetime,
        float maximumLifetime,
        float minimumRise,
        float maximumRise,
        float sidewaysDrift,
        float turbulence,
        float visibility,
        Color? colorOverride = null)
    {
        Undo.RecordObjects(new UnityEngine.Object[] { dust, dustMaterial }, "Adjust Underwater Dust");

        ParticleSystem.MainModule main = dust.main;
        main.maxParticles = Mathf.Max(1, maximumParticles);
        main.startSize = new ParticleSystem.MinMaxCurve(Mathf.Max(0.002f, minimumSize), Mathf.Max(minimumSize, maximumSize));
        main.startLifetime = new ParticleSystem.MinMaxCurve(Mathf.Max(1f, minimumLifetime), Mathf.Max(minimumLifetime, maximumLifetime));

        ParticleSystem.EmissionModule emission = dust.emission;
        emission.rateOverTime = new ParticleSystem.MinMaxCurve(Mathf.Max(0f, dustPerSecond * 0.7f), Mathf.Max(0f, dustPerSecond));

        ParticleSystem.VelocityOverLifetimeModule velocity = dust.velocityOverLifetime;
        velocity.x = new ParticleSystem.MinMaxCurve(-Mathf.Abs(sidewaysDrift), Mathf.Abs(sidewaysDrift));
        velocity.y = new ParticleSystem.MinMaxCurve(Mathf.Max(0f, minimumRise), Mathf.Max(minimumRise, maximumRise));
        velocity.z = new ParticleSystem.MinMaxCurve(-Mathf.Abs(sidewaysDrift), Mathf.Abs(sidewaysDrift));

        ParticleSystem.NoiseModule noise = dust.noise;
        noise.strength = Mathf.Max(0f, turbulence);

        Color tint = colorOverride ?? (dustMaterial.HasProperty("_TintColor")
            ? dustMaterial.GetColor("_TintColor")
            : Color.white);
        tint.a = Mathf.Clamp01(visibility);
        if (dustMaterial.HasProperty("_TintColor"))
            dustMaterial.SetColor("_TintColor", tint);

        EditorUtility.SetDirty(dust);
        EditorUtility.SetDirty(dustMaterial);
        AssetDatabase.SaveAssets();
        MarkPoolroomSceneDirty();
        SceneView.RepaintAll();
    }

    private static float CurveMinimum(ParticleSystem.MinMaxCurve curve)
    {
        return curve.mode == ParticleSystemCurveMode.TwoConstants ? curve.constantMin : curve.constant;
    }

    private static float CurveMaximum(ParticleSystem.MinMaxCurve curve)
    {
        return curve.mode == ParticleSystemCurveMode.TwoConstants ? curve.constantMax : curve.constant;
    }

    private static T FindSceneComponent<T>() where T : Component
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != ScenePath)
            return null;

        return scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true))
            .FirstOrDefault();
    }

    private static T FindNamedSceneComponent<T>(string objectName) where T : Component
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != ScenePath)
            return null;

        return scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true))
            .FirstOrDefault(component => component.gameObject.name == objectName);
    }

    private static void SelectAndPing(UnityEngine.Object target)
    {
        Selection.activeObject = target;
        EditorGUIUtility.PingObject(target);
    }

    private static void MarkPoolroomSceneDirty()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.IsValid() && scene.path == ScenePath)
            EditorSceneManager.MarkSceneDirty(scene);
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
        Debug.Log("POOLROOM_GAMEPLAY_SAVED: Saved player, swimming, splash, underwater, and ladder settings.");
    }
}
