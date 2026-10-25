using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.Management;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features.Interactions;
using UnityEngine.XR.OpenXR.Features.MetaQuestSupport;
using Unity.XR.CoreUtils;

public class VVRVerificationTests
{
    [Test]
    public void Test01_SceneAndPrefabs_ExistAndHaveZeroMissingScripts()
    {
        string scenePath = "Assets/Scenes/VikingArena.unity";
        Assert.IsTrue(System.IO.File.Exists(scenePath), "VikingArena.unity must exist on disk.");

        Assert.IsTrue(EditorBuildSettings.scenes.Length > 0, "EditorBuildSettings.scenes must not be empty.");
        Assert.AreEqual(scenePath, EditorBuildSettings.scenes[0].path);

        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        Assert.IsTrue(scene.IsValid(), "Opened scene must be valid.");

        foreach (GameObject rootGo in scene.GetRootGameObjects())
        {
            AssertZeroMissingScriptsRecursive(rootGo);
        }

        string[] prefabPaths = new[]
        {
            "Assets/Prefabs/Sword.prefab",
            "Assets/Prefabs/Axe.prefab",
            "Assets/Prefabs/Shield.prefab",
            "Assets/Prefabs/VikingEnemy.prefab"
        };

        foreach (string path in prefabPaths)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(prefab, $"Prefab at {path} must exist.");
            AssertZeroMissingScriptsRecursive(prefab);
        }
    }

    private void AssertZeroMissingScriptsRecursive(GameObject go)
    {
        int missingCount = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
        Assert.AreEqual(0, missingCount, $"GameObject '{go.name}' has {missingCount} missing script(s).");

        foreach (Transform child in go.transform)
        {
            AssertZeroMissingScriptsRecursive(child.gameObject);
        }
    }

    [Test]
    public void Test02_SceneWiring_XROriginWeaponsAndManagersConfigured()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/VikingArena.unity", OpenSceneMode.Single);

        XROrigin xrOrigin = Object.FindAnyObjectByType<XROrigin>();
        Assert.IsNotNull(xrOrigin, "XROrigin must exist in VikingArena scene.");
        Assert.AreEqual("Player", xrOrigin.gameObject.tag, "XR Origin must be tagged 'Player'.");
        Assert.IsNotNull(xrOrigin.Camera, "XROrigin.Camera must be assigned.");

        XRDirectInteractor[] interactors = xrOrigin.GetComponentsInChildren<XRDirectInteractor>(true);
        Assert.AreEqual(2, interactors.Length, "Both Left and Right XRDirectInteractors must be present.");

        HandAnimator[] handAnimators = xrOrigin.GetComponentsInChildren<HandAnimator>(true);
        Assert.AreEqual(2, handAnimators.Length, "Both Left and Right HandAnimators must be present.");

        PlayerHealth playerHealth = xrOrigin.GetComponent<PlayerHealth>();
        Assert.IsNotNull(playerHealth, "PlayerHealth must be attached to XR Origin.");
        Assert.IsNotNull(playerHealth.activeShield, "PlayerHealth.activeShield must be wired.");

        Sword sword = Object.FindAnyObjectByType<Sword>();
        Assert.IsNotNull(sword, "Grabbable Sword must exist in scene.");
        Assert.IsNotNull(sword.damagePoint, "Sword.damagePoint must be assigned.");
        Assert.IsNotNull(sword.GetComponent<XRGrabInteractable>(), "Sword must have XRGrabInteractable.");

        Axe axe = Object.FindAnyObjectByType<Axe>();
        Assert.IsNotNull(axe, "Grabbable Axe must exist in scene.");
        Assert.IsNotNull(axe.axeHead, "Axe.axeHead must be assigned.");
        Assert.IsNotNull(axe.GetComponent<XRGrabInteractable>(), "Axe must have XRGrabInteractable.");

        Shield shield = Object.FindAnyObjectByType<Shield>();
        Assert.IsNotNull(shield, "Grabbable Shield must exist in scene.");
        Assert.IsNotNull(shield.GetComponent<XRGrabInteractable>(), "Shield must have XRGrabInteractable.");

        WaveManager waveManager = Object.FindAnyObjectByType<WaveManager>();
        Assert.IsNotNull(waveManager, "WaveManager must exist in scene.");
        Assert.IsNotNull(waveManager.enemyPrefabs);
        Assert.Greater(waveManager.enemyPrefabs.Length, 0);
        Assert.AreEqual(4, waveManager.spawnPoints.Length);
        Assert.IsNotNull(waveManager.waveText);
        Assert.IsNotNull(waveManager.enemiesLeftText);
        Assert.IsNotNull(waveManager.killCountText);

        GameManager gameManager = Object.FindAnyObjectByType<GameManager>();
        Assert.IsNotNull(gameManager, "GameManager must exist in scene.");
        Assert.AreEqual(playerHealth, gameManager.playerHealth);
        Assert.AreEqual(waveManager, gameManager.waveManager);
        Assert.IsNotNull(gameManager.gameOverCanvas);
        Assert.IsNotNull(gameManager.victoryCanvas);
    }

    [Test]
    public void Test03_WeaponVelocity_MeasuredAtBladeTipAndDealsVelocityScaledDamage()
    {
        GameObject swordObj = new GameObject("TestSword");
        swordObj.AddComponent<Rigidbody>();
        swordObj.AddComponent<XRGrabInteractable>();
        Sword sword = swordObj.AddComponent<Sword>();

        GameObject tip = new GameObject("DamagePoint");
        tip.transform.SetParent(swordObj.transform, false);
        tip.transform.localPosition = new Vector3(0f, 1.0f, 0f);
        sword.damagePoint = tip.transform;
        sword.minimumVelocityForDamage = 1.5f;
        sword.baseDamage = 10f;
        sword.damageMultiplier = 2.0f;

        InvokePrivateMethod(sword, "Awake");
        sword.SetHeldState(true);

        // Rotate sword 90 degrees in place without moving the handle root position!
        swordObj.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        InvokePrivateMethod(sword, "FixedUpdate");

        Assert.Greater(sword.CurrentVelocity.magnitude, 10f,
            "Rotating the weapon around a stationary handle must produce high velocity at the blade tip.");

        GameObject enemyObj = new GameObject("TestEnemy");
        EnemyHealth enemyHealth = enemyObj.AddComponent<EnemyHealth>();
        enemyHealth.maxHealth = 200f;
        InvokePrivateMethod(enemyHealth, "Awake");

        bool dealt = sword.TryDealDamage(enemyHealth);
        Assert.IsTrue(dealt, "Held sword with sufficient tip velocity must deal damage.");
        Assert.Less(enemyHealth.currentHealth, 200f, "Enemy health must decrease after sword strike.");

        Object.DestroyImmediate(swordObj);
        Object.DestroyImmediate(enemyObj);
    }

    [Test]
    public void Test04_ShieldBlocking_BlocksIncomingEnemyAttackRegardlessOfHierarchy()
    {
        GameObject playerObj = new GameObject("TestPlayer");
        playerObj.tag = "Player";
        CapsuleCollider playerCol = playerObj.AddComponent<CapsuleCollider>();
        PlayerHealth playerHealth = playerObj.AddComponent<PlayerHealth>();
        playerHealth.maxHealth = 100f;
        InvokePrivateMethod(playerHealth, "Awake");

        GameObject shieldObj = new GameObject("TestShield");
        shieldObj.AddComponent<Rigidbody>();
        shieldObj.AddComponent<XRGrabInteractable>();
        Shield shield = shieldObj.AddComponent<Shield>();
        shield.blockAngleTolerance = 80f;
        shield.damageReductionMultiplier = 0.2f;
        InvokePrivateMethod(shield, "Awake");
        InvokePrivateMethod(shield, "OnEnable");

        playerObj.transform.position = Vector3.zero;
        shieldObj.transform.position = new Vector3(0f, 1f, 0.4f);
        shieldObj.transform.forward = Vector3.forward;

        // 1. Unheld shield must NOT block
        shield.SetHeldState(false);
        Assert.IsFalse(shield.TryBlock(Vector3.forward, out _, 20f), "Unheld shield must not block attacks.");

        // 2. Held shield facing the enemy MUST block and reduce EnemyWeapon damage by 80%
        shield.SetHeldState(true);
        Assert.IsTrue(shield.TryBlock(Vector3.forward, out float reducedDmg, 20f), "Held shield facing enemy must block.");
        Assert.AreEqual(4f, reducedDmg, 0.01f);

        GameObject enemyWeaponObj = new GameObject("TestEnemyWeapon");
        enemyWeaponObj.transform.position = new Vector3(0f, 1f, 1.5f);
        EnemyWeapon enemyWeapon = enemyWeaponObj.AddComponent<EnemyWeapon>();
        enemyWeapon.damage = 20f;
        enemyWeapon.EnableDamage();

        InvokePrivateMethod(enemyWeapon, "OnTriggerEnter", playerCol);
        Assert.AreEqual(96f, playerHealth.currentHealth, 0.01f,
            "Blocked enemy attack should deal only 20% damage (4 HP).");

        // 3. Held shield facing AWAY from the enemy (perpendicular 90 deg > 40 deg half-cone) must NOT block
        shieldObj.transform.forward = Vector3.right;
        enemyWeapon.EnableDamage();
        InvokePrivateMethod(enemyWeapon, "OnTriggerEnter", playerCol);
        Assert.AreEqual(76f, playerHealth.currentHealth, 0.01f,
            "Attack outside the shield's facing cone must deal full damage (20 HP).");

        InvokePrivateMethod(shield, "OnDisable");
        Object.DestroyImmediate(enemyWeaponObj);
        Object.DestroyImmediate(shieldObj);
        Object.DestroyImmediate(playerObj);
    }

    [Test]
    public void Test05_WaveManager_KillingEnemiesDuringStaggeredSpawnDoesNotSoftLock()
    {
        GameObject wmObj = new GameObject("TestWaveManager");
        WaveManager wm = wmObj.AddComponent<WaveManager>();

        GameObject dummyEnemyPrefab = new GameObject("DummyEnemyPrefab");
        dummyEnemyPrefab.AddComponent<EnemyHealth>();
        GameObject spawnPointObj = new GameObject("DummySpawnPoint");

        wm.enemyPrefabs = new[] { dummyEnemyPrefab };
        wm.spawnPoints = new[] { spawnPointObj.transform };
        wm.baseEnemiesPerWave = 3;

        int waveEndCount = 0;
        wm.OnWaveEnd.AddListener(() => waveEndCount++);

        SetPrivateField(wm, "currentWave", 1);
        SetPrivateField(wm, "isWaveActive", true);
        SetPrivateField(wm, "isTransitioningWave", false);
        SetPrivateField(wm, "enemiesRemainingToSpawn", 3);
        SetPrivateField(wm, "enemiesAlive", 0);

        GameObject e1 = wm.SpawnEnemy();
        SetPrivateField(wm, "enemiesRemainingToSpawn", 2);

        wm.EnemyDefeated();

        Assert.AreEqual(0, waveEndCount, "Wave must not end while enemiesRemainingToSpawn > 0.");
        Assert.IsTrue(wm.IsWaveActive, "Wave must remain active while more enemies are queued to spawn.");

        GameObject e2 = wm.SpawnEnemy();
        SetPrivateField(wm, "enemiesRemainingToSpawn", 1);
        GameObject e3 = wm.SpawnEnemy();
        SetPrivateField(wm, "enemiesRemainingToSpawn", 0);

        Assert.AreEqual(2, wm.EnemiesAlive);

        wm.EnemyDefeated();
        Assert.AreEqual(0, waveEndCount, "Wave must not end while 1 enemy is still alive.");

        wm.EnemyDefeated();
        Assert.AreEqual(1, waveEndCount, "Wave must complete cleanly once all spawned enemies are defeated.");
        Assert.AreEqual(3, wm.TotalKills);

        wm.StopWaves();
        if (e1 != null) Object.DestroyImmediate(e1);
        if (e2 != null) Object.DestroyImmediate(e2);
        if (e3 != null) Object.DestroyImmediate(e3);
        Object.DestroyImmediate(spawnPointObj);
        Object.DestroyImmediate(dummyEnemyPrefab);
        Object.DestroyImmediate(wmObj);
    }

    [Test]
    public void Test06_PlayerHealthAndGameManager_FatalDamageTriggersGameOver()
    {
        GameObject playerObj = new GameObject("TestPlayer");
        PlayerHealth playerHealth = playerObj.AddComponent<PlayerHealth>();
        playerHealth.maxHealth = 100f;
        InvokePrivateMethod(playerHealth, "Awake");

        GameObject gmObj = new GameObject("TestGameManager");
        WaveManager wm = gmObj.AddComponent<WaveManager>();
        GameManager gm = gmObj.AddComponent<GameManager>();
        GameObject gameOverCanvas = new GameObject("GameOverCanvas");

        gm.playerHealth = playerHealth;
        gm.waveManager = wm;
        gm.gameOverCanvas = gameOverCanvas;

        InvokePrivateMethod(gm, "Awake");
        InvokePrivateMethod(gm, "Start");

        Assert.IsFalse(gm.IsGameOver);
        Assert.IsFalse(gameOverCanvas.activeSelf);

        playerHealth.TakeDamage(100f);

        Assert.AreEqual(0f, playerHealth.currentHealth);
        Assert.IsTrue(gm.IsGameOver, "Fatal damage to PlayerHealth must trigger GameManager.GameOver().");
        Assert.IsTrue(gameOverCanvas.activeSelf, "GameOverCanvas must be activated on player death.");

        Object.DestroyImmediate(gameOverCanvas);
        Object.DestroyImmediate(gmObj);
        Object.DestroyImmediate(playerObj);
    }

    [Test]
    public void Test07_MetaQuest2ProjectSettings_ConfiguredCorrectly()
    {
        Assert.AreEqual(ColorSpace.Linear, PlayerSettings.colorSpace, "Quest 2 requires Linear color space.");
        Assert.AreEqual(StereoRenderingPath.Instancing, PlayerSettings.stereoRenderingPath, "Single-Pass Instanced rendering should be enabled.");
        Assert.GreaterOrEqual((int)PlayerSettings.Android.minSdkVersion, 29, "Quest 2 OpenXR requires Android Min SDK >= 29.");
        Assert.AreEqual(AndroidArchitecture.ARM64, PlayerSettings.Android.targetArchitectures, "Quest 2 requires ARM64 architecture.");
        Assert.AreEqual("Enemy", LayerMask.LayerToName(8), "Layer 8 must be configured as 'Enemy'.");
        Assert.AreEqual("Weapon", LayerMask.LayerToName(9), "Layer 9 must be configured as 'Weapon'.");
        Assert.AreEqual("Player", LayerMask.LayerToName(10), "Layer 10 must be configured as 'Player'.");
    }

    [Test]
    public void Test08_Quest2Controllers_SelectActivateHapticsAndTrackingConfigured()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/VikingArena.unity", OpenSceneMode.Single);

        XROrigin xrOrigin = Object.FindAnyObjectByType<XROrigin>();
        Assert.IsNotNull(xrOrigin);

        TrackedPoseDriver hmdTpd = xrOrigin.Camera.GetComponent<TrackedPoseDriver>();
        Assert.IsNotNull(hmdTpd, "Main Camera must have TrackedPoseDriver.");
        Assert.IsNotNull(hmdTpd.positionInput.action, "HMD position action must be assigned.");
        Assert.Greater(hmdTpd.positionInput.action.bindings.Count, 0, "HMD position action must have bindings.");
        Assert.IsNotNull(hmdTpd.rotationInput.action, "HMD rotation action must be assigned.");
        Assert.Greater(hmdTpd.rotationInput.action.bindings.Count, 0, "HMD rotation action must have bindings.");

        XRDirectInteractor[] interactors = xrOrigin.GetComponentsInChildren<XRDirectInteractor>(true);
        Assert.AreEqual(2, interactors.Length);

        foreach (XRDirectInteractor interactor in interactors)
        {
            TrackedPoseDriver controllerTpd = interactor.GetComponent<TrackedPoseDriver>();
            Assert.IsNotNull(controllerTpd, $"{interactor.name} must have TrackedPoseDriver.");
            Assert.Greater(controllerTpd.positionInput.action.bindings.Count, 0);
            Assert.Greater(controllerTpd.rotationInput.action.bindings.Count, 0);

            Assert.AreEqual(XRInputButtonReader.InputSourceMode.InputAction, interactor.selectInput.inputSourceMode,
                $"{interactor.name} selectInput must use direct InputAction mode.");
            Assert.IsNotNull(interactor.selectInput.inputActionPerformed, $"{interactor.name} selectInput action must be assigned.");
            Assert.Greater(interactor.selectInput.inputActionPerformed.bindings.Count, 0, $"{interactor.name} selectInput must bind to gripPressed.");

            Assert.AreEqual(XRInputButtonReader.InputSourceMode.InputAction, interactor.activateInput.inputSourceMode,
                $"{interactor.name} activateInput must use direct InputAction mode.");
            Assert.IsNotNull(interactor.activateInput.inputActionPerformed);
            Assert.Greater(interactor.activateInput.inputActionPerformed.bindings.Count, 0);

            HapticImpulsePlayer hapticPlayer = interactor.GetComponent<HapticImpulsePlayer>();
            Assert.IsNotNull(hapticPlayer, $"{interactor.name} must have HapticImpulsePlayer for Quest 2 haptics.");
            Assert.AreEqual(XRInputHapticImpulseProvider.InputSourceMode.InputAction, hapticPlayer.hapticOutput.inputSourceMode);
            Assert.IsNotNull(hapticPlayer.hapticOutput.inputAction);
            Assert.Greater(hapticPlayer.hapticOutput.inputAction.bindings.Count, 0);
        }
    }

    [Test]
    public void Test09_Weapons_ReachableAndHaveValidAttachTransforms()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/VikingArena.unity", OpenSceneMode.Single);

        XROrigin xrOrigin = Object.FindAnyObjectByType<XROrigin>();
        Vector3 playerOrigin = xrOrigin.transform.position;

        Weapon[] weapons = new Weapon[]
        {
            Object.FindAnyObjectByType<Sword>(),
            Object.FindAnyObjectByType<Axe>(),
            Object.FindAnyObjectByType<Shield>()
        };

        foreach (Weapon w in weapons)
        {
            Assert.IsNotNull(w);
            Vector2 flatPlayer = new Vector2(playerOrigin.x, playerOrigin.z);
            Vector2 flatWeapon = new Vector2(w.transform.position.x, w.transform.position.z);
            float horizontalReachDistance = Vector2.Distance(flatPlayer, flatWeapon);

            Assert.LessOrEqual(horizontalReachDistance, 0.72f,
                $"{w.name} horizontal distance ({horizontalReachDistance:F2}m) must be within comfortable Quest 2 arm reach (<= 0.72m).");
            Assert.GreaterOrEqual(w.transform.position.y, 0.75f, $"{w.name} height must be above waist level.");
            Assert.LessOrEqual(w.transform.position.y, 1.15f, $"{w.name} height must be below shoulder level.");

            XRGrabInteractable grab = w.GetComponent<XRGrabInteractable>();
            Assert.IsNotNull(grab);
            Assert.IsNotNull(grab.attachTransform, $"{w.name} must have an explicit attachTransform for ergonomic Quest 2 controller grip.");
        }

        Assert.IsTrue(Physics.GetIgnoreLayerCollision(9, 10),
            "Weapon layer (9) and Player layer (10) collision must be ignored so the player body collider never pushes held weapons.");
    }

    [Test]
    public void Test10_OpenXRAndXRManagement_ConfiguredForStandaloneAndAndroid()
    {
        XRGeneralSettingsPerBuildTarget perBuildTarget = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>("Assets/XR/XRGeneralSettingsPerBuildTarget.asset");
        Assert.IsNotNull(perBuildTarget, "XRGeneralSettingsPerBuildTarget.asset must exist.");

        BuildTargetGroup[] groups = new[] { BuildTargetGroup.Standalone, BuildTargetGroup.Android };
        foreach (BuildTargetGroup group in groups)
        {
            XRGeneralSettings general = perBuildTarget.SettingsForBuildTarget(group);
            Assert.IsNotNull(general, $"XRGeneralSettings must exist for {group}.");
            Assert.IsTrue(general.InitManagerOnStart, $"InitManagerOnStart must be true for {group}.");
            Assert.IsNotNull(general.Manager, $"XRManagerSettings must exist for {group}.");
            Assert.Greater(general.Manager.activeLoaders.Count, 0, $"OpenXRLoader must be assigned in activeLoaders for {group}.");

            OpenXRSettings openXr = OpenXRSettings.GetSettingsForBuildTargetGroup(group);
            Assert.IsNotNull(openXr, $"OpenXRSettings must exist for {group}.");
            Assert.AreEqual(OpenXRSettings.RenderMode.SinglePassInstanced, openXr.renderMode);

            OculusTouchControllerProfile touchProfile = openXr.GetFeature<OculusTouchControllerProfile>();
            Assert.IsNotNull(touchProfile, $"OculusTouchControllerProfile must exist for {group}.");
            Assert.IsTrue(touchProfile.enabled, $"OculusTouchControllerProfile must be enabled for {group}.");
        }

        OpenXRSettings androidOpenXr = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
        MetaQuestFeature metaQuest = androidOpenXr.GetFeature<MetaQuestFeature>();
        Assert.IsNotNull(metaQuest, "MetaQuestFeature must exist on Android OpenXRSettings.");
        Assert.IsTrue(metaQuest.enabled, "MetaQuestFeature must be enabled on Android OpenXRSettings.");
    }

    private static void InvokePrivateMethod(object target, string methodName, params object[] args)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.IsNotNull(method, $"Method '{methodName}' not found on {target.GetType().Name}");
        method.Invoke(target, args);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.IsNotNull(field, $"Field '{fieldName}' not found on {target.GetType().Name}");
        field.SetValue(target, value);
    }
}
