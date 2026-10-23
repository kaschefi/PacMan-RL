#if UNITY_EDITOR
using GameHandler;
using UnityEditor;
using UnityEngine;

namespace Editor
{
    public class MazeAreaGridGenerator : EditorWindow
    {
        private GameObject sourceMaze;
        private int rows = 4;
        private int columns = 4;
        private float spacingX = 50f;
        private float spacingY = 50f;

        [MenuItem("Tools/PacMan RL/Generate Parallel Mazes Grid")]
        public static void ShowWindow()
        {
            var window = GetWindow<MazeAreaGridGenerator>("Parallel Mazes Grid");
            window.minSize = new Vector2(350, 300);
            window.Show();
        }

        [MenuItem("Tools/PacMan RL/Resolve Packages")]
        public static void ResolvePackages()
        {
            UnityEditor.PackageManager.Client.Resolve();
            Debug.Log("[PacMan RL] Triggered Unity Package Manager resolve.");
        }

        [MenuItem("Tools/PacMan RL/Attach RL Agent & Sensors to Player")]
        public static void AttachAgentToPlayer()
        {
            var players = Object.FindObjectsByType<Player.PlayerController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (players.Length == 0)
            {
                Debug.LogWarning("[PacMan RL] No PlayerController found in the scene.");
                return;
            }

            foreach (var pc in players)
            {
                var go = pc.gameObject;
                pc.enabled = true;

                // 1. Add PacmanAgent
                var agent = go.GetComponent<Player.PacmanAgent>();
                if (agent == null)
                {
                    agent = go.AddComponent<Player.PacmanAgent>();
                    Undo.RegisterCreatedObjectUndo(agent, "Add PacmanAgent");
                }

                // 2. Configure BehaviorParameters
                var bp = go.GetComponent<Unity.MLAgents.Policies.BehaviorParameters>();
                if (bp == null)
                {
                    bp = go.AddComponent<Unity.MLAgents.Policies.BehaviorParameters>();
                    Undo.RegisterCreatedObjectUndo(bp, "Add BehaviorParameters");
                }
                bp.BehaviorName = "PacmanAgent";
                bp.BrainParameters.VectorObservationSize = 28;
                bp.BrainParameters.NumStackedVectorObservations = 1;
                bp.BrainParameters.ActionSpec = Unity.MLAgents.Actuators.ActionSpec.MakeDiscrete(5);

                // 3. Configure RayPerceptionSensorComponent2D
                var sensor = go.GetComponent<Unity.MLAgents.Sensors.RayPerceptionSensorComponent2D>();
                if (sensor == null)
                {
                    sensor = go.AddComponent<Unity.MLAgents.Sensors.RayPerceptionSensorComponent2D>();
                    Undo.RegisterCreatedObjectUndo(sensor, "Add RayPerceptionSensorComponent2D");
                }
                sensor.SensorName = "RayPerceptionSensor2D";
                sensor.DetectableTags = new System.Collections.Generic.List<string>
                {
                    TagsConstants.WallBordersTag,
                    TagsConstants.PacGumTag,
                    TagsConstants.EnergizerTag,
                    TagsConstants.EnemyTag,
                    "Door"
                };
                sensor.RaysPerDirection = 4;
                sensor.MaxRayDegrees = 180f;
                sensor.SphereCastRadius = 0.25f;
                sensor.RayLength = 16f;
                sensor.ObservationStacks = 2;

                // 4. Configure DecisionRequester
                var dr = go.GetComponent<Unity.MLAgents.DecisionRequester>();
                if (dr == null)
                {
                    dr = go.AddComponent<Unity.MLAgents.DecisionRequester>();
                    Undo.RegisterCreatedObjectUndo(dr, "Add DecisionRequester");
                }
                dr.DecisionPeriod = 5;
                dr.TakeActionsBetweenDecisions = true;

                EditorUtility.SetDirty(go);
            }

            Debug.Log($"[PacMan RL] Successfully configured PacmanAgent, BehaviorParameters, DecisionRequester, and RayPerceptionSensor2D on {players.Length} player(s).");
        }

        [MenuItem("Tools/PacMan RL/Frame Camera to All Mazes")]
        public static void FrameCameraToAllMazes()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                cam = Object.FindAnyObjectByType<Camera>();
            }

            if (cam == null)
            {
                Debug.LogWarning("[PacMan RL] No Camera found in the scene.");
                return;
            }

            // Find all maze areas or GameHandlers
            var areas = Object.FindObjectsByType<PacManArea>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            var gameHandlers = Object.FindObjectsByType<GameHandler.GameHandler>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            var targets = new System.Collections.Generic.List<Transform>();
            if (areas.Length > 0)
            {
                foreach (var a in areas) targets.Add(a.transform);
            }
            else if (gameHandlers.Length > 0)
            {
                foreach (var gh in gameHandlers)
                {
                    Transform parentTarget = gh.transform.parent != null ? gh.transform.parent : gh.transform;
                    if (!targets.Contains(parentTarget))
                        targets.Add(parentTarget);
                }
            }
            else
            {
                foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                {
                    if (root.name.ToLower().Contains("trainingarea") || root.name.ToLower().Contains("maze"))
                    {
                        targets.Add(root.transform);
                    }
                }
            }

            if (targets.Count == 0)
            {
                Debug.LogWarning("[PacMan RL] No maze areas or training areas found to frame.");
                return;
            }

            Bounds combinedBounds = new Bounds();
            bool hasBounds = false;

            foreach (var t in targets)
            {
                var renderers = t.GetComponentsInChildren<Renderer>();
                foreach (var r in renderers)
                {
                    if (r is ParticleSystemRenderer) continue;
                    if (!hasBounds)
                    {
                        combinedBounds = r.bounds;
                        hasBounds = true;
                    }
                    else
                    {
                        combinedBounds.Encapsulate(r.bounds);
                    }
                }
            }

            if (!hasBounds)
            {
                combinedBounds = new Bounds(targets[0].position, new Vector3(32f, 38f, 0f));
                foreach (var t in targets)
                {
                    combinedBounds.Encapsulate(new Bounds(t.position, new Vector3(32f, 38f, 0f)));
                }
            }

            Undo.RecordObject(cam.transform, "Frame Camera To Mazes");
            Undo.RecordObject(cam, "Frame Camera To Mazes");

            Vector3 newPos = combinedBounds.center;
            newPos.z = cam.transform.position.z;
            cam.transform.position = newPos;

            float aspect = cam.aspect > 0.1f ? cam.aspect : (16f / 9f);
            float verticalHalf = combinedBounds.size.y / 2f;
            float horizontalHalf = (combinedBounds.size.x / 2f) / aspect;
            float targetSize = Mathf.Max(verticalHalf, horizontalHalf) * 1.15f; // 15% margin

            cam.orthographic = true;
            cam.orthographicSize = Mathf.Max(targetSize, 18f);

            Debug.Log($"[PacMan RL] Auto-framed Camera to {targets.Count} mazes! Camera Position: {newPos}, Orthographic Size: {cam.orthographicSize:F1}");
        }

        private void OnGUI()
        {
            GUILayout.Label("Parallel RL Mazes Grid Generator", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            sourceMaze = (GameObject)EditorGUILayout.ObjectField("Source Maze Area", sourceMaze, typeof(GameObject), true);
            rows = EditorGUILayout.IntSlider("Rows", rows, 1, 16);
            columns = EditorGUILayout.IntSlider("Columns", columns, 1, 16);
            spacingX = EditorGUILayout.FloatField("X Spacing (min 40-50 units)", spacingX);
            spacingY = EditorGUILayout.FloatField("Y Spacing (min 40-50 units)", spacingY);

            int totalMazes = rows * columns;
            EditorGUILayout.HelpBox($"Total Parallel Mazes: {totalMazes}\nGrid Spacing: {spacingX}x{spacingY} units (prevents sensor bleed).", MessageType.Info);

            EditorGUILayout.Space();

            if (GUILayout.Button("Frame Camera to All Current Mazes", GUILayout.Height(28)))
            {
                FrameCameraToAllMazes();
            }

            EditorGUILayout.Space();

            GUI.enabled = sourceMaze != null;
            if (GUILayout.Button($"Generate {totalMazes} Mazes in Grid", GUILayout.Height(35)))
            {
                GenerateGrid();
            }
            GUI.enabled = true;

            EditorGUILayout.Space();
            var gridObj = GameObject.Find("TrainingAreas_16x");
            if (gridObj != null)
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Mode: Single Arena", GUILayout.Height(26)))
                {
                    SwitchToSingleMode();
                }
                if (GUILayout.Button("Mode: Parallel Grid", GUILayout.Height(26)))
                {
                    SwitchToGridMode();
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("Attach PacmanAgent & Sensors to Players", GUILayout.Height(30)))
            {
                AttachAgentToPlayer();
            }
        }

        private void SwitchToSingleMode()
        {
            var gridObj = GameObject.Find("TrainingAreas_16x");
            if (gridObj != null)
            {
                Undo.RecordObject(gridObj, "Disable Grid");
                gridObj.SetActive(false);
            }

            if (sourceMaze != null && sourceMaze.scene.IsValid())
            {
                Undo.RecordObject(sourceMaze, "Enable Single Maze");
                sourceMaze.SetActive(true);

                var inputHandler = Object.FindAnyObjectByType<LevelInputHandler.LevelInputHandler>();
                if (inputHandler != null)
                {
                    var player = sourceMaze.GetComponentInChildren<Player.PlayerController>(true);
                    if (player != null)
                    {
                        Undo.RecordObject(inputHandler, "Bind Input to Single Arena Player");
                        inputHandler.playerController = player;
                    }
                }
            }

            FrameCameraToAllMazes();
            Debug.Log("[PacMan RL] Switched to Single Arena Mode. Template arena is active, parallel grid is hidden.");
        }

        private void SwitchToGridMode()
        {
            if (sourceMaze != null && sourceMaze.scene.IsValid())
            {
                Undo.RecordObject(sourceMaze, "Disable Single Maze");
                sourceMaze.SetActive(false);
            }

            // Find inactive TrainingAreas_16x
            foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root.name == "TrainingAreas_16x")
                {
                    Undo.RecordObject(root, "Enable Grid");
                    root.SetActive(true);

                    var inputHandler = Object.FindAnyObjectByType<LevelInputHandler.LevelInputHandler>();
                    if (inputHandler != null)
                    {
                        var player = root.GetComponentInChildren<Player.PlayerController>(true);
                        if (player != null)
                        {
                            Undo.RecordObject(inputHandler, "Bind Input to Grid First Player");
                            inputHandler.playerController = player;
                        }
                    }
                    break;
                }
            }

            FrameCameraToAllMazes();
            Debug.Log("[PacMan RL] Switched to Parallel Grid Mode. Parallel grid is active, template arena is safely disabled.");
        }

        private void GenerateGrid()
        {
            if (sourceMaze == null) return;

            // Clean up any previously generated container to prevent duplicate grid stacking
            var existingContainer = GameObject.Find("TrainingAreas_16x");
            if (existingContainer != null)
            {
                Undo.DestroyObjectImmediate(existingContainer);
            }

            // If sourceMaze is an active scene object, deactivate it so it doesn't double-stack at (0,0,0) with Maze_Area_0_0
            if (!PrefabUtility.IsPartOfPrefabAsset(sourceMaze) && sourceMaze.scene.IsValid())
            {
                Undo.RecordObject(sourceMaze, "Deactivate Source Template Maze");
                sourceMaze.SetActive(false);
            }

            var parent = new GameObject("TrainingAreas_16x");
            Undo.RegisterCreatedObjectUndo(parent, "Create TrainingAreas Container");

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < columns; c++)
                {
                    Vector3 position = new Vector3(c * spacingX, r * spacingY, 0f);
                    GameObject mazeInstance;

                    if (PrefabUtility.IsPartOfPrefabAsset(sourceMaze))
                    {
                        mazeInstance = (GameObject)PrefabUtility.InstantiatePrefab(sourceMaze, parent.transform);
                        mazeInstance.transform.position = position;
                    }
                    else
                    {
                        mazeInstance = Instantiate(sourceMaze, position, Quaternion.identity, parent.transform);
                        mazeInstance.SetActive(true); // Ensure clone is active even if template source was deactivated
                    }

                    mazeInstance.name = $"Maze_Area_{r}_{c}";
                    Undo.RegisterCreatedObjectUndo(mazeInstance, "Create Maze Instance");

                    // Disable duplicate EventSystem components on cloned mazes to prevent console warnings
                    if (r > 0 || c > 0)
                    {
                        var duplicateEventSystems = mazeInstance.GetComponentsInChildren<UnityEngine.EventSystems.EventSystem>(true);
                        foreach (var es in duplicateEventSystems)
                            es.enabled = false;
                    }

                    var area = mazeInstance.GetComponent<PacManArea>();
                    if (area == null)
                    {
                        area = mazeInstance.AddComponent<PacManArea>();
                    }
                    area.InitializeArea();
                }
            }

            // Automatically re-link manual keyboard input handler to Maze_Area_0_0's Player
            var inputHandler = Object.FindAnyObjectByType<LevelInputHandler.LevelInputHandler>();
            if (inputHandler != null)
            {
                var firstPlayer = parent.GetComponentInChildren<Player.PlayerController>(true);
                if (firstPlayer != null)
                {
                    Undo.RecordObject(inputHandler, "Bind Input to First Generated Player");
                    inputHandler.playerController = firstPlayer;
                }
            }

            Selection.activeGameObject = parent;
            Debug.Log($"[PacMan RL] Successfully generated {totalMazes()} parallel mazes spaced {spacingX}x{spacingY} units apart.");
            FrameCameraToAllMazes();
        }

        private int totalMazes() => rows * columns;
    }
}
#endif
