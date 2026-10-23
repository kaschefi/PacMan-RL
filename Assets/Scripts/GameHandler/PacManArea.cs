using Collectibles;
using Ghosts;
using Player;
using UnityEngine;

namespace GameHandler
{
    /// <summary>
    /// Encapsulates a self-contained Pac-Man training area for parallel RL environments.
    /// Manages local references, coordinates resets, and prevents cross-environment state bleed.
    /// </summary>
    public class PacManArea : MonoBehaviour
    {
        [Header("Area Components (Auto-resolved if null)")]
        [SerializeField] private GameHandler gameHandler;
        [SerializeField] private PlayerController player;
        [SerializeField] private global::ScoreHandler.ScoreHandler scoreHandler;
        [SerializeField] private GhostAiMovement[] ghosts;

        public GameHandler GameHandler => gameHandler;
        public PlayerController Player => player;
        public global::ScoreHandler.ScoreHandler AreaScoreHandler => scoreHandler;
        public GhostAiMovement[] Ghosts => ghosts;

        [Header("Area Bounds & Spacing Visualization")]
        public Vector2 areaDimensions = new Vector2(32f, 38f);
        public Color gizmoColor = new Color(0f, 0.8f, 1f, 0.35f);

        private CollectiblePacGum[] _allPacGums;
        private CollectiblePower[] _allPowerPellets;

        private void Awake()
        {
            if (Application.isBatchMode)
            {
                AudioListener.pause = true;
                AudioListener.volume = 0f;
            }
            InitializeArea();
        }

        /// <summary>
        /// Locates the root training area transform for any component within a maze.
        /// Searches for an ancestor with PacManArea or with "trainingarea"/"maze" naming,
        /// or stops before container roots.
        /// </summary>
        public static Transform FindAreaRoot(Transform current)
        {
            if (current == null) return null;

            // 1. If an ancestor has PacManArea, that is explicitly the area root
            var pacManArea = current.GetComponentInParent<PacManArea>();
            if (pacManArea != null) return pacManArea.transform;

            // 2. Climb up parent chain
            Transform curr = current;
            Transform bestRoot = curr;

            while (curr != null)
            {
                string lower = curr.name.ToLower();
                if (lower.Contains("trainingarea") || lower.Contains("maze_area") || lower.Contains("mazearea") || lower.Contains("maze"))
                    return curr;

                if (curr.GetComponentInChildren<GameHandler>(true) != null)
                {
                    bestRoot = curr;
                }

                if (curr.parent == null)
                    break;

                curr = curr.parent;
            }

            return bestRoot;
        }

        public void InitializeArea()
        {
            if (gameHandler == null)
                gameHandler = GetComponentInChildren<GameHandler>(true);
            if (player == null)
                player = GetComponentInChildren<PlayerController>(true);
            if (scoreHandler == null)
                scoreHandler = GetComponentInChildren<global::ScoreHandler.ScoreHandler>(true);
            if (ghosts == null || ghosts.Length == 0)
                ghosts = GetComponentsInChildren<GhostAiMovement>(true);

            if (gameHandler != null)
                gameHandler.EnsureInitialized();

            // Inject local dependencies directly to all pellets and ghosts
            _allPacGums = GetComponentsInChildren<CollectiblePacGum>(true);
            foreach (var pellet in _allPacGums)
                pellet.ResolveDependencies(gameHandler, scoreHandler);

            _allPowerPellets = GetComponentsInChildren<CollectiblePower>(true);
            foreach (var power in _allPowerPellets)
                power.ResolveDependencies(gameHandler, scoreHandler);

            if (ghosts != null)
            {
                foreach (var ghost in ghosts)
                {
                    if (ghost != null)
                        ghost.ResolveDependencies(gameHandler, scoreHandler);
                }
            }
        }

        /// <summary>
        /// Resets the entire board for this training area in 0.0 seconds.
        /// Can be hooked directly into the RL Agent's OnEpisodeBegin().
        /// </summary>
        public void ResetArea()
        {
            if (gameHandler != null)
            {
                gameHandler.ResetBoard();
            }

            // Immediately synchronize 2D Box2D physics transforms to prevent frame-0 tunneling
            Physics2D.SyncTransforms();
        }

        /// <summary>
        /// Sets the number of active ghosts for Curriculum Learning lessons.
        /// </summary>
        public void SetActiveGhostCount(int count)
        {
            if (ghosts == null) return;
            int clamped = Mathf.Clamp(count, 0, ghosts.Length);
            for (int i = 0; i < ghosts.Length; i++)
            {
                if (ghosts[i] != null)
                {
                    bool active = i < clamped;
                    ghosts[i].gameObject.SetActive(active);
                    ghosts[i].enabled = active;
                }
            }
        }

        /// <summary>
        /// Calculates the relative vector from Pac-Man's local position to the nearest active pellet.
        /// </summary>
        public Vector2 GetRelativeVectorToNearestPellet(Vector3 agentLocalPosition)
        {
            Vector2 bestDelta = Vector2.zero;
            float minSqrDist = float.MaxValue;
            if (_allPacGums == null) return bestDelta;

            for (int i = 0; i < _allPacGums.Length; i++)
            {
                var pellet = _allPacGums[i];
                if (pellet != null && pellet.gameObject.activeSelf)
                {
                    Vector2 delta = (Vector2)(pellet.transform.localPosition - agentLocalPosition);
                    float sqrDist = delta.sqrMagnitude;
                    if (sqrDist < minSqrDist)
                    {
                        minSqrDist = sqrDist;
                        bestDelta = delta;
                    }
                }
            }
            return bestDelta;
        }

        /// <summary>
        /// Calculates the relative vector from Pac-Man's local position to the nearest active power pellet (energizer).
        /// </summary>
        public Vector2 GetRelativeVectorToNearestEnergizer(Vector3 agentLocalPosition)
        {
            Vector2 bestDelta = Vector2.zero;
            float minSqrDist = float.MaxValue;
            if (_allPowerPellets == null) return bestDelta;

            for (int i = 0; i < _allPowerPellets.Length; i++)
            {
                var power = _allPowerPellets[i];
                if (power != null && power.gameObject.activeSelf)
                {
                    Vector2 delta = (Vector2)(power.transform.localPosition - agentLocalPosition);
                    float sqrDist = delta.sqrMagnitude;
                    if (sqrDist < minSqrDist)
                    {
                        minSqrDist = sqrDist;
                        bestDelta = delta;
                    }
                }
            }
            return bestDelta;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = gizmoColor;
            Gizmos.DrawWireCube(transform.position, new Vector3(areaDimensions.x, areaDimensions.y, 0f));
        }
    }
}
