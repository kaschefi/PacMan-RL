# Pac-Man RL Environment Preparation: Changelog & Architecture Documentation

This document records all architectural refactorings and code modifications made to transform the standalone Pac-Man game into a high-throughput, multi-agent reinforcement learning (MARL) environment compatible with **Unity ML-Agents**.

---

## Table of Contents
1. [Overview & RL Transition Goals](#1-overview--rl-transition-goals)
2. [Step 1: Zero-Delay Reset & Single-Life RL Episodes](#2-step-1-zero-delay-reset--single-life-rl-episodes)
3. [Step 2: Fixing the "3 Deadly Sins" of Parallel Prefabs](#3-step-2-fixing-the-3-deadly-sins-of-parallel-prefabs)
   - [Sin #1: Singletons & Global Searches (State Contamination)](#sin-1-singletons--global-searches-state-contamination)
   - [Sin #2: World Coordinates (Cross-Maze Teleportation)](#sin-2-world-coordinates-cross-maze-teleportation)
   - [Sin #3: Sensor & Raycast Bleed (Spatial Isolation)](#sin-3-sensor--raycast-bleed-spatial-isolation)
4. [New Components & Editor Tooling](#4-new-components--editor-tooling)
5. [Complete File Modification Matrix](#5-complete-file-modification-matrix)
6. [Testing & Verification Guide](#6-testing--verification-guide)

---

## 1. Overview & RL Transition Goals

In traditional human play, a Pac-Man game relies on:
- Global singletons (`GameHandler.Instance`, `ScoreHandler.Instance`).
- Three lives per playthrough with 2–4 second dramatic death animations and music delays.
- Objects being destroyed (`Destroy(gameObject)`) upon collection.
- Absolute world positions (`transform.position`).

In **Reinforcement Learning**, thousands of steps must execute per second across **16+ parallel maze instances** duplicated into the same Unity physics scene. To achieve this, the codebase was modified to satisfy:
1. **Zero Wait Times**: Instant 0.0s episode reset upon death or maze completion.
2. **Object Pooling / Re-use**: Pellets and ghosts deactivate/reactivate rather than allocating/destroying GameObjects.
3. **Strict Environmental Isolation**: Each maze operates as an autonomous container with zero memory leaks, cross-singleton calls, or sensor overlap.

---

## 2. Step 1: Zero-Delay Reset & Single-Life RL Episodes

*Implemented in commit `abb29bf`.*

1. **Eliminated Game Timers & Delays**:
   - Removed 3-second death wait timer (`Invoke(nameof(ResetGhostsAndPlayer), 3f)`).
   - Removed 4-second game start intro music wait in `GameStartHandler`.
   - Removed 0.6-second ghost eaten pause (`WaitForSecondsRealtime(GhostPauseEatenTime)`).
   - Removed 6-second level transition delay upon clearing all pellets.
2. **Single-Life Policy (1 Death = 1 Episode)**:
   - Disabled the 3-lives system in `PlayerLife.cs`; life is clamped to 1.
   - Deactivated the Hearts UI display.
   - Pac-Man death immediately triggers `GameHandler.OnPlayerDied` and resets the board.
3. **Instant Board Reset (`GameHandler.ResetBoard()`)**:
   - Pellets are deactivated (`SetActive(false)`) rather than destroyed upon collection.
   - Added `ResetPellet()` on `CollectiblePacGum` and `CollectiblePower` to instantly restore them on episode reset without scene reloading.
   - Added events: `OnPlayerDied` and `OnBoardCleared` for ML-Agents `Agent.EndEpisode()` and reward hooks.

---

## 3. Step 2: Fixing the "3 Deadly Sins" of Parallel Prefabs

When 16 Pac-Man mazes are duplicated into the same scene, they share the same physics scene and memory space. The following three critical traps were resolved:

### Sin #1: Singletons & Global Searches (State Contamination)

#### The Problem
- Calling `GameHandler.Instance` or `ScoreHandler.Instance` meant eating a pellet in Maze #14 would award score to Maze #1, or dying in Maze #5 would reset Maze #1.
- `Awake()` methods containing `if (Instance != null && Instance != this) Destroy(this);` would cause mazes 2 through 16 to destroy their own managers upon loading.
- `FindObjectsOfType<CollectiblePacGum>()` or `FindObjectOfType<PlayerController>()` grabbed entities across all 16 mazes indiscriminately.

#### The Fix
- **Removed `Destroy(this)`** from all managers (`GameHandler`, `ScoreHandler`, `GameStartHandler`, `CollectibleAudioHandler`, `MusicHandler`).
- **Eliminated `FindObjectsOfType`**: Managers search strictly within their local parent hierarchy:
  ```csharp
  Transform searchRoot = transform.parent != null ? transform.parent : transform;
  _ghosts = searchRoot.GetComponentsInChildren<GhostAiMovement>(true);
  _player = searchRoot.GetComponentInChildren<PlayerController>(true);
  _allPacGums = searchRoot.GetComponentsInChildren<CollectiblePacGum>(true);
  ```
  *(A fallback to scene roots is retained only when `transform.parent == null` to support single-maze legacy scenes).*
- **Hierarchical Area Container Resolution (`PacManArea.FindAreaRoot`)**:
  - In the original scene hierarchy, `GameHandler` is attached to `EventSystem` and `ScoreHandler` to `CanvasUi` (both are siblings under `trainingArea`, not parents).
  - Calling `GetComponentInParent<GameHandler>` from ghosts or pellets returned `null`, which previously caused a silent fallback to `GameHandler.Instance`.
  - Added `PacManArea.FindAreaRoot(transform)` which traverses up to the local maze container (`trainingArea`) and resolves `GameHandler` and `ScoreHandler` via `searchRoot.GetComponentInChildren<T>(true)`.
  - `GameHandler.EnsureInitialized()` and `PacManArea.InitializeArea()` proactively push local dependencies directly into all child pellets and ghosts upon area start.
  - Added `if (gameHandler == null) ResolveDependencies();` guard in `GhostAiMovement.OnTriggerEnter2D`.

---

### Sin #2: World Coordinates (Cross-Maze Teleportation)

#### The Problem
If Maze #2 is located at `(X: 50, Y: 0, Z: 0)` and Pac-Man resets to `transform.position = _spawnPosition;` where `_spawnPosition` was recorded as `(0.5, 0, 0)`, Pac-Man in Maze #2 immediately teleports across the screen into Maze #1.

#### The Fix
- **Switched Spawning to `transform.localPosition`**:
  - `PlayerController.cs`: Records `_spawnLocalPosition = transform.localPosition;`.
  - `GhostAiMovement.cs`: Records `_spawnLocalPoint = transform.localPosition;`.
  - `PlayerLife.cs`: Records `_respawnLocalPoint = transform.localPosition;`.
- **Reset Logic & Physics Synchronization**:
  On `Reset()`:
  ```csharp
  transform.localPosition = _spawnLocalPosition;
  if (_rigidbody2D != null)
  {
      _rigidbody2D.velocity = Vector2.zero;
      _rigidbody2D.position = transform.position; // Syncs physics engine immediately
  }
  NextDestination = (Vector2)transform.position + initDirection;
  ```
- **Teleporters (`Teleporter.cs`)**:
  - Added null guards and synchronized `Rigidbody2D.position` when teleporting across tunnel endpoints.

---

### Sin #3: Sensor & Raycast Bleed (Spatial Isolation)

#### The Problem
The Pac-Man maze is approximately **32 units wide by 38 units tall**. If two duplicated mazes are spaced only 2 to 10 units apart, Pac-Man's RL sensors (`RayPerceptionSensor2D` or 25-unit raycasts) will shoot out of the tunnel and detect ghosts or walls in the neighboring maze.

#### The Fix
- **50-Unit Grid Spacing Standard**:
  - Minimum spacing between parallel maze centers is set to **50 units** along the X and Y axes.
  - At 50 units center-to-center and 32 units width, there is an **18-unit buffer of empty space** separating adjacent mazes.
  - Raycasts clamped to 25 units can physically never touch an adjacent maze.
- **`PacManArea` Container**:
  - Provides a visual Scene View Gizmo wireframe (`32 x 38`) to visually inspect boundary margins.

---

## 4. New Components & Editor Tooling

### 1. `PacManArea.cs` (`Assets/Scripts/GameHandler/PacManArea.cs`)
Top-level container component attached to the root of each duplicated maze:
- Holds local references to `GameHandler`, `PlayerController`, `ScoreHandler`, and `GhostAiMovement[]`.
- `InitializeArea()`: Forces all child pellets and ghosts to resolve local references.
- `ResetArea()`: Standard entry point for ML-Agents `Agent.OnEpisodeBegin()` to reset the local environment in 0.0 seconds.
- `OnDrawGizmosSelected()`: Displays a wireframe bounding box of the maze footprint.

### 2. `MazeAreaGridGenerator.cs` (`Assets/Scripts/Editor/MazeAreaGridGenerator.cs`)
Editor window accessible via **`Tools > PacMan RL > Generate Parallel Mazes Grid`**:
- Takes any source maze GameObject or Prefab.
- Instantiates a configurable $N \times M$ grid (default: 4x4 = 16 mazes) with 50-unit spacing.
- Automatically organizes instances under a `TrainingAreas_16x` parent object with full Undo support.
- **Auto-Camera Framing**: Includes **`Tools > PacMan RL > Frame Camera to All Mazes`** (and a button in the window) to automatically compute bounding boxes of all active mazes and adjust `Main Camera` position and orthographic size so all mazes fit cleanly side-by-side in the Game window.
- **Single Arena Preservation & Mode Switcher**:
  - Automatically disables (never deletes) the original template `trainingArea` so you never lose the original single arena.
  - Adds **`Mode: Single Arena`** and **`Mode: Parallel Grid`** buttons directly in the tool window to seamlessly toggle between testing a single arena and testing the parallel grid with automatic camera framing and input rebinding.

### 3. `ParallelPrefabTester.cs` (`Assets/Scripts/Editor/ParallelPrefabTester.cs`)
Automated diagnostic tool accessible via **`Tools > PacMan RL > Run Parallel Prefab Verification Test`**:
- Audits the scene across 5 automated checks:
  1. Multi-instance manager coexistence without self-destruction.
  2. Ghost chase targets scoped to local player.
  3. Blue Ghost scoped to local Red Ghost.
  4. Local coordinate spawn retention on `Reset()` (zero teleportation).
  5. Minimum 40–50 unit spacing between all parallel instances.

---

## 5. Complete File Modification Matrix

| File | Type | Changes Made |
| :--- | :--- | :--- |
| [`PacManArea.cs`](file:///c:/Unity/PacMan-RL/Assets/Scripts/GameHandler/PacManArea.cs) | **New** | Root area manager, component resolution, `ResetArea()` interface, bounding gizmo. |
| [`MazeAreaGridGenerator.cs`](file:///c:/Unity/PacMan-RL/Assets/Scripts/Editor/MazeAreaGridGenerator.cs) | **New** | Editor window for 16x parallel grid generation with 50-unit spacing. |
| [`ParallelPrefabTester.cs`](file:///c:/Unity/PacMan-RL/Assets/Scripts/Editor/ParallelPrefabTester.cs) | **New** | 1-Click diagnostic verification window auditing the 3 Deadly Sins. |
| [`GameHandler.cs`](file:///c:/Unity/PacMan-RL/Assets/Scripts/GameHandler/GameHandler.cs) | Modified | Removed `FindObjectsOfType`, removed `Destroy(this)`, localized child resolution, resolved CS1061 namespace shadowing via `global::ScoreHandler`, local `_playerLife` and `_scoreHandler` in resets. |
| [`ScoreHandler.cs`](file:///c:/Unity/PacMan-RL/Assets/Scripts/ScoreHandler/ScoreHandler.cs) | Modified | Removed `Destroy(this)`, added null-guards for headless mode, exposed `CurrentScore`. |
| [`CollectiblePacGum.cs`](file:///c:/Unity/PacMan-RL/Assets/Scripts/Collectibles/CollectiblePacGum.cs) | Modified | Local `gameHandler` and `scoreHandler` resolution via `GetComponentInParent`. |
| [`CollectiblePower.cs`](file:///c:/Unity/PacMan-RL/Assets/Scripts/Collectibles/CollectiblePower.cs) | Modified | Local `gameHandler` and `scoreHandler` resolution via `GetComponentInParent`. |
| [`GhostAiMovement.cs`](file:///c:/Unity/PacMan-RL/Assets/Scripts/Ghosts/GhostAiMovement.cs) | Modified | Local `_spawnLocalPoint`, local target resolution, Rigidbody2D velocity zeroing. |
| [`BlueGhostAiMovement.cs`](file:///c:/Unity/PacMan-RL/Assets/Scripts/Ghosts/BlueGhostAiMovement.cs) | Modified | Auto-binds local `RedGhost` reference in local maze; null guards. |
| [`PinkGhostAiMovement.cs`](file:///c:/Unity/PacMan-RL/Assets/Scripts/Ghosts/PinkGhostAiMovement.cs) | Modified | Added null guard for `chaseModeTarget`. |
| [`RedGhostAiMovement.cs`](file:///c:/Unity/PacMan-RL/Assets/Scripts/Ghosts/RedGhostAiMovement.cs) | Modified | Added null guard for `chaseModeTarget`. |
| [`OrangeGhostAiMovement.cs`](file:///c:/Unity/PacMan-RL/Assets/Scripts/Ghosts/OrangeGhostAiMovement.cs) | Modified | Added null guards for `chaseModeTarget` and `scatterModeTarget`. |
| [`PlayerController.cs`](file:///c:/Unity/PacMan-RL/Assets/Scripts/Player/PlayerController.cs) | Modified | Local `_spawnLocalPosition`, reset via `transform.localPosition`, Rigidbody2D position sync and velocity zeroing. |
| [`PlayerLife.cs`](file:///c:/Unity/PacMan-RL/Assets/Scripts/Player/PlayerLife.cs) | Modified | Local `_respawnLocalPoint` in local coordinates. |
| [`Teleporter.cs`](file:///c:/Unity/PacMan-RL/Assets/Scripts/Teleporter/Teleporter.cs) | Modified | Null guards and Rigidbody2D position synchronization. |
| [`GameStartHandler.cs`](file:///c:/Unity/PacMan-RL/Assets/Scripts/GameHandler/GameStartHandler.cs) | Modified | Removed `Destroy(this)`, localized `gameHandler` reference. |
| [`CollectibleAudioHandler.cs`](file:///c:/Unity/PacMan-RL/Assets/Scripts/AudioHandler/CollectibleAudioHandler.cs) | Modified | Removed `Destroy(this)` singleton self-destruction. |
| [`MusicHandler.cs`](file:///c:/Unity/PacMan-RL/Assets/Scripts/MusicHandler/MusicHandler.cs) | Modified | Removed `Destroy(this)` singleton self-destruction. |
| [`GamePauseUiHandler.cs`](file:///c:/Unity/PacMan-RL/Assets/Scripts/GamePauseUi/GamePauseUiHandler.cs) | Modified | Localized `gameHandler`, null guards on audio and pause controls. |
| [`GhostScoreDisplayer.cs`](file:///c:/Unity/PacMan-RL/Assets/Scripts/Ghosts/GhostScoreDisplayer.cs) | Modified | Localized `gameHandler`, index clamping on sprite arrays. |
| [`LevelInputHandler.cs`](file:///c:/Unity/PacMan-RL/Assets/Scripts/LevelInputHandler/LevelInputHandler.cs) | Modified | Localized `gameHandler` and null guards. |

---

## 6. Testing & Verification Guide

### Quick In-Editor Test
1. Open Unity Editor.
2. In the top menu, navigate to:
   **`Tools > PacMan RL > Run Parallel Prefab Verification Test`**
3. The report will run automated checks on all scene components and display a `[PASS]` summary.

### Visual PlayMode Test
1. Group your maze objects under an empty GameObject named `TrainingArea` with `PacManArea`.
2. Duplicate it (`Ctrl+D`) and place the copy at `(50, 0, 0)`.
3. Hit **Play**:
   - Eat a pellet in Maze #1: Only Maze #1 score increments.
   - Touch a ghost in Maze #2: Pac-Man #2 resets inside Maze #2 (no teleportation to Maze #1).
   - Both mazes run simultaneously without cross-talk.

---

## 7. Step 3: ML-Agents Package Integration & Event-Driven Episode Lifecycle (Step 1 Deliverable)

### Modifications Implemented:
1. **Unity Package Manifest**:
   - Added `"com.unity.ml-agents": "3.0.0"` to [`Packages/manifest.json`](file:///c:/Unity/PacMan-RL/Packages/manifest.json).
2. **Tag & Sensing Sanitization**:
   - Added `Energizer` tag to [`TagManager.asset`](file:///c:/Unity/PacMan-RL/ProjectSettings/TagManager.asset) and [`TagsConstants.cs`](file:///c:/Unity/PacMan-RL/Assets/Scripts/TagsConstants.cs).
   - Configured `CollectiblePower.prefab` to use tag `Energizer` (with fallback assignment in `CollectiblePower.cs`).
3. **Decoupled Event Hooks for RL Rewards**:
   - Added `OnPelletEaten`, `OnEnergizerEaten`, and `OnGhostEaten` events in [`GameHandler.cs`](file:///c:/Unity/PacMan-RL/Assets/Scripts/GameHandler/GameHandler.cs).
   - `GhostAiMovement.cs` calls `NotifyGhostEaten()` directly upon being consumed.
4. **Physics Transform Synchronization & State Accessors**:
   - Added `Physics2D.SyncTransforms()` inside [`PacManArea.ResetArea()`](file:///c:/Unity/PacMan-RL/Assets/Scripts/GameHandler/PacManArea.cs) to eliminate Box2D frame-0 tunneling.
   - Added `GetRelativeVectorToNearestPellet()` and `GetRelativeVectorToNearestEnergizer()` inside `PacManArea.cs`.
   - Added `SetActiveGhostCount(int count)` for curriculum learning.
   - Exposed `LastDirection` and `CanMoveDirection(Vector2 dir)` in [`PlayerController.cs`](file:///c:/Unity/PacMan-RL/Assets/Scripts/Player/PlayerController.cs).
   - Exposed `CurrentMode` in [`GhostAiMovement.cs`](file:///c:/Unity/PacMan-RL/Assets/Scripts/Ghosts/GhostAiMovement.cs).
5. **Headless Execution Optimization**:
   - Added batch-mode audio suspension in `PacManArea.Awake()` (`AudioListener.pause = true`, `AudioListener.volume = 0f`).

---

## 8. Step 4: Agent Architecture Implementation (`PacmanAgent.cs`)

### Components Implemented:
1. **`PacmanAgent.cs` (`Assets/Scripts/Player/PacmanAgent.cs`)**:
   - Inherits from `Unity.MLAgents.Agent`.
   - **Discrete Action Space**: 1 branch of size 4 (0: Up, 1: Right, 2: Down, 3: Left) dispatched to `PlayerController.SetInputDirection()`.
   - **Vector Observation Space (28 floats)**:
     - 2 floats: Pac-Man current direction (`LastDirection.x`, `LastDirection.y`).
     - 4 floats: Wall open/blocked flags in 4 cardinal directions (`CanMoveDirection(dir)`).
     - 2 floats: Normalized vector to nearest pellet (`GetRelativeVectorToNearestPellet`).
     - 2 floats: Normalized vector to nearest energizer (`GetRelativeVectorToNearestEnergizer`).
     - 16 floats: 4 ghosts $\times$ 4 state features `[Relative X / 32, Relative Y / 38, Normalized Distance, IsFrightened]`.
     - 2 floats: Frightened mode timer remaining fraction + total pellets remaining fraction.
   - **Reward Shaping**:
     - Pellet consumed: `+0.10`
     - Energizer consumed: `+0.50`
     - Frightened ghost eaten: `+1.50`
     - Terminal win (board cleared): `+15.00`
     - Terminal loss (caught by ghost): `-10.00`
     - Existential step penalty: `-0.0005`
     - Invalid wall collision move: `-0.005`
   - **Curriculum Learning**: Automatically queries `Academy.Instance.EnvironmentParameters.GetWithDefault("num_ghosts", 4.0f)` on episode begin.
   - **Heuristic Mode**: WASD and Arrow key fallback for manual human play verification.
2. **Editor 1-Click Tooling**:
   - Added `Tools > PacMan RL > Attach RL Agent & Sensors to Player` in `MazeAreaGridGenerator.cs`.
   - Added automated Test 6 in `ParallelPrefabTester.cs` auditing agent component configuration.


