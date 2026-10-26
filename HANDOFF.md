# HANDOFF: Enemy Spawn Freeze Investigation & Partial Fix (Claude Sonnet -> Next Agent)

**Project:** `VVR-Arena` (Unity `6000.6.6f1`, OpenXR + XR Interaction Toolkit `3.6.1`)  
**Target Hardware:** Meta Quest 2 (Standalone Android VR, ARM64, Vulkan)  
**Branch:** `argon-complete-repair`  
**Status at Handoff:** Partial diagnostic + `TimeManager` mitigation applied by Claude Sonnet before quota exhaustion; **APK not yet rebuilt with these changes and root cause not yet fully resolved.**

---

## 1. What the Freeze Bug Appears to Be & Where It Happens

### Observed Symptom on Meta Quest 2 Hardware
- The APK (`Builds/VVR-Arena-Quest2.apk`, built prior to Sonnet's changes) launches cleanly on the Quest 2.
- For the first ~3 seconds (`WaveManager.timeBetweenWaves = 3f`), head tracking, smooth locomotion, snap turn, and grabbing the sword/axe/shield work smoothly.
- At `t = 3.0s`, `WaveManager.Update()` transitions from `waitingForWave` into `StartNextWave()` -> `SpawnEnemy()`.
- **Immediately as the first enemy spawns, the entire application freezes on the Quest 2**, locking head/hand tracking and controller input.

### Where It Happens & Likely Root Causes
Based on code inspection and Sonnet's PlayMode diagnostic run, the freeze triggers right at the boundary of `WaveManager.SpawnEnemy()` (`Assets/Scripts/Managers/WaveManager.cs:112-122`) and the first frame of `VikingEnemy` initialization (`Assets/Prefabs/VikingEnemy.prefab`, `Assets/Scripts/Enemy/EnemyAI.cs`):

1. **Cold-Start Prefab Instantiation & Synchronous Shader/Material/PSO Upload (`WaveManager.cs` -> `VikingEnemy.prefab`):**
   - `VikingEnemy.prefab` is **never present in `VikingArena.unity` at scene load**—it is only referenced on `WaveManager.enemyPrefabs` and instantiated dynamically at `t = 3.0s`.
   - When `Instantiate(prefab, spawnPoint.position, spawnPoint.rotation)` runs at `t = 3.0s`, Unity must synchronously initialize and upload unseen materials/shaders (`EnemyTunic.mat`, `EnemySkin.mat`, `BloodRed.mat` on `ParticleSystemRenderer`), `Animator` (`EnemyAnimatorController.controller`), and `NavMeshAgent` while OpenXR is actively rendering frames.
   - **Evidence from Sonnet's PlayMode test:** Even in the desktop Windows Editor (`VVRPlayModeSpawnTests.cs`), the initial scene/wave spawn frame spiked to **`836.1 ms`**, whereas subsequent forced burst spawns of 30 enemies only took `40.3 ms` max frame time. On Quest 2 (Adreno 650 / Vulkan + OpenXR compositor), a ~1+ second main-thread stall on first instantiation can stall the OpenXR frame loop or trigger a physics catch-up spiral.

2. **Physics Catch-Up "Spiral of Death" (`ProjectSettings/TimeManager.asset`):**
   - `Fixed Timestep` is configured for 90 Hz VR (`0.01111111s`), and `Maximum Allowed Timestep` was previously set to `0.11111111s` (allowing up to **10 consecutive `FixedUpdate` + physics + XRI velocity-tracked steps** in a single frame after any frame hitch).
   - When the cold-start `Instantiate(VikingEnemy)` frame stalls for hundreds of milliseconds, the very next frame executes 10 back-to-back physics & XR kinematic steps, compounding the main-thread stall.

3. **Compound Collider Hierarchy + Child Kinematic `Rigidbody` on `VikingEnemy.prefab` (`Assets/Prefabs/VikingEnemy.prefab` & `Assets/Scripts/Editor/VVRProjectSetup.cs`):**
   - `VikingEnemy` root has a non-trigger `CapsuleCollider` on Layer 8 (`Enemy`), **plus** child `Torso` (`CapsuleCollider`) and `Head` (`SphereCollider`) non-trigger colliders on Layer 8, **plus** child `EnemyWeapon` which has its own kinematic `Rigidbody` (`isKinematic = true`, `useGravity = false`) and a trigger `BoxCollider` (`EnemyWeapon.cs`).
   - Having multiple non-trigger colliders without a root `Rigidbody` alongside a child kinematic `Rigidbody` with a trigger collider inside the same prefab can cause unnecessary PhysX broadphase/compound regeneration or self-overlap `OnTriggerEnter` checks during instantiation.

4. **Unthrottled Per-Frame Pathfinding in `EnemyAI.Update()` (`Assets/Scripts/Enemy/EnemyAI.cs:71-78`):**
   - Every active `EnemyAI` calls `agent.SetDestination(target.position)` on **every single `Update()` frame** (90 times per second per enemy on Quest 2) whenever `distanceToTarget > attackRange`.
   - Furthermore, `target` is resolved in `EnemyAI.Start()` via `GameObject.FindGameObjectWithTag("Player")` (which finds `XR Origin`). Since `XR Origin`'s root position or camera offset updates continuously, `SetDestination` recalculates path requests every frame.

---

## 2. Every File Claude Sonnet Changed or Created

### Modified Tracked Files

| File | Summary of Changes |
| :--- | :--- |
| [`ProjectSettings/TimeManager.asset`](file:///C:/Users/aresg/.gemini/antigravity/scratch/VVR-Arena/ProjectSettings/TimeManager.asset) | Lowered `Maximum Allowed Timestep` from `0.11111111` (10x fixed steps at 90 Hz) to `0.033333335` (~3x fixed steps max per frame) so a single hitch during enemy spawn cannot trigger a 10-step physics catch-up spiral. |
| [`Assets/Scripts/Managers/WaveManager.cs`](file:///C:/Users/aresg/.gemini/antigravity/scratch/VVR-Arena/Assets/Scripts/Managers/WaveManager.cs) | Added two `Debug.Log` breadcrumbs in `SpawnEnemy()` immediately before and after `Instantiate(prefab, spawnPoint.position, spawnPoint.rotation)` (`VVR_DIAG spawn begin ...` and `VVR_DIAG spawn instantiated ...`) to pinpoint whether the Quest 2 hangs inside `Instantiate()` itself. |
| [`Assets/Scripts/Enemy/EnemyAI.cs`](file:///C:/Users/aresg/.gemini/antigravity/scratch/VVR-Arena/Assets/Scripts/Enemy/EnemyAI.cs) | Added `private bool loggedFirstUpdate = false;`, a `Debug.Log("VVR_DIAG enemy Start onNavMesh=...")` at the top of `Start()`, and a one-time `Debug.Log("VVR_DIAG enemy first Update ...")` in `Update()` to verify on-device whether `Start()` and `Update()` execute and whether `agent.isOnNavMesh` is true on Quest 2. |
| [`ProjectSettings/ProjectSettings.asset`](file:///C:/Users/aresg/.gemini/antigravity/scratch/VVR-Arena/ProjectSettings/ProjectSettings.asset) | Modified by the Unity Editor during the earlier Android APK build session: added Android platform icon slots, default Android ASTC texture compression format entry, `preloadedAssets` entries for OpenXR settings, and changed `activeInputHandler` from `2` (`Both`) to `1` (`InputSystemPackage` only). |

### Newly Created (Previously Untracked) Files

| File | Summary of Changes |
| :--- | :--- |
| [`Assets/Scripts/Diagnostics/VVRRuntimeDiagnostics.cs`](file:///C:/Users/aresg/.gemini/antigravity/scratch/VVR-Arena/Assets/Scripts/Diagnostics/VVRRuntimeDiagnostics.cs) | Created by Sonnet. Uses `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]` to spawn a persistent `DontDestroyOnLoad` logger that prints `VVR_DIAG start` (timestep, FPS, graphics API, CPU/RAM), `VVR_DIAG spike` whenever `Time.unscaledDeltaTime > 0.1s`, and a 2-second heartbeat (`VVR_DIAG beat`) with frame count, fixed steps, max delta time, current wave, and enemies alive. |
| [`Assets/Tests/PlayMode/VVRPlayModeSpawnTests.cs`](file:///C:/Users/aresg/.gemini/antigravity/scratch/VVR-Arena/Assets/Tests/PlayMode/VVRPlayModeSpawnTests.cs) | Created by Sonnet. PlayMode `[UnityTest]` (`Spawn_RealScene_NavMeshStateAndFrameTimes`) that loads `Assets/Scenes/VikingArena.unity`, logs `NavMesh.CalculateTriangulation()` vertex/index counts, lets Wave 1 run for 12 seconds while recording `maxFrame`, checks `agent.isOnNavMesh` for all spawned enemies, and then force-spawns 30 enemies in a single frame to measure burst frame time. |
| [`Assets/Tests/PlayMode/VVRPlayModeTests.asmdef`](file:///C:/Users/aresg/.gemini/antigravity/scratch/VVR-Arena/Assets/Tests/PlayMode/VVRPlayModeTests.asmdef) | Created by Sonnet. Assembly definition for PlayMode tests referencing `VVR`, `UnityEngine.TestRunner`, `UnityEditor.TestRunner`, `Unity.AI.Navigation`, `Unity.TextMeshPro`, and `UnityEngine.UI`, gated by `"defineConstraints": ["UNITY_INCLUDE_TESTS"]`. |
| [`Assets/XR/Settings/OpenXR Editor Settings.asset`](file:///C:/Users/aresg/.gemini/antigravity/scratch/VVR-Arena/Assets/XR/Settings/OpenXR Editor Settings.asset) | Auto-generated by Unity OpenXR plugin during the Android build session. |

---

## 3. What Looks Finished vs. Unfinished / Half-Done

### Finished
- **On-Device Breadcrumb Instrumentation (`VVR_DIAG`):** `VVRRuntimeDiagnostics.cs`, `WaveManager.cs`, and `EnemyAI.cs` are cleanly instrumented so a single `adb logcat -s Unity:I | findstr VVR_DIAG` session will immediately show the exact last statement executed before the freeze.
- **Physics Timestep Cap (`TimeManager.asset`):** `Maximum Allowed Timestep` is reduced from `0.11111111` to `0.033333335`.
- **Desktop PlayMode Reproduction of Initial Frame Hitch:** Sonnet ran `VVRPlayModeSpawnTests` in an isolated scratch copy (`VVR-Diag`) and confirmed:
  - Baked NavMesh is valid in PlayMode (`vertices=146, indices=186`, all `3/3` Wave 1 enemies report `isOnNavMesh=true`).
  - No managed exceptions or infinite C# loops occurred in Editor PlayMode.
  - However, **initial scene/wave execution produced an `836.1 ms` frame spike**, whereas spawning 30 additional enemies after warm-up only produced a `40.3 ms` frame spike.

### Unfinished / Half-Done
1. **No New APK Built With Sonnet's Changes:**
   - The existing `Builds/VVR-Arena-Quest2.apk` on disk was built at **7:13 PM**, *before* Sonnet edited `TimeManager.asset`, `WaveManager.cs`, `EnemyAI.cs`, and `VVRRuntimeDiagnostics.cs` (7:52 PM – 8:04 PM).
2. **No On-Device `adb logcat` Captured Yet:**
   - No Quest 2 was connected via USB (`adb devices` returned empty) during Sonnet's session, so the on-device freeze has not yet been captured with `adb logcat`.
3. **No Pre-Warming / Pooling or Enemy Prefab/AI Fixes Applied Yet:**
   - `WaveManager` still cold-instantiates `VikingEnemy.prefab` at `t = 3.0s` without pre-warming or object pooling.
   - `EnemyAI.Update()` still calls `agent.SetDestination(target.position)` every frame without throttling.
   - `VikingEnemy.prefab` still has redundant child colliders (`Torso`, `Head`) alongside its root `CapsuleCollider`.
4. **Missing `.meta` Files on New Files:**
   - Because the Unity Editor was open and holding the lockfile while Sonnet created `Assets/Scripts/Diagnostics/` and `Assets/Tests/PlayMode/` from the shell, Unity has not yet generated `.meta` files for `VVRRuntimeDiagnostics.cs`, `VVRPlayModeSpawnTests.cs`, and `VVRPlayModeTests.asmdef`.

---

## 4. Recommended Remaining Steps to Fix the Freeze

1. **Let Unity Refresh `.meta` Files & Fix `VVRPlayModeTests.asmdef`:**
   - Focus/refresh the Unity Editor (or close the open Editor instance and run a quick `-batchmode -quit` pass) so Unity generates `.meta` files for `Assets/Scripts/Diagnostics/VVRRuntimeDiagnostics.cs` and `Assets/Tests/PlayMode/`.
   - **Important:** In `Assets/Tests/PlayMode/VVRPlayModeTests.asmdef`, `references` currently includes `"UnityEditor.TestRunner"` while `"includePlatforms": []` targets all platforms. Either remove `"UnityEditor.TestRunner"` from `VVRPlayModeTests.asmdef` or restrict its platform scope so Development Android builds (`UNITY_INCLUDE_TESTS`) never fail trying to link `UnityEditor.TestRunner` on Android.

2. **Eliminate the Cold-Start Spawn Hitch (Pre-Warm / Object Pool in `WaveManager`):**
   - In `WaveManager.Start()` (during initial scene load, before the user starts moving), pre-instantiate a small pool of `VikingEnemy` instances (or instantiate 1 warm-up instance at an off-screen position, let its shaders/Animator/ParticleSystem initialize, and deactivate/destroy it) so `t = 3.0s` does not trigger synchronous asset/shader/PSO initialization on the Quest 2 main thread.

3. **Throttle `NavMeshAgent.SetDestination` & Guard `isOnNavMesh` in `EnemyAI.cs`:**
   - In `EnemyAI.Start()`, explicitly verify `if (agent != null && !agent.isOnNavMesh)` and call `NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 5f, NavMesh.AllAreas)` + `agent.Warp(hit.position)` before enabling pathfinding.
   - In `EnemyAI.Update()`, throttle `agent.SetDestination(target.position)` to run every `0.2f` seconds (5 Hz) instead of every frame (90 Hz), and target `Camera.main.transform` (or player head XZ position) so the enemy tracks the physical player in room-scale VR.

4. **Clean Up `VikingEnemy.prefab` Colliders:**
   - Remove the redundant non-trigger `CapsuleCollider` on `Torso` and `SphereCollider` on `Head` inside `VikingEnemy.prefab` (keeping the single root `CapsuleCollider` on Layer 8 `Enemy` for weapon hits and `EnemyWeapon`'s trigger `BoxCollider` for attacks).

5. **Rebuild the Quest 2 APK & Verify via `adb logcat`:**
   - Rebuild `Builds/VVR-Arena-Quest2.apk`, install via `adb install -r Builds/VVR-Arena-Quest2.apk`, and monitor:
     ```powershell
     adb logcat -c
     adb logcat -s Unity:I AndroidRuntime:E CRASH:E | findstr "VVR_DIAG Exception Fatal"
     ```
   - Confirm `VVR_DIAG spawn begin` -> `VVR_DIAG spawn instantiated` -> `VVR_DIAG enemy Start` -> `VVR_DIAG enemy first Update` -> continuous `VVR_DIAG beat` heartbeats with no freeze on Quest 2 hardware.

---

## 5. Risks & Things That Look Broken Right Now

- **`VVRPlayModeTests.asmdef` references `UnityEditor.TestRunner` with `"includePlatforms": []`:**
  - While standard Release Android builds exclude assemblies with `"defineConstraints": ["UNITY_INCLUDE_TESTS"]`, any Android build with `BuildOptions.IncludeTestAssemblies` or Editor PlayMode player builds for Android will fail to compile because `UnityEditor.TestRunner` is an Editor-only assembly.
- **`ProjectSettings/ProjectSettings.asset` `activeInputHandler` Changed from `2` (`Both`) to `1` (`InputSystemPackage`):**
  - When the Unity Editor opened/built the project, `activeInputHandler` changed from `2` to `1`. That is actually *good* and clean for Unity 6 + XR Interaction Toolkit 3.x (which uses the new Input System exclusively), but worth noting in the diff.
- **Uncommitted `.meta` Files for `Assets/Scripts/Diagnostics/` and `Assets/Tests/PlayMode/`:**
  - Must be generated by Unity next time the project refreshes so GUIDs stay stable across machines.
- **Leftover Scratch Clone Outside Repo (`C:\Users\aresg\.gemini\antigravity\scratch\VVR-Diag`):**
  - Sonnet created a separate directory `../VVR-Diag` outside this repository to run batchmode PlayMode tests while the Unity Editor had `VVR-Arena` locked. It is not part of this Git repo and can be safely deleted.
