using System;
using GameHandler;
using Ghosts;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

namespace Player
{
    /// <summary>
    /// ML-Agents Agent controlling Pac-Man.
    /// Manages discrete action mapping, 2D sensory observations, event-driven reward shaping,
    /// and 0.0s episode resets across parallel training environments.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class PacmanAgent : Agent
    {
        [Header("Local Environment References (Auto-resolved if null)")]
        [SerializeField] private PlayerController _playerController;
        [SerializeField] private PacManArea _pacManArea;
        [SerializeField] private GameHandler.GameHandler _gameHandler;
        [SerializeField] private Rigidbody2D _rigidbody2D;

        [Header("Reward Shaping Values")]
        [Tooltip("Dense reward granted when Pac-Man consumes a standard pellet.")]
        public float pelletReward = 0.10f;

        [Tooltip("Dense reward granted when Pac-Man consumes a power pellet / energizer.")]
        public float energizerReward = 0.50f;

        [Tooltip("Dense reward granted when Pac-Man eats a frightened ghost.")]
        public float ghostEatenReward = 1.50f;

        [Tooltip("Terminal reward granted when all pellets in the maze are cleared (Win condition).")]
        public float boardClearedReward = 15.0f;

        [Tooltip("Terminal penalty applied when Pac-Man is caught by a dangerous ghost (Loss condition).")]
        public float deathPenalty = -10.0f;

        [Tooltip("Existential step penalty applied per decision step to discourage stalling/circling.")]
        public float stepPenalty = -0.0005f;

        [Tooltip("Minor penalty applied when attempting to steer into an immediately blocked wall.")]
        public float invalidMovePenalty = -0.005f;

        [Header("Curriculum Learning")]
        [Tooltip("Whether to read ghost difficulty parameters from Academy.Instance.EnvironmentParameters.")]
        public bool enableCurriculum = true;

        private bool _isSubscribed;

        public override void Initialize()
        {
            ResolveDependencies();
            SubscribeEvents();
        }

        private void OnDestroy()
        {
            UnsubscribeEvents();
        }

        public void ResolveDependencies()
        {
            if (_playerController == null)
                _playerController = GetComponent<PlayerController>();

            if (_rigidbody2D == null)
                _rigidbody2D = GetComponent<Rigidbody2D>();

            Transform areaRoot = PacManArea.FindAreaRoot(transform);
            Transform searchRoot = areaRoot != null ? areaRoot : (transform.parent != null ? transform.parent : transform);

            if (_pacManArea == null)
                _pacManArea = searchRoot.GetComponent<PacManArea>() ?? searchRoot.GetComponentInChildren<PacManArea>(true);

            if (_gameHandler == null)
                _gameHandler = searchRoot.GetComponentInChildren<GameHandler.GameHandler>(true) ??
                               GetComponentInParent<GameHandler.GameHandler>();
        }

        private void SubscribeEvents()
        {
            if (_isSubscribed || _gameHandler == null) return;

            _gameHandler.OnPlayerDied += OnPlayerDiedHandler;
            _gameHandler.OnBoardCleared += OnBoardClearedHandler;
            _gameHandler.OnPelletEaten += OnPelletEatenHandler;
            _gameHandler.OnEnergizerEaten += OnEnergizerEatenHandler;
            _gameHandler.OnGhostEaten += OnGhostEatenHandler;

            _isSubscribed = true;
        }

        private void UnsubscribeEvents()
        {
            if (!_isSubscribed || _gameHandler == null) return;

            _gameHandler.OnPlayerDied -= OnPlayerDiedHandler;
            _gameHandler.OnBoardCleared -= OnBoardClearedHandler;
            _gameHandler.OnPelletEaten -= OnPelletEatenHandler;
            _gameHandler.OnEnergizerEaten -= OnEnergizerEatenHandler;
            _gameHandler.OnGhostEaten -= OnGhostEatenHandler;

            _isSubscribed = false;
        }

        public override void OnEpisodeBegin()
        {
            ResolveDependencies();
            SubscribeEvents();

            // 1. Apply Curriculum Learning: Adjust active ghost count from Academy environment parameters
            if (enableCurriculum && _pacManArea != null)
            {
                float ghostParam = Academy.Instance.EnvironmentParameters.GetWithDefault("num_ghosts", 4.0f);
                _pacManArea.SetActiveGhostCount(Mathf.RoundToInt(ghostParam));
            }

            // 2. Reset entire local training area in 0.0s (resets pellets, ghosts, score, physics)
            if (_pacManArea != null)
            {
                _pacManArea.ResetArea();
            }
            else if (_gameHandler != null)
            {
                _gameHandler.ResetBoard();
                Physics2D.SyncTransforms();
            }

            // 3. Clear residual velocities
            if (_rigidbody2D != null)
            {
                _rigidbody2D.linearVelocity = Vector2.zero;
                _rigidbody2D.position = transform.position;
            }
        }

        public override void CollectObservations(VectorSensor sensor)
        {
            if (_playerController == null || _pacManArea == null || _gameHandler == null)
            {
                // Fallback zero observations if components are not yet initialized
                for (int i = 0; i < 28; i++) sensor.AddObservation(0f);
                return;
            }

            Vector3 agentLocalPos = transform.localPosition;

            // 1. Current Pac-Man Direction (2 floats)
            Vector2 lastDir = _playerController.LastDirection;
            sensor.AddObservation(lastDir.x);
            sensor.AddObservation(lastDir.y);

            // 2. Immediate Wall Proximity in 4 Cardinal Directions (4 floats: 1.0 = open, 0.0 = blocked)
            sensor.AddObservation(_playerController.CanMoveDirection(Vector2.up) ? 1.0f : 0.0f);
            sensor.AddObservation(_playerController.CanMoveDirection(Vector2.right) ? 1.0f : 0.0f);
            sensor.AddObservation(_playerController.CanMoveDirection(Vector2.down) ? 1.0f : 0.0f);
            sensor.AddObservation(_playerController.CanMoveDirection(Vector2.left) ? 1.0f : 0.0f);

            // 3. Normalized Relative Vector to Nearest Pellet (2 floats)
            Vector2 toPellet = _pacManArea.GetRelativeVectorToNearestPellet(agentLocalPos);
            sensor.AddObservation(toPellet.sqrMagnitude > 0.001f ? toPellet.normalized : Vector2.zero);

            // 4. Normalized Relative Vector to Nearest Energizer (2 floats)
            Vector2 toEnergizer = _pacManArea.GetRelativeVectorToNearestEnergizer(agentLocalPos);
            sensor.AddObservation(toEnergizer.sqrMagnitude > 0.001f ? toEnergizer.normalized : Vector2.zero);

            // 5. Ghost State Observations (4 ghosts * 4 features = 16 floats)
            // Each ghost provides: [Relative X / 32, Relative Y / 38, Normalized Distance, IsFrightened]
            GhostAiMovement[] ghosts = _pacManArea.Ghosts;
            for (int i = 0; i < 4; i++)
            {
                if (ghosts != null && i < ghosts.Length && ghosts[i] != null && ghosts[i].gameObject.activeInHierarchy)
                {
                    Vector3 ghostLocalPos = ghosts[i].transform.localPosition;
                    Vector2 delta = (Vector2)(ghostLocalPos - agentLocalPos);
                    sensor.AddObservation(delta.x / 32.0f);
                    sensor.AddObservation(delta.y / 38.0f);
                    sensor.AddObservation(Mathf.Clamp01(delta.magnitude / 40.0f));
                    sensor.AddObservation(ghosts[i].CurrentMode == GhostMode.Frightened ? 1.0f : 0.0f);
                }
                else
                {
                    // Ghost is disabled or not present in current curriculum lesson (zero-padded)
                    sensor.AddObservation(0.0f);
                    sensor.AddObservation(0.0f);
                    sensor.AddObservation(0.0f);
                    sensor.AddObservation(0.0f);
                }
            }

            // 6. Global Environment Progress & Timers (2 floats)
            float totalFrightenedTime = _gameHandler.FrightenedTimeTotal > 0 ? _gameHandler.FrightenedTimeTotal : 10f;
            sensor.AddObservation(Mathf.Clamp01(_gameHandler.FrightenedTimerRemaining / totalFrightenedTime));

            int totalPellets = _gameHandler.TotalPelletCount > 0 ? _gameHandler.TotalPelletCount : 244;
            sensor.AddObservation(Mathf.Clamp01((float)_gameHandler.PelletCount / totalPellets));
        }

        public override void OnActionReceived(ActionBuffers actions)
        {
            if (_playerController == null) return;

            int action = actions.DiscreteActions[0];
            Vector2 chosenDirection = Vector2.zero;

            switch (action)
            {
                case 1:
                    chosenDirection = Vector2.up;
                    break;
                case 2:
                    chosenDirection = Vector2.right;
                    break;
                case 3:
                    chosenDirection = Vector2.down;
                    break;
                case 4:
                    chosenDirection = Vector2.left;
                    break;
                case 0:
                default:
                    // 0 = No-op: continue moving in current direction without turning
                    chosenDirection = Vector2.zero;
                    break;
            }

            // Check if agent requested a move directly into an impassable wall
            if (chosenDirection != Vector2.zero)
            {
                if (!_playerController.CanMoveDirection(chosenDirection))
                {
                    AddReward(invalidMovePenalty);
                }
                _playerController.SetInputDirection(chosenDirection);
            }

            // Existential step penalty to encourage rapid maze clearance
            AddReward(stepPenalty);
        }

        public override void Heuristic(in ActionBuffers actionsOut)
        {
            var discreteActions = actionsOut.DiscreteActions;
            discreteActions[0] = 0; // Default: maintain current direction (No-Op)

            // 1. Support Unity New Input System
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
                {
                    discreteActions[0] = 1; // Up
                    return;
                }
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
                {
                    discreteActions[0] = 2; // Right
                    return;
                }
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
                {
                    discreteActions[0] = 3; // Down
                    return;
                }
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
                {
                    discreteActions[0] = 4; // Left
                    return;
                }
            }

            // 2. Fallback to Legacy Input (if enabled in PlayerSettings)
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
            {
                discreteActions[0] = 1; // Up
            }
            else if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
            {
                discreteActions[0] = 2; // Right
            }
            else if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
            {
                discreteActions[0] = 3; // Down
            }
            else if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
            {
                discreteActions[0] = 4; // Left
            }
        }

        #region Event Callbacks

        private void OnPelletEatenHandler(int points)
        {
            AddReward(pelletReward);
        }

        private void OnEnergizerEatenHandler(int points)
        {
            AddReward(energizerReward);
        }

        private void OnGhostEatenHandler(int points)
        {
            AddReward(ghostEatenReward);
        }

        private void OnBoardClearedHandler()
        {
            AddReward(boardClearedReward);
            EndEpisode();
        }

        private void OnPlayerDiedHandler()
        {
            AddReward(deathPenalty);
            EndEpisode();
        }

        #endregion
    }
}
