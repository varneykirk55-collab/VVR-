# Quest 2 build, install and log capture

## 1. Build the APK (Unity 6000.6.6f1 with Android Build Support)

1. Open the project, let it import, and confirm the Console has no compile errors.
2. Optional: **Window → General → Test Runner**, then run **EditMode** and **PlayMode** tests.
3. Menu **VVR → Build Quest 2 APK**. The APK is written to `Builds/VVR-Arena-Quest2.apk`.

## 2. Install

With the headset connected by USB and developer mode on:

```powershell
adb devices
adb install -r Builds\VVR-Arena-Quest2.apk
```

(SideQuest or Meta Quest Developer Hub can also drag-and-drop install the same file.)

## 3. Capture a log while you play

Start this **before** launching the game, then launch **VVR-Arena** from *Library → Unknown Sources*:

```powershell
adb logcat -c
adb logcat -v time -s Unity > vvr_log.txt
```

Play, then press `Ctrl+C` in the window. Send back `vvr_log.txt`. To read only the game's breadcrumbs:

```powershell
findstr VVR_DIAG vvr_log.txt
```

## 4. What to check in the headset

| # | Check | Matching log lines |
|---|-------|--------------------|
| 1 | Game launches, arena visible | `VVR_DIAG start`, `VVR_DIAG xr ready ...` |
| 2 | Both controllers tracked (blue hand blocks follow) | `VVR_DIAG input present ... LeftHand / RightHand` |
| 3 | Grip picks up sword / axe / shield | `VVR_DIAG grab weapon=Sword ...` |
| 4 | Enemies appear ~3 s in and walk toward you, **no freeze** | `VVR_DIAG spawn wave=1 ... pooled=True`, `enemy Start`, steady `beat` lines |
| 5 | Swinging a weapon into an enemy damages / kills it | `enemy hit ...`, `enemy died ...` |
| 6 | Enemies swing their axe and hurt you; a held shield blocks most damage | `player hit damage=...` |
| 7 | Next wave starts after a wave is cleared | `spawn wave=2 ...` |
| 8 | On death, "GAME OVER" shows and the arena reloads after 8 s | `game over ...`, `restart scene` |

## 5. If it freezes again

Keep logcat running for ~10 s after the freeze, then send `vvr_log.txt`. The log tells us which kind of freeze it is:

- `VVR_DIAG HANG main thread has not completed a frame for N ms; lastMarker=...` means the game's main thread is stuck. The `lastMarker` value names the step it was in.
- If `VVR_DIAG beat ...` lines keep arriving every 2 s while the headset image is frozen, the game logic is still running. The stall is then in rendering or the XR compositor.
- `VVR_DIAG error ...` reports the first exceptions. Any `AndroidRuntime`, `CRASH` or `DEBUG` lines near the end point to a native crash. To capture everything:
  `adb logcat -v time > vvr_full_log.txt`
