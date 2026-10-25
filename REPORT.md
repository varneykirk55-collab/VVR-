# VVR-Arena | Unity 6000.6.6f1 VR Project Documentation

## 1. Overview
**VVR-Arena** is a Viking VR wave-survival combat project built for **Unity 6 (`6000.6.6f1`)** and configured for **Meta Quest 2 standalone VR** via **OpenXR** and **XR Interaction Toolkit 3.x (`3.6.1`)**.

## 2. Project Architecture & Repaired Systems

### Package & Assembly Configuration
- **`Packages/manifest.json`**: Configured for Unity `6000.6.6f1` using `com.unity.ugui@2.6.0` (replacing the deprecated standalone `com.unity.textmeshpro` package), `com.unity.inputsystem@1.20.0`, `com.unity.xr.interaction.toolkit@3.6.1`, `com.unity.xr.openxr@1.18.0`, `com.unity.xr.management@4.7.0`, `com.unity.xr.core-utils@2.6.0`, `com.unity.ai.navigation@2.0.15`, and `com.unity.test-framework@1.8.0`.
- **`Assets/Scripts/VVR.asmdef`**: Runtime assembly definition referencing `Unity.InputSystem`, `Unity.TextMeshPro`, `UnityEngine.UI`, `Unity.XR.Interaction.Toolkit`, `Unity.XR.CoreUtils`, `Unity.AI.Navigation`, `Unity.XR.OpenXR`, and `Unity.XR.Management`.

### XR Interaction Toolkit 3.x & Gameplay Scripts (`Assets/Scripts/`)
1. **`Weapon.cs`**:
   - Migrated to `UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable` and `UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInputInteractor`.
   - Replaced obsolete `XRBaseController.SendHapticImpulse` (`.xrController`) with `XRBaseInputInteractor.SendHapticImpulse(intensity, duration)`.
   - Added `GetVelocityReferenceTransform()` so subclasses measure swing velocity at the physical strike point rather than the grip pivot.
   - Enforced `CollisionDetectionMode.ContinuousDynamic` on weapon rigidbodies.
2. **`Sword.cs` & `Axe.cs`**:
   - Override `GetVelocityReferenceTransform()` to track `damagePoint` (blade tip) and `axeHead` respectively, ensuring rotational wrist swings register accurate linear strike velocity.
3. **`Shield.cs`, `EnemyWeapon.cs`, & `PlayerHealth.cs`**:
   - Added active shield tracking (`Shield.ActiveShields` / `Shield.GetHeldShield()`) and `PlayerHealth.activeShield` reference so shield blocking works when `XRGrabInteractable` unparents the shield or re-parents it to a controller interactor.
   - Fixed directional block cone math in `Shield.TryBlock` and added direct physical shield-trigger interception in `EnemyWeapon.OnTriggerEnter`.
4. **`WaveManager.cs` & `GameManager.cs`**:
   - Fixed the staggered wave-spawning race condition by tracking both `enemiesRemainingToSpawn` and `enemiesAlive`, and invoking `CheckWaveCompletion()` both when an enemy dies and when the spawn loop completes.
   - Connected `PlayerHealth.OnDeath` to `GameManager.GameOver()` and `WaveManager.StopWaves()`.
5. **`EnemyAI.cs` & `EnemyHealth.cs`**:
   - Computes melee distance on the horizontal XZ plane so VR headset elevation does not prevent enemies from reaching attack range.
   - Drives `EnemyWeapon.EnableDamage()` and `DisableDamage()` during attack swings via `AttackDamageWindowRoutine()`.

### Playable Vertical Slice (`Assets/Scenes/VikingArena.unity`)
- **Environment**: Octagonal Viking arena floor with baked `NavMeshSurface`, perimeter stone walls, timber pillars with glowing torches, and a wooden weapon rack in front of the player.
- **VR Rig**: `XR Origin (VR)` tagged `Player` with `XROrigin`, `XRInteractionManager`, `PlayerHealth`, `Main Camera` (`TrackedPoseDriver`), and `Left Hand Controller` / `Right Hand Controller` (`XRDirectInteractor`, `HandAnimator`).
- **Prefabs (`Assets/Prefabs/`)**: `Sword.prefab`, `Axe.prefab`, `Shield.prefab`, and `VikingEnemy.prefab`.
- **Automated Verification (`Assets/Tests/Editor/VVRVerificationTests.cs`)**: 7 NUnit EditMode tests verifying scene/prefab integrity, component wiring, blade-tip velocity calculation, shield blocking, mid-spawn wave progression, player death/game-over handling, and Meta Quest 2 project settings.
