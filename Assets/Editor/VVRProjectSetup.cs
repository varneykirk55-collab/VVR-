using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.Management;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;
using UnityEngine.XR.OpenXR.Features.Interactions;
using UnityEngine.XR.OpenXR.Features.MetaQuestSupport;
using Unity.XR.CoreUtils;
using Unity.AI.Navigation;
using TMPro;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.OpenXR.Features;

public static class VVRProjectSetup
{
    [MenuItem("VVR/Build Complete Vertical Slice")]
    public static void BuildCompleteVerticalSlice()
    {
        Debug.Log("[VVRProjectSetup] Starting VVR-Arena Complete Vertical Slice Build...");

        EnsureDirectories();
        ConfigureLayersAndTags();
        ConfigureXRLoaderAndOpenXRFeatures();

        // 1. Create Materials
        Dictionary<string, Material> mats = CreateMaterials();

        // 2. Create Animator Controllers
        AnimatorController enemyAnimController = CreateEnemyAnimatorController();
        AnimatorController handAnimController = CreateHandAnimatorController();

        // 3. Create Input Action Asset
        InputActionAsset inputAsset = CreateVRInputActions();

        // 4. Create Prefabs (Sword, Axe, Shield, VikingEnemy)
        GameObject swordPrefab = CreateSwordPrefab(mats);
        GameObject axePrefab = CreateAxePrefab(mats);
        GameObject shieldPrefab = CreateShieldPrefab(mats);
        GameObject enemyPrefab = CreateVikingEnemyPrefab(mats, enemyAnimController);

        // 5. Build Playable Scene (Assets/Scenes/VikingArena.unity)
        BuildVikingArenaScene(
            mats,
            handAnimController,
            inputAsset,
            swordPrefab,
            axePrefab,
            shieldPrefab,
            enemyPrefab);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[VVRProjectSetup] VVR-Arena Complete Vertical Slice Build Succeeded!");
    }

    [MenuItem("VVR/Build Quest 2 APK")]
    public static void BuildQuest2Apk()
    {
        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
        {
            Debug.LogError("[VVRProjectSetup] Cannot build Quest 2 APK: Unity Android Build Support (AndroidPlayer, SDK/NDK, OpenJDK) is not installed in Unity 6000.6.6f1.");
            return;
        }

        string buildsDir = Path.Combine(Directory.GetCurrentDirectory(), "Builds");
        Directory.CreateDirectory(buildsDir);
        string apkPath = Path.Combine(buildsDir, "VVR-Arena-Quest2.apk");

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/VikingArena.unity" },
            locationPathName = apkPath,
            targetGroup = BuildTargetGroup.Android,
            target = BuildTarget.Android,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result == BuildResult.Succeeded)
        {
            Debug.Log($"[VVRProjectSetup] Quest 2 APK Build Succeeded: {apkPath} ({report.summary.totalSize} bytes)");
        }
        else
        {
            Debug.LogError($"[VVRProjectSetup] Quest 2 APK Build Failed: {report.summary.result} ({report.summary.totalErrors} errors)");
        }
    }

    private static void EnsureDirectories()
    {
        string[] dirs = new[]
        {
            "Assets/Materials",
            "Assets/Animations",
            "Assets/Input",
            "Assets/Prefabs",
            "Assets/Scenes",
            "Assets/XR",
            "Assets/XR/Loaders",
            "Assets/XR/Settings"
        };

        foreach (string dir in dirs)
        {
            if (!AssetDatabase.IsValidFolder(dir))
            {
                string parent = Path.GetDirectoryName(dir)?.Replace("\\", "/") ?? "Assets";
                string folderName = Path.GetFileName(dir);
                AssetDatabase.CreateFolder(parent, folderName);
            }
        }
    }

    private static void ConfigureLayersAndTags()
    {
        SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty layersProp = tagManager.FindProperty("layers");
        if (layersProp != null && layersProp.isArray)
        {
            SerializedProperty enemyLayer = layersProp.GetArrayElementAtIndex(8);
            if (string.IsNullOrEmpty(enemyLayer.stringValue))
                enemyLayer.stringValue = "Enemy";

            SerializedProperty weaponLayer = layersProp.GetArrayElementAtIndex(9);
            if (string.IsNullOrEmpty(weaponLayer.stringValue))
                weaponLayer.stringValue = "Weapon";

            SerializedProperty playerLayer = layersProp.GetArrayElementAtIndex(10);
            if (string.IsNullOrEmpty(playerLayer.stringValue))
                playerLayer.stringValue = "Player";
        }
        tagManager.ApplyModifiedPropertiesWithoutUndo();

        Physics.IgnoreLayerCollision(9, 10, true);
    }

    private static void ConfigureXRLoaderAndOpenXRFeatures()
    {
        // 1. Ensure OpenXRLoader asset exists
        string loaderPath = "Assets/XR/Loaders/OpenXRLoader.asset";
        OpenXRLoader openXrLoader = AssetDatabase.LoadAssetAtPath<OpenXRLoader>(loaderPath);
        if (openXrLoader == null)
        {
            openXrLoader = ScriptableObject.CreateInstance<OpenXRLoader>();
            openXrLoader.name = "OpenXRLoader";
            AssetDatabase.CreateAsset(openXrLoader, loaderPath);
        }

        // 2. Ensure XRGeneralSettingsPerBuildTarget exists and assigns OpenXRLoader for Standalone & Android
        string generalSettingsPath = "Assets/XR/XRGeneralSettingsPerBuildTarget.asset";
        XRGeneralSettingsPerBuildTarget perBuildTarget = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(generalSettingsPath);
        if (perBuildTarget == null)
        {
            perBuildTarget = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
            perBuildTarget.name = "XRGeneralSettingsPerBuildTarget";
            AssetDatabase.CreateAsset(perBuildTarget, generalSettingsPath);
        }
        EditorBuildSettings.AddConfigObject(XRGeneralSettings.settingsKey, perBuildTarget, true);

        BuildTargetGroup[] targetGroups = new[] { BuildTargetGroup.Standalone, BuildTargetGroup.Android };
        foreach (BuildTargetGroup group in targetGroups)
        {
            if (!perBuildTarget.HasSettingsForBuildTarget(group))
            {
                perBuildTarget.CreateDefaultSettingsForBuildTarget(group);
            }
            if (!perBuildTarget.HasManagerSettingsForBuildTarget(group))
            {
                perBuildTarget.CreateDefaultManagerSettingsForBuildTarget(group);
            }

            XRGeneralSettings generalSettings = perBuildTarget.SettingsForBuildTarget(group);
            if (generalSettings != null)
            {
                generalSettings.InitManagerOnStart = true;
                EditorUtility.SetDirty(generalSettings);
            }

            XRManagerSettings managerSettings = perBuildTarget.ManagerSettingsForBuildTarget(group);
            if (managerSettings != null)
            {
                List<XRLoader> loaders = new List<XRLoader> { openXrLoader };
                managerSettings.TrySetLoaders(loaders);
                EditorUtility.SetDirty(managerSettings);
            }
        }
        EditorUtility.SetDirty(perBuildTarget);

        // 3. Ensure OpenXR Package Settings has entries for both Standalone and Android
        EnsureAndroidOpenXRSettingsRegistered();

        foreach (BuildTargetGroup group in targetGroups)
        {
            FeatureHelpers.RefreshFeatures(group);
            OpenXRSettings settings = OpenXRSettings.GetSettingsForBuildTargetGroup(group);
            if (settings == null) continue;

            settings.renderMode = OpenXRSettings.RenderMode.SinglePassInstanced;

            OpenXRFeature[] features = settings.GetFeatures<OpenXRFeature>();
            if (features != null)
            {
                foreach (OpenXRFeature feature in features)
                {
                    if (feature == null) continue;
                    string typeName = feature.GetType().Name;

                    // OculusQuestFeature is deprecated in favor of MetaQuestFeature; keep it disabled
                    if (typeName == "OculusQuestFeature")
                    {
                        feature.enabled = false;
                        EditorUtility.SetDirty(feature);
                        continue;
                    }

                    if (typeName.Contains("OculusTouch") ||
                        typeName.Contains("MetaQuest"))
                    {
                        feature.enabled = true;
                        if (feature is MetaQuestFeature metaQuest)
                        {
                            metaQuest.AddTargetDevice("quest", "Quest", true);
                            metaQuest.AddTargetDevice("quest2", "Quest 2", true);
                            metaQuest.AddTargetDevice("cambria", "Quest Pro", true);
                            metaQuest.AddTargetDevice("eureka", "Quest 3", true);
                            metaQuest.AddTargetDevice("quest3s", "Quest 3S", true);
                        }
                        EditorUtility.SetDirty(feature);
                    }
                }
            }
            EditorUtility.SetDirty(settings);
        }

        AssetDatabase.SaveAssets();
    }

    private static void EnsureAndroidOpenXRSettingsRegistered()
    {
        string pkgSettingsPath = "Assets/XR/Settings/OpenXR Package Settings.asset";
        ScriptableObject pkgSettingsObj = AssetDatabase.LoadAssetAtPath<ScriptableObject>(pkgSettingsPath);
        if (pkgSettingsObj == null) return;

        FieldInfo settingsDictField = pkgSettingsObj.GetType().GetField("Settings", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (settingsDictField == null) return;

        var dict = settingsDictField.GetValue(pkgSettingsObj) as Dictionary<BuildTargetGroup, OpenXRSettings>;
        if (dict == null) return;

        if (!dict.TryGetValue(BuildTargetGroup.Android, out OpenXRSettings androidSettings) || androidSettings == null)
        {
            UnityEngine.Object[] allSubAssets = AssetDatabase.LoadAllAssetsAtPath(pkgSettingsPath);
            foreach (UnityEngine.Object sub in allSubAssets)
            {
                if (sub is OpenXRSettings existing && existing.name == "Android")
                {
                    androidSettings = existing;
                    break;
                }
            }

            if (androidSettings == null)
            {
                androidSettings = ScriptableObject.CreateInstance<OpenXRSettings>();
                androidSettings.name = "Android";
                AssetDatabase.AddObjectToAsset(androidSettings, pkgSettingsObj);
            }

            dict[BuildTargetGroup.Android] = androidSettings;
            EditorUtility.SetDirty(androidSettings);
            EditorUtility.SetDirty(pkgSettingsObj);
            AssetDatabase.SaveAssets();
        }
    }

    private static Dictionary<string, Material> CreateMaterials()
    {
        Dictionary<string, Material> mats = new Dictionary<string, Material>();
        Shader shader = Shader.Find("Standard");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");

        mats["ArenaFloor"] = CreateOrUpdateMaterial("Assets/Materials/ArenaFloor.mat", shader, new Color(0.28f, 0.24f, 0.19f), 0.05f, 0.2f);
        mats["ArenaStone"] = CreateOrUpdateMaterial("Assets/Materials/ArenaStone.mat", shader, new Color(0.35f, 0.36f, 0.38f), 0.1f, 0.25f);
        mats["WoodTimber"] = CreateOrUpdateMaterial("Assets/Materials/WoodTimber.mat", shader, new Color(0.42f, 0.26f, 0.14f), 0.0f, 0.3f);
        mats["SteelBlade"] = CreateOrUpdateMaterial("Assets/Materials/SteelBlade.mat", shader, new Color(0.82f, 0.85f, 0.88f), 0.85f, 0.78f);
        mats["DarkIron"] = CreateOrUpdateMaterial("Assets/Materials/DarkIron.mat", shader, new Color(0.22f, 0.23f, 0.25f), 0.75f, 0.55f);
        mats["ShieldWood"] = CreateOrUpdateMaterial("Assets/Materials/ShieldWood.mat", shader, new Color(0.58f, 0.16f, 0.12f), 0.05f, 0.35f);
        mats["EnemyTunic"] = CreateOrUpdateMaterial("Assets/Materials/EnemyTunic.mat", shader, new Color(0.18f, 0.28f, 0.38f), 0.1f, 0.3f);
        mats["EnemySkin"] = CreateOrUpdateMaterial("Assets/Materials/EnemySkin.mat", shader, new Color(0.82f, 0.66f, 0.54f), 0.0f, 0.25f);
        mats["BloodRed"] = CreateOrUpdateMaterial("Assets/Materials/BloodRed.mat", shader, new Color(0.75f, 0.08f, 0.08f), 0.1f, 0.6f);
        mats["PlayerHand"] = CreateOrUpdateMaterial("Assets/Materials/PlayerHand.mat", shader, new Color(0.32f, 0.58f, 0.85f), 0.2f, 0.6f);

        Material torchMat = CreateOrUpdateMaterial("Assets/Materials/TorchFlame.mat", shader, new Color(1.0f, 0.55f, 0.1f), 0.0f, 0.8f);
        torchMat.EnableKeyword("_EMISSION");
        torchMat.SetColor("_EmissionColor", new Color(2.2f, 1.0f, 0.2f));
        EditorUtility.SetDirty(torchMat);
        mats["TorchFlame"] = torchMat;

        return mats;
    }

    private static Material CreateOrUpdateMaterial(string path, Shader shader, Color color, float metallic, float glossiness)
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.color = color;
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
        if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", glossiness);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static AnimatorController CreateEnemyAnimatorController()
    {
        string path = "Assets/Animations/EnemyAnimatorController.controller";
        if (File.Exists(path)) AssetDatabase.DeleteAsset(path);

        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Die", AnimatorControllerParameterType.Trigger);

        AnimatorStateMachine rootSm = controller.layers[0].stateMachine;
        AnimatorState idleState = rootSm.AddState("Idle");
        AnimatorState walkState = rootSm.AddState("Walk");
        AnimatorState attackState = rootSm.AddState("Attack");
        AnimatorState hitState = rootSm.AddState("Hit");
        AnimatorState dieState = rootSm.AddState("Die");

        rootSm.defaultState = idleState;

        AnimatorStateTransition toWalk = idleState.AddTransition(walkState);
        toWalk.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
        toWalk.hasExitTime = false;

        AnimatorStateTransition toIdle = walkState.AddTransition(idleState);
        toIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
        toIdle.hasExitTime = false;

        AnimatorStateTransition anyToAttack = rootSm.AddAnyStateTransition(attackState);
        anyToAttack.AddCondition(AnimatorConditionMode.If, 0f, "Attack");
        anyToAttack.hasExitTime = false;

        AnimatorStateTransition attackToIdle = attackState.AddTransition(idleState);
        attackToIdle.hasExitTime = true;
        attackToIdle.exitTime = 0.85f;

        AnimatorStateTransition anyToHit = rootSm.AddAnyStateTransition(hitState);
        anyToHit.AddCondition(AnimatorConditionMode.If, 0f, "Hit");
        anyToHit.hasExitTime = false;

        AnimatorStateTransition hitToIdle = hitState.AddTransition(idleState);
        hitToIdle.hasExitTime = true;
        hitToIdle.exitTime = 0.75f;

        AnimatorStateTransition anyToDie = rootSm.AddAnyStateTransition(dieState);
        anyToDie.AddCondition(AnimatorConditionMode.If, 0f, "Die");
        anyToDie.hasExitTime = false;

        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static AnimatorController CreateHandAnimatorController()
    {
        string path = "Assets/Animations/HandAnimatorController.controller";
        if (File.Exists(path)) AssetDatabase.DeleteAsset(path);

        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        controller.AddParameter("Trigger", AnimatorControllerParameterType.Float);
        controller.AddParameter("Grip", AnimatorControllerParameterType.Float);

        AnimatorStateMachine rootSm = controller.layers[0].stateMachine;
        AnimatorState defaultState = rootSm.AddState("HandPose");
        rootSm.defaultState = defaultState;

        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static InputActionAsset CreateVRInputActions()
    {
        string path = "Assets/Input/VVRInputActions.asset";
        if (File.Exists(path)) AssetDatabase.DeleteAsset(path);

        InputActionAsset asset = ScriptableObject.CreateInstance<InputActionAsset>();
        asset.name = "VVRInputActions";

        InputActionMap hmdMap = new InputActionMap("XRHMD");
        InputAction hmdPos = hmdMap.AddAction("Position", InputActionType.Value, "<XRHMD>/centerEyePosition");
        hmdPos.expectedControlType = "Vector3";
        InputAction hmdRot = hmdMap.AddAction("Rotation", InputActionType.Value, "<XRHMD>/centerEyeRotation");
        hmdRot.expectedControlType = "Quaternion";
        asset.AddActionMap(hmdMap);

        InputActionMap leftMap = new InputActionMap("XRLeftHand");
        leftMap.AddAction("Position", InputActionType.Value, "<XRController>{LeftHand}/devicePosition").expectedControlType = "Vector3";
        leftMap.AddAction("Rotation", InputActionType.Value, "<XRController>{LeftHand}/deviceRotation").expectedControlType = "Quaternion";
        leftMap.AddAction("Select", InputActionType.Button, "<XRController>{LeftHand}/gripPressed");
        leftMap.AddAction("Activate", InputActionType.Button, "<XRController>{LeftHand}/triggerPressed");
        leftMap.AddAction("PinchValue", InputActionType.Value, "<XRController>{LeftHand}/trigger").expectedControlType = "Axis";
        leftMap.AddAction("GripValue", InputActionType.Value, "<XRController>{LeftHand}/grip").expectedControlType = "Axis";
        leftMap.AddAction("Haptic", InputActionType.PassThrough, "<XRController>{LeftHand}/haptic").expectedControlType = "Haptic";
        asset.AddActionMap(leftMap);

        InputActionMap rightMap = new InputActionMap("XRRightHand");
        rightMap.AddAction("Position", InputActionType.Value, "<XRController>{RightHand}/devicePosition").expectedControlType = "Vector3";
        rightMap.AddAction("Rotation", InputActionType.Value, "<XRController>{RightHand}/deviceRotation").expectedControlType = "Quaternion";
        rightMap.AddAction("Select", InputActionType.Button, "<XRController>{RightHand}/gripPressed");
        rightMap.AddAction("Activate", InputActionType.Button, "<XRController>{RightHand}/triggerPressed");
        rightMap.AddAction("PinchValue", InputActionType.Value, "<XRController>{RightHand}/trigger").expectedControlType = "Axis";
        rightMap.AddAction("GripValue", InputActionType.Value, "<XRController>{RightHand}/grip").expectedControlType = "Axis";
        rightMap.AddAction("Haptic", InputActionType.PassThrough, "<XRController>{RightHand}/haptic").expectedControlType = "Haptic";
        asset.AddActionMap(rightMap);

        AssetDatabase.CreateAsset(asset, path);
        EditorUtility.SetDirty(asset);
        return asset;
    }

    private static GameObject CreateSwordPrefab(Dictionary<string, Material> mats)
    {
        int enemyLayerIndex = LayerMask.NameToLayer("Enemy");
        if (enemyLayerIndex < 0) enemyLayerIndex = 8;
        int weaponLayerIndex = LayerMask.NameToLayer("Weapon");
        if (weaponLayerIndex < 0) weaponLayerIndex = 9;

        GameObject root = new GameObject("Sword");
        root.layer = weaponLayerIndex;

        Rigidbody rb = root.AddComponent<Rigidbody>();
        rb.mass = 1.2f;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        BoxCollider col = root.AddComponent<BoxCollider>();
        col.center = new Vector3(0f, 0.38f, 0f);
        col.size = new Vector3(0.08f, 0.92f, 0.04f);

        AudioSource audio = root.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        audio.spatialBlend = 1.0f;

        GameObject attachPoint = new GameObject("AttachPoint");
        attachPoint.transform.SetParent(root.transform, false);
        attachPoint.transform.localPosition = new Vector3(0f, 0.02f, 0f);
        attachPoint.transform.localRotation = Quaternion.Euler(65f, 0f, 0f);

        XRGrabInteractable grab = root.AddComponent<XRGrabInteractable>();
        grab.movementType = XRBaseInteractable.MovementType.VelocityTracking;
        grab.throwOnDetach = true;
        grab.attachTransform = attachPoint.transform;

        // Visual primitives
        CreatePrimitiveChild(PrimitiveType.Cylinder, "Hilt", root.transform,
            new Vector3(0f, 0f, 0f), Quaternion.identity, new Vector3(0.035f, 0.11f, 0.035f), mats["WoodTimber"]);
        CreatePrimitiveChild(PrimitiveType.Sphere, "Pommel", root.transform,
            new Vector3(0f, -0.12f, 0f), Quaternion.identity, new Vector3(0.055f, 0.055f, 0.055f), mats["DarkIron"]);
        CreatePrimitiveChild(PrimitiveType.Cube, "Crossguard", root.transform,
            new Vector3(0f, 0.12f, 0f), Quaternion.identity, new Vector3(0.22f, 0.03f, 0.04f), mats["DarkIron"]);
        CreatePrimitiveChild(PrimitiveType.Cube, "Blade", root.transform,
            new Vector3(0f, 0.48f, 0f), Quaternion.identity, new Vector3(0.055f, 0.70f, 0.015f), mats["SteelBlade"]);

        GameObject damagePoint = new GameObject("DamagePoint");
        damagePoint.transform.SetParent(root.transform, false);
        damagePoint.transform.localPosition = new Vector3(0f, 0.82f, 0f);

        Sword sword = root.AddComponent<Sword>();
        sword.baseDamage = 15f;
        sword.damageMultiplier = 2.0f;
        sword.minimumVelocityForDamage = 1.5f;
        sword.swingCooldown = 0.25f;
        sword.damagePoint = damagePoint.transform;
        sword.enemyLayer = 1 << enemyLayerIndex;

        string prefabPath = "Assets/Prefabs/Sword.prefab";
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        UnityEngine.Object.DestroyImmediate(root);
        return prefab;
    }

    private static GameObject CreateAxePrefab(Dictionary<string, Material> mats)
    {
        int enemyLayerIndex = LayerMask.NameToLayer("Enemy");
        if (enemyLayerIndex < 0) enemyLayerIndex = 8;
        int weaponLayerIndex = LayerMask.NameToLayer("Weapon");
        if (weaponLayerIndex < 0) weaponLayerIndex = 9;

        GameObject root = new GameObject("Axe");
        root.layer = weaponLayerIndex;

        Rigidbody rb = root.AddComponent<Rigidbody>();
        rb.mass = 1.8f;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        BoxCollider col = root.AddComponent<BoxCollider>();
        col.center = new Vector3(0.04f, 0.32f, 0f);
        col.size = new Vector3(0.24f, 0.78f, 0.06f);

        AudioSource audio = root.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        audio.spatialBlend = 1.0f;

        GameObject attachPoint = new GameObject("AttachPoint");
        attachPoint.transform.SetParent(root.transform, false);
        attachPoint.transform.localPosition = new Vector3(0f, 0.14f, 0f);
        attachPoint.transform.localRotation = Quaternion.Euler(65f, 0f, 0f);

        XRGrabInteractable grab = root.AddComponent<XRGrabInteractable>();
        grab.movementType = XRBaseInteractable.MovementType.VelocityTracking;
        grab.throwOnDetach = true;
        grab.attachTransform = attachPoint.transform;

        // Visual primitives
        CreatePrimitiveChild(PrimitiveType.Cylinder, "Shaft", root.transform,
            new Vector3(0f, 0.28f, 0f), Quaternion.identity, new Vector3(0.04f, 0.38f, 0.04f), mats["WoodTimber"]);
        CreatePrimitiveChild(PrimitiveType.Cube, "AxeHeadSocket", root.transform,
            new Vector3(0.02f, 0.56f, 0f), Quaternion.identity, new Vector3(0.10f, 0.10f, 0.05f), mats["DarkIron"]);
        CreatePrimitiveChild(PrimitiveType.Cube, "BeardedBlade", root.transform,
            new Vector3(0.11f, 0.54f, 0f), Quaternion.Euler(0f, 0f, -8f), new Vector3(0.14f, 0.22f, 0.02f), mats["SteelBlade"]);

        GameObject axeHead = new GameObject("AxeHead");
        axeHead.transform.SetParent(root.transform, false);
        axeHead.transform.localPosition = new Vector3(0.14f, 0.56f, 0f);

        Axe axe = root.AddComponent<Axe>();
        axe.baseDamage = 20f;
        axe.damageMultiplier = 2.5f;
        axe.minimumVelocityForDamage = 1.5f;
        axe.swingCooldown = 0.35f;
        axe.axeHead = axeHead.transform;
        axe.enemyLayer = 1 << enemyLayerIndex;

        string prefabPath = "Assets/Prefabs/Axe.prefab";
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        UnityEngine.Object.DestroyImmediate(root);
        return prefab;
    }

    private static GameObject CreateShieldPrefab(Dictionary<string, Material> mats)
    {
        int weaponLayerIndex = LayerMask.NameToLayer("Weapon");
        if (weaponLayerIndex < 0) weaponLayerIndex = 9;

        GameObject root = new GameObject("Shield");
        root.layer = weaponLayerIndex;

        Rigidbody rb = root.AddComponent<Rigidbody>();
        rb.mass = 2.2f;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        BoxCollider col = root.AddComponent<BoxCollider>();
        col.center = new Vector3(0f, 0f, 0.03f);
        col.size = new Vector3(0.68f, 0.68f, 0.10f);

        AudioSource audio = root.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        audio.spatialBlend = 1.0f;

        GameObject attachPoint = new GameObject("AttachPoint");
        attachPoint.transform.SetParent(root.transform, false);
        attachPoint.transform.localPosition = new Vector3(0f, 0f, -0.02f);
        attachPoint.transform.localRotation = Quaternion.identity;

        XRGrabInteractable grab = root.AddComponent<XRGrabInteractable>();
        grab.movementType = XRBaseInteractable.MovementType.VelocityTracking;
        grab.throwOnDetach = true;
        grab.attachTransform = attachPoint.transform;

        // Round wooden board + iron boss + grip
        CreatePrimitiveChild(PrimitiveType.Cylinder, "ShieldDisc", root.transform,
            new Vector3(0f, 0f, 0.03f), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.66f, 0.02f, 0.66f), mats["ShieldWood"]);
        CreatePrimitiveChild(PrimitiveType.Sphere, "IronBoss", root.transform,
            new Vector3(0f, 0f, 0.06f), Quaternion.identity, new Vector3(0.16f, 0.16f, 0.10f), mats["SteelBlade"]);
        CreatePrimitiveChild(PrimitiveType.Cube, "IronBraceHorizontal", root.transform,
            new Vector3(0f, 0f, 0.045f), Quaternion.identity, new Vector3(0.64f, 0.04f, 0.015f), mats["DarkIron"]);
        CreatePrimitiveChild(PrimitiveType.Cube, "GripHandle", root.transform,
            new Vector3(0f, 0f, -0.02f), Quaternion.identity, new Vector3(0.04f, 0.18f, 0.05f), mats["WoodTimber"]);

        Shield shield = root.AddComponent<Shield>();
        shield.blockAngleTolerance = 100f;
        shield.damageReductionMultiplier = 0.2f;

        string prefabPath = "Assets/Prefabs/Shield.prefab";
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        UnityEngine.Object.DestroyImmediate(root);
        return prefab;
    }

    private static GameObject CreateVikingEnemyPrefab(Dictionary<string, Material> mats, AnimatorController animController)
    {
        int enemyLayerIndex = LayerMask.NameToLayer("Enemy");
        if (enemyLayerIndex < 0) enemyLayerIndex = 8;

        GameObject root = new GameObject("VikingEnemy");
        root.layer = enemyLayerIndex;

        CapsuleCollider capsule = root.AddComponent<CapsuleCollider>();
        capsule.center = new Vector3(0f, 1.0f, 0f);
        capsule.height = 2.0f;
        capsule.radius = 0.38f;

        // Kinematic body so the NavMeshAgent moves a kinematic Rigidbody instead of re-positioning a static collider every frame.
        Rigidbody rootRb = root.AddComponent<Rigidbody>();
        rootRb.isKinematic = true;
        rootRb.useGravity = false;
        rootRb.mass = 80f;

        NavMeshAgent agent = root.AddComponent<NavMeshAgent>();
        agent.speed = 3.2f;
        agent.angularSpeed = 240f;
        agent.acceleration = 12f;
        agent.stoppingDistance = 0.9f;
        agent.height = 2.0f;
        agent.radius = 0.4f;

        Animator animator = root.AddComponent<Animator>();
        animator.runtimeAnimatorController = animController;

        AudioSource audio = root.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        audio.spatialBlend = 1.0f;

        // Visual body parts. Torso and Head are visual only: the root CapsuleCollider fully encloses both and is the
        // single hit volume for player weapons.
        GameObject torso = CreatePrimitiveChild(PrimitiveType.Capsule, "Torso", root.transform,
            new Vector3(0f, 1.05f, 0f), Quaternion.identity, new Vector3(0.55f, 0.58f, 0.38f), mats["EnemyTunic"]);
        torso.layer = enemyLayerIndex;

        GameObject head = CreatePrimitiveChild(PrimitiveType.Sphere, "Head", root.transform,
            new Vector3(0f, 1.78f, 0f), Quaternion.identity, new Vector3(0.32f, 0.34f, 0.32f), mats["EnemySkin"]);
        head.layer = enemyLayerIndex;

        CreatePrimitiveChild(PrimitiveType.Cylinder, "Helmet", head.transform,
            new Vector3(0f, 0.22f, 0f), Quaternion.identity, new Vector3(1.05f, 0.35f, 1.05f), mats["DarkIron"]);
        CreatePrimitiveChild(PrimitiveType.Cylinder, "Beard", head.transform,
            new Vector3(0f, -0.28f, 0.25f), Quaternion.Euler(20f, 0f, 0f), new Vector3(0.65f, 0.35f, 0.45f), mats["WoodTimber"]);

        CreatePrimitiveChild(PrimitiveType.Capsule, "LeftArm", root.transform,
            new Vector3(-0.38f, 1.15f, 0.1f), Quaternion.Euler(25f, 0f, -12f), new Vector3(0.18f, 0.35f, 0.18f), mats["EnemySkin"]);
        CreatePrimitiveChild(PrimitiveType.Capsule, "RightArm", root.transform,
            new Vector3(0.38f, 1.18f, 0.22f), Quaternion.Euler(55f, 0f, 10f), new Vector3(0.18f, 0.35f, 0.18f), mats["EnemySkin"]);

        // Enemy Weapon child with Trigger Collider
        GameObject enemyWeaponObj = new GameObject("EnemyWeapon");
        enemyWeaponObj.transform.SetParent(root.transform, false);
        enemyWeaponObj.transform.localPosition = new Vector3(0.38f, 1.15f, 0.65f);
        enemyWeaponObj.transform.localRotation = Quaternion.Euler(45f, 0f, 0f);

        BoxCollider weaponTrigger = enemyWeaponObj.AddComponent<BoxCollider>();
        weaponTrigger.isTrigger = true;
        weaponTrigger.center = new Vector3(0f, 0.25f, 0f);
        weaponTrigger.size = new Vector3(0.45f, 0.65f, 0.25f);

        Rigidbody weaponRb = enemyWeaponObj.AddComponent<Rigidbody>();
        weaponRb.isKinematic = true;
        weaponRb.useGravity = false;

        CreatePrimitiveChild(PrimitiveType.Cylinder, "EnemyAxeShaft", enemyWeaponObj.transform,
            new Vector3(0f, 0.2f, 0f), Quaternion.identity, new Vector3(0.04f, 0.32f, 0.04f), mats["WoodTimber"]);
        CreatePrimitiveChild(PrimitiveType.Cube, "EnemyAxeBlade", enemyWeaponObj.transform,
            new Vector3(0f, 0.42f, 0.10f), Quaternion.identity, new Vector3(0.03f, 0.18f, 0.16f), mats["SteelBlade"]);

        EnemyWeapon enemyWeapon = enemyWeaponObj.AddComponent<EnemyWeapon>();
        enemyWeapon.damage = 15f;

        // Blood Hit Particle System
        GameObject hitFxObj = new GameObject("HitEffect");
        hitFxObj.transform.SetParent(root.transform, false);
        hitFxObj.transform.localPosition = new Vector3(0f, 1.25f, 0f);
        ParticleSystem ps = hitFxObj.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 0.35f;
        main.startLifetime = 0.35f;
        main.startSpeed = 3.0f;
        main.startSize = 0.08f;
        main.startColor = new Color(0.8f, 0.05f, 0.05f, 1f);
        main.maxParticles = 64;
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystemRenderer psRenderer = hitFxObj.GetComponent<ParticleSystemRenderer>();
        if (psRenderer != null) psRenderer.sharedMaterial = mats["BloodRed"];

        EnemyHealth enemyHealth = root.AddComponent<EnemyHealth>();
        enemyHealth.maxHealth = 50f;
        enemyHealth.bloodEffect = ps;
        enemyHealth.audioSource = audio;

        EnemyAI enemyAI = root.AddComponent<EnemyAI>();
        enemyAI.attackRange = 1.2f;
        enemyAI.attackCooldown = 2.0f;
        enemyAI.moveSpeed = 3.2f;
        enemyAI.enemyWeapon = enemyWeapon;

        string prefabPath = "Assets/Prefabs/VikingEnemy.prefab";
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        UnityEngine.Object.DestroyImmediate(root);
        return prefab;
    }

    private static void BuildVikingArenaScene(
        Dictionary<string, Material> mats,
        AnimatorController handAnimController,
        InputActionAsset inputAsset,
        GameObject swordPrefab,
        GameObject axePrefab,
        GameObject shieldPrefab,
        GameObject enemyPrefab)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // 1. Directional Light & Environment Lighting
        GameObject lightObj = new GameObject("Directional Light");
        Light dirLight = lightObj.AddComponent<Light>();
        dirLight.type = LightType.Directional;
        dirLight.color = new Color(1.0f, 0.92f, 0.80f);
        dirLight.intensity = 1.15f;
        dirLight.shadows = LightShadows.Soft;
        lightObj.transform.rotation = Quaternion.Euler(48f, -35f, 0f);

        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.28f, 0.32f, 0.42f);
        RenderSettings.ambientEquatorColor = new Color(0.22f, 0.20f, 0.18f);
        RenderSettings.ambientGroundColor = new Color(0.12f, 0.10f, 0.08f);

        // 2. Viking Arena Environment + NavMeshSurface
        GameObject arenaEnv = new GameObject("VikingArena_Environment");

        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "ArenaFloor";
        floor.transform.SetParent(arenaEnv.transform, false);
        floor.transform.position = new Vector3(0f, -0.5f, 0f);
        floor.transform.localScale = new Vector3(28f, 1f, 28f);
        floor.GetComponent<MeshRenderer>().sharedMaterial = mats["ArenaFloor"];

        // Octagonal Stone Walls & Timber Pillars
        int wallSegments = 8;
        float arenaRadius = 12.5f;
        for (int i = 0; i < wallSegments; i++)
        {
            float angleDeg = i * (360f / wallSegments);
            float angleRad = angleDeg * Mathf.Deg2Rad;
            Vector3 wallPos = new Vector3(Mathf.Sin(angleRad) * arenaRadius, 1.5f, Mathf.Cos(angleRad) * arenaRadius);
            Quaternion wallRot = Quaternion.Euler(0f, angleDeg, 0f);

            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = $"ArenaWall_{i}";
            wall.transform.SetParent(arenaEnv.transform, false);
            wall.transform.position = wallPos;
            wall.transform.rotation = wallRot;
            wall.transform.localScale = new Vector3(10.5f, 3.2f, 0.8f);
            wall.GetComponent<MeshRenderer>().sharedMaterial = mats["ArenaStone"];

            // Timber Pillar at each corner
            float cornerAngleDeg = angleDeg + (180f / wallSegments);
            float cornerAngleRad = cornerAngleDeg * Mathf.Deg2Rad;
            Vector3 pillarPos = new Vector3(Mathf.Sin(cornerAngleRad) * (arenaRadius - 0.6f), 2.0f, Mathf.Cos(cornerAngleRad) * (arenaRadius - 0.6f));

            GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pillar.name = $"TimberPillar_{i}";
            pillar.transform.SetParent(arenaEnv.transform, false);
            pillar.transform.position = pillarPos;
            pillar.transform.localScale = new Vector3(0.75f, 2.2f, 0.75f);
            pillar.GetComponent<MeshRenderer>().sharedMaterial = mats["WoodTimber"];

            GameObject torch = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            torch.name = $"TorchFlame_{i}";
            torch.transform.SetParent(pillar.transform, false);
            torch.transform.localPosition = new Vector3(0f, 1.1f, 0f);
            torch.transform.localScale = new Vector3(0.45f, 0.45f, 0.45f);
            torch.GetComponent<MeshRenderer>().sharedMaterial = mats["TorchFlame"];
            UnityEngine.Object.DestroyImmediate(torch.GetComponent<Collider>());
        }

        // Weapon Rack / Table positioned at comfortable arm's reach (z = 0.55m) in front of the player
        GameObject weaponRack = GameObject.CreatePrimitive(PrimitiveType.Cube);
        weaponRack.name = "WeaponRack";
        weaponRack.transform.SetParent(arenaEnv.transform, false);
        weaponRack.transform.position = new Vector3(0f, 0.42f, 0.55f);
        weaponRack.transform.localScale = new Vector3(1.35f, 0.84f, 0.45f);
        weaponRack.GetComponent<MeshRenderer>().sharedMaterial = mats["WoodTimber"];

        // Bake NavMesh on ArenaFloor
        NavMeshSurface navSurface = floor.AddComponent<NavMeshSurface>();
        navSurface.collectObjects = CollectObjects.All;
        navSurface.BuildNavMesh();

        // 3. VR Player Rig (XR Origin + Left/Right Controllers + PlayerHealth)
        int playerLayerIndex = LayerMask.NameToLayer("Player");
        if (playerLayerIndex < 0) playerLayerIndex = 10;

        GameObject xrOriginObj = new GameObject("XR Origin (VR)");
        xrOriginObj.tag = "Player";
        xrOriginObj.layer = playerLayerIndex;
        xrOriginObj.transform.position = Vector3.zero;

        XRInteractionManager interactionManager = xrOriginObj.AddComponent<XRInteractionManager>();
        XROrigin xrOrigin = xrOriginObj.AddComponent<XROrigin>();

        CapsuleCollider playerCollider = xrOriginObj.AddComponent<CapsuleCollider>();
        playerCollider.center = new Vector3(0f, 0.9f, 0f);
        playerCollider.height = 1.8f;
        playerCollider.radius = 0.28f;

        Rigidbody playerRb = xrOriginObj.AddComponent<Rigidbody>();
        playerRb.isKinematic = true;
        playerRb.useGravity = false;

        PlayerHealth playerHealth = xrOriginObj.AddComponent<PlayerHealth>();
        playerHealth.maxHealth = 100f;

        GameObject cameraOffsetObj = new GameObject("Camera Offset");
        cameraOffsetObj.transform.SetParent(xrOriginObj.transform, false);
        cameraOffsetObj.transform.localPosition = Vector3.zero;

        // Main Camera
        GameObject mainCamObj = new GameObject("Main Camera");
        mainCamObj.tag = "MainCamera";
        mainCamObj.transform.SetParent(cameraOffsetObj.transform, false);
        mainCamObj.transform.localPosition = new Vector3(0f, 1.65f, 0f);

        Camera mainCamera = mainCamObj.AddComponent<Camera>();
        mainCamera.nearClipPlane = 0.05f;
        mainCamera.farClipPlane = 200f;
        mainCamera.clearFlags = CameraClearFlags.Skybox;
        mainCamObj.AddComponent<AudioListener>();

        TrackedPoseDriver hmdTpd = mainCamObj.AddComponent<TrackedPoseDriver>();
        InputAction hmdPosAction = new InputAction("HMDPosition", InputActionType.Value, "<XRHMD>/centerEyePosition");
        hmdPosAction.AddBinding("<XRHMD>/devicePosition");
        InputAction hmdRotAction = new InputAction("HMDRotation", InputActionType.Value, "<XRHMD>/centerEyeRotation");
        hmdRotAction.AddBinding("<XRHMD>/deviceRotation");
        hmdTpd.positionInput = new InputActionProperty(hmdPosAction);
        hmdTpd.rotationInput = new InputActionProperty(hmdRotAction);

        xrOrigin.CameraFloorOffsetObject = cameraOffsetObj;
        xrOrigin.Camera = mainCamera;
        xrOrigin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;

        // Left & Right Controllers (configured with Quest 2 Grip Select, Trigger Activate, and Haptics)
        CreateVRController(
            "Left Hand Controller",
            cameraOffsetObj.transform,
            new Vector3(-0.28f, 1.05f, 0.42f),
            "{LeftHand}",
            interactionManager,
            handAnimController,
            mats["PlayerHand"]);

        CreateVRController(
            "Right Hand Controller",
            cameraOffsetObj.transform,
            new Vector3(0.28f, 1.05f, 0.42f),
            "{RightHand}",
            interactionManager,
            handAnimController,
            mats["PlayerHand"]);

        // 4. Instantiate Grabbable Weapons on the Weapon Rack within immediate Quest 2 controller reach (z = 0.55m, y = 0.92m)
        GameObject swordInstance = (GameObject)PrefabUtility.InstantiatePrefab(swordPrefab);
        swordInstance.name = "Sword";
        swordInstance.transform.position = new Vector3(0.35f, 0.92f, 0.55f);
        swordInstance.transform.rotation = Quaternion.Euler(90f, 0f, -15f);

        GameObject axeInstance = (GameObject)PrefabUtility.InstantiatePrefab(axePrefab);
        axeInstance.name = "Axe";
        axeInstance.transform.position = new Vector3(0.0f, 0.92f, 0.55f);
        axeInstance.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        GameObject shieldInstance = (GameObject)PrefabUtility.InstantiatePrefab(shieldPrefab);
        shieldInstance.name = "Shield";
        shieldInstance.transform.position = new Vector3(-0.35f, 0.92f, 0.55f);
        shieldInstance.transform.rotation = Quaternion.Euler(-75f, 0f, 0f);

        playerHealth.activeShield = shieldInstance.GetComponent<Shield>();

        // 5. Spawn Points & Managers (WaveManager + GameManager + World-Space UI)
        GameObject spawnPointsRoot = new GameObject("SpawnPoints");
        Vector3[] spawnPositions = new[]
        {
            new Vector3(0f, 0f, 8.5f),
            new Vector3(7.5f, 0f, 4.5f),
            new Vector3(-7.5f, 0f, 4.5f),
            new Vector3(0f, 0f, -8.5f)
        };
        Transform[] spawnTransforms = new Transform[spawnPositions.Length];
        for (int i = 0; i < spawnPositions.Length; i++)
        {
            GameObject sp = new GameObject($"SpawnPoint_{i + 1}");
            sp.transform.SetParent(spawnPointsRoot.transform, false);
            sp.transform.position = spawnPositions[i];
            sp.transform.LookAt(Vector3.zero);
            spawnTransforms[i] = sp.transform;
        }

        // World-Space Arena HUD Canvas
        GameObject hudCanvasObj = CreateWorldCanvas("ArenaHUDCanvas", new Vector3(0f, 3.3f, 6.2f), new Vector2(640f, 220f), 0.008f);
        TextMeshProUGUI waveText = CreateTMPText("WaveText", hudCanvasObj.transform, new Vector2(0f, 55f), "Wave: 1", 44f, new Color(1f, 0.84f, 0.35f));
        TextMeshProUGUI enemiesLeftText = CreateTMPText("EnemiesLeftText", hudCanvasObj.transform, new Vector2(0f, 0f), "Enemies: 3", 36f, Color.white);
        TextMeshProUGUI killCountText = CreateTMPText("KillCountText", hudCanvasObj.transform, new Vector2(0f, -55f), "Kills: 0", 36f, new Color(0.8f, 0.92f, 1f));

        // Game Over & Victory Canvases
        GameObject gameOverCanvasObj = CreateWorldCanvas("GameOverCanvas", new Vector3(0f, 1.65f, 2.4f), new Vector2(520f, 200f), 0.004f);
        CreateTMPText("GameOverTitle", gameOverCanvasObj.transform, new Vector2(0f, 20f), "VALHALLA AWAITS\nGAME OVER", 42f, new Color(0.95f, 0.25f, 0.25f));
        gameOverCanvasObj.SetActive(false);

        GameObject victoryCanvasObj = CreateWorldCanvas("VictoryCanvas", new Vector3(0f, 1.65f, 2.4f), new Vector2(520f, 200f), 0.004f);
        CreateTMPText("VictoryTitle", victoryCanvasObj.transform, new Vector2(0f, 20f), "VICTORY IN THE ARENA!", 42f, new Color(0.3f, 0.95f, 0.45f));
        victoryCanvasObj.SetActive(false);

        // GameSystems (WaveManager + GameManager)
        GameObject systemsObj = new GameObject("GameSystems");
        WaveManager waveManager = systemsObj.AddComponent<WaveManager>();
        waveManager.enemyPrefabs = new[] { enemyPrefab };
        waveManager.spawnPoints = spawnTransforms;
        waveManager.timeBetweenWaves = 3f;
        waveManager.spawnStaggerDelay = 0.75f;
        waveManager.baseEnemiesPerWave = 3;
        waveManager.difficultyMultiplier = 1.5f;
        waveManager.waveText = waveText;
        waveManager.enemiesLeftText = enemiesLeftText;
        waveManager.killCountText = killCountText;

        GameManager gameManager = systemsObj.AddComponent<GameManager>();
        gameManager.playerHealth = playerHealth;
        gameManager.waveManager = waveManager;
        gameManager.gameOverCanvas = gameOverCanvasObj;
        gameManager.victoryCanvas = victoryCanvasObj;

        // 6. Save Scene & Register in EditorBuildSettings
        string scenePath = "Assets/Scenes/VikingArena.unity";
        EditorSceneManager.SaveScene(scene, scenePath);

        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(scenePath, true)
        };

        // 7. Capture Preview Screenshot
        CaptureArenaScreenshots(enemyPrefab);
    }

    private static void CreateVRController(
        string name,
        Transform parent,
        Vector3 localPos,
        string handUsage,
        XRInteractionManager interactionManager,
        AnimatorController handAnimController,
        Material handMat)
    {
        GameObject controllerObj = new GameObject(name);
        controllerObj.transform.SetParent(parent, false);
        controllerObj.transform.localPosition = localPos;

        TrackedPoseDriver tpd = controllerObj.AddComponent<TrackedPoseDriver>();
        InputAction posAction = new InputAction($"{name}_Pos", InputActionType.Value, $"<XRController>{handUsage}/devicePosition");
        posAction.AddBinding($"<XRController>{handUsage}/pointerPosition");
        InputAction rotAction = new InputAction($"{name}_Rot", InputActionType.Value, $"<XRController>{handUsage}/deviceRotation");
        rotAction.AddBinding($"<XRController>{handUsage}/pointerRotation");
        tpd.positionInput = new InputActionProperty(posAction);
        tpd.rotationInput = new InputActionProperty(rotAction);

        SphereCollider triggerCol = controllerObj.AddComponent<SphereCollider>();
        triggerCol.isTrigger = true;
        triggerCol.radius = 0.16f;

        XRDirectInteractor interactor = controllerObj.AddComponent<XRDirectInteractor>();
        interactor.interactionManager = interactionManager;
        interactor.selectActionTrigger = XRBaseInputInteractor.InputTriggerType.State;

        // Configure Quest 2 Grip button for Select (Grab)
        XRInputButtonReader selectReader = interactor.selectInput;
        selectReader.inputSourceMode = XRInputButtonReader.InputSourceMode.InputAction;
        InputAction selectPerformed = new InputAction($"{name}_Select", InputActionType.Button, $"<XRController>{handUsage}/gripPressed");
        selectPerformed.AddBinding($"<XRController>{handUsage}/selectPressed");
        selectReader.inputActionPerformed = selectPerformed;
        selectReader.inputActionValue = new InputAction($"{name}_SelectValue", InputActionType.Value, $"<XRController>{handUsage}/grip");
        interactor.selectInput = selectReader;

        // Configure Quest 2 Trigger button for Activate
        XRInputButtonReader activateReader = interactor.activateInput;
        activateReader.inputSourceMode = XRInputButtonReader.InputSourceMode.InputAction;
        activateReader.inputActionPerformed = new InputAction($"{name}_Activate", InputActionType.Button, $"<XRController>{handUsage}/triggerPressed");
        activateReader.inputActionValue = new InputAction($"{name}_ActivateValue", InputActionType.Value, $"<XRController>{handUsage}/trigger");
        interactor.activateInput = activateReader;

        // Configure Quest 2 Controller Haptics
        HapticImpulsePlayer hapticPlayer = controllerObj.AddComponent<HapticImpulsePlayer>();
        XRInputHapticImpulseProvider hapticOutput = hapticPlayer.hapticOutput;
        hapticOutput.inputSourceMode = XRInputHapticImpulseProvider.InputSourceMode.InputAction;
        hapticOutput.inputAction = new InputAction($"{name}_Haptic", InputActionType.PassThrough, $"<XRController>{handUsage}/haptic");
        hapticPlayer.hapticOutput = hapticOutput;

        Animator animator = controllerObj.AddComponent<Animator>();
        animator.runtimeAnimatorController = handAnimController;

        HandAnimator handAnimator = controllerObj.AddComponent<HandAnimator>();
        handAnimator.handAnimator = animator;
        handAnimator.pinchAnimationAction = new InputActionProperty(new InputAction($"{name}_Pinch", InputActionType.Value, $"<XRController>{handUsage}/trigger"));
        handAnimator.gripAnimationAction = new InputActionProperty(new InputAction($"{name}_Grip", InputActionType.Value, $"<XRController>{handUsage}/grip"));

        CreatePrimitiveChild(PrimitiveType.Cube, "HandVisual", controllerObj.transform,
            Vector3.zero, Quaternion.identity, new Vector3(0.08f, 0.05f, 0.14f), handMat);
    }

    private static GameObject CreateWorldCanvas(string name, Vector3 position, Vector2 sizeDelta, float scale)
    {
        GameObject canvasObj = new GameObject(name);
        canvasObj.transform.position = position;
        canvasObj.transform.localScale = Vector3.one * scale;

        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvasObj.AddComponent<CanvasScaler>();
        canvasObj.AddComponent<GraphicRaycaster>();

        RectTransform rt = canvasObj.GetComponent<RectTransform>();
        rt.sizeDelta = sizeDelta;

        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(canvasObj.transform, false);
        Image bgImage = bgObj.AddComponent<Image>();
        bgImage.color = new Color(0.08f, 0.09f, 0.12f, 0.85f);
        RectTransform bgRt = bgObj.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;

        return canvasObj;
    }

    private static TextMeshProUGUI CreateTMPText(string name, Transform parent, Vector2 anchoredPos, string text, float fontSize, Color color)
    {
        GameObject textObj = new GameObject(name);
        textObj.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;

        RectTransform rt = textObj.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(580f, 60f);
        rt.anchoredPosition = anchoredPos;
        return tmp;
    }

    private static GameObject CreatePrimitiveChild(
        PrimitiveType type,
        string name,
        Transform parent,
        Vector3 localPos,
        Quaternion localRot,
        Vector3 localScale,
        Material mat,
        bool removeCollider = true)
    {
        GameObject obj = GameObject.CreatePrimitive(type);
        obj.name = name;
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = localPos;
        obj.transform.localRotation = localRot;
        obj.transform.localScale = localScale;

        MeshRenderer renderer = obj.GetComponent<MeshRenderer>();
        if (renderer != null && mat != null)
        {
            renderer.sharedMaterial = mat;
        }

        if (removeCollider)
        {
            Collider col = obj.GetComponent<Collider>();
            if (col != null) UnityEngine.Object.DestroyImmediate(col);
        }

        return obj;
    }

    private static void CaptureArenaScreenshots(GameObject enemyPrefab)
    {
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
        {
            Debug.Log("[VVRProjectSetup] GraphicsDeviceType is Null (-nographics); skipping screenshot render.");
            return;
        }

        try
        {
            string screenshotDir = Path.Combine(Directory.GetCurrentDirectory(), "Screenshots");
            Directory.CreateDirectory(screenshotDir);

            GameObject previewEnemy = (GameObject)PrefabUtility.InstantiatePrefab(enemyPrefab);
            previewEnemy.transform.position = new Vector3(0f, 0f, 4.2f);
            previewEnemy.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            GameObject overviewCamObj = new GameObject("TempOverviewCamera");
            overviewCamObj.transform.position = new Vector3(1.6f, 2.4f, -1.8f);
            overviewCamObj.transform.LookAt(new Vector3(0f, 1.2f, 2.5f));
            Camera overviewCam = overviewCamObj.AddComponent<Camera>();
            overviewCam.clearFlags = CameraClearFlags.Skybox;
            overviewCam.fieldOfView = 60f;

            RenderCameraToPng(overviewCam, Path.Combine(screenshotDir, "VikingArena_Overview.png"), 1280, 720);

            if (Camera.main != null)
            {
                RenderCameraToPng(Camera.main, Path.Combine(screenshotDir, "VikingArena_PlayerView.png"), 1280, 720);
            }

            UnityEngine.Object.DestroyImmediate(overviewCamObj);
            UnityEngine.Object.DestroyImmediate(previewEnemy);

            Debug.Log($"[VVRProjectSetup] Saved scene screenshots to {screenshotDir}");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[VVRProjectSetup] Screenshot capture warning: {ex.Message}");
        }
    }

    private static void RenderCameraToPng(Camera cam, string filePath, int width, int height)
    {
        RenderTexture rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        RenderTexture prevActive = RenderTexture.active;
        RenderTexture prevTarget = cam.targetTexture;

        cam.targetTexture = rt;
        cam.Render();

        RenderTexture.active = rt;
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        tex.Apply();

        byte[] pngBytes = tex.EncodeToPNG();
        File.WriteAllBytes(filePath, pngBytes);

        cam.targetTexture = prevTarget;
        RenderTexture.active = prevActive;

        UnityEngine.Object.DestroyImmediate(tex);
        UnityEngine.Object.DestroyImmediate(rt);
    }
}
