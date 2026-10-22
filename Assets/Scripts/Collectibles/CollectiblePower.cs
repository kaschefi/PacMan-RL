using GameHandler;
using UnityEngine;

namespace Collectibles
{
    public class CollectiblePower : MonoBehaviour
    {
        public int points = 100;

        [Header("Local Area References (Auto-resolved if null)")]
        [SerializeField] private GameHandler.GameHandler gameHandler;
        [SerializeField] private global::ScoreHandler.ScoreHandler scoreHandler;

        private SpriteRenderer _spriteRenderer;
        private Collider2D _collider2D;

        private void Awake()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
            _collider2D = GetComponent<Collider2D>();
            ResolveDependencies();
        }

        private void Start()
        {
            if (_spriteRenderer == null) _spriteRenderer = GetComponent<SpriteRenderer>();
            if (_collider2D == null) _collider2D = GetComponent<Collider2D>();
            ResolveDependencies();
        }

        public void ResolveDependencies(GameHandler.GameHandler gh = null, global::ScoreHandler.ScoreHandler sh = null)
        {
            if (gh != null) gameHandler = gh;
            if (sh != null) scoreHandler = sh;

            Transform areaRoot = PacManArea.FindAreaRoot(transform);
            Transform searchRoot = areaRoot != null ? areaRoot : (transform.parent != null ? transform.parent : transform);

            if (gameHandler == null)
            {
                gameHandler = searchRoot.GetComponentInChildren<GameHandler.GameHandler>(true) ??
                              GetComponentInParent<GameHandler.GameHandler>() ??
                              (transform.parent == null ? FindFirstObjectByType<GameHandler.GameHandler>(FindObjectsInactive.Include) : null);
            }

            if (scoreHandler == null)
            {
                scoreHandler = searchRoot.GetComponentInChildren<global::ScoreHandler.ScoreHandler>(true) ??
                               GetComponentInParent<global::ScoreHandler.ScoreHandler>() ??
                               (transform.parent == null ? FindFirstObjectByType<global::ScoreHandler.ScoreHandler>(FindObjectsInactive.Include) : null);
            }
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (!collision.gameObject.CompareTag(TagsConstants.PlayerTag)) return;

            var sh = scoreHandler != null ? scoreHandler : global::ScoreHandler.ScoreHandler.Instance;
            if (sh != null)
                sh.AddScore(points);

            var gh = gameHandler != null ? gameHandler : GameHandler.GameHandler.Instance;
            if (gh != null)
                gh.SwitchingFrightenedMode();

            // In RL: Deactivate rather than Destroy, so power pellets can be reset in 0.0s for the next episode
            gameObject.SetActive(false);
        }

        public void ResetPellet()
        {
            gameObject.SetActive(true);
            if (_collider2D != null) _collider2D.enabled = true;
            if (_spriteRenderer != null) _spriteRenderer.enabled = true;
        }
    }
}
