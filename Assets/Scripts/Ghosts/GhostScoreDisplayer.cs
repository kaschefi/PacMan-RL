using GameHandler;
using UnityEngine;

namespace Ghosts
{
    public class GhostScoreDisplayer : MonoBehaviour
    {
        private SpriteRenderer _ghostScoreSpriteRenderer;
        public Sprite[] ghostScoreSpriteArray;
        [SerializeField] private GameHandler.GameHandler gameHandler;

        private void Awake()
        {
            _ghostScoreSpriteRenderer = GetComponent<SpriteRenderer>();
            ResolveDependencies();
        }

        private void Start()
        {
            if (_ghostScoreSpriteRenderer == null)
                _ghostScoreSpriteRenderer = GetComponent<SpriteRenderer>();
            ResolveDependencies();
        }

        public void ResolveDependencies(GameHandler.GameHandler gh = null)
        {
            if (gh != null) gameHandler = gh;

            if (gameHandler == null)
            {
                Transform areaRoot = PacManArea.FindAreaRoot(transform);
                Transform searchRoot = areaRoot != null ? areaRoot : (transform.parent != null ? transform.parent : transform);
                gameHandler = searchRoot.GetComponentInChildren<GameHandler.GameHandler>(true) ??
                              GetComponentInParent<GameHandler.GameHandler>() ??
                              (transform.parent == null ? FindFirstObjectByType<GameHandler.GameHandler>(FindObjectsInactive.Include) : null);
            }
        }

        public void DisplayGhostScore()
        {
            var gh = gameHandler != null ? gameHandler : GameHandler.GameHandler.Instance;
            int count = gh != null ? gh.GhostCountEaten : 0;
            if (_ghostScoreSpriteRenderer != null && ghostScoreSpriteArray != null && ghostScoreSpriteArray.Length > 0)
            {
                int idx = Mathf.Clamp(count, 0, ghostScoreSpriteArray.Length - 1);
                _ghostScoreSpriteRenderer.sprite = ghostScoreSpriteArray[idx];
            }
        }

        public void HideGhostScore()
        {
            if (_ghostScoreSpriteRenderer != null)
                _ghostScoreSpriteRenderer.sprite = null;
        }
    }
}
