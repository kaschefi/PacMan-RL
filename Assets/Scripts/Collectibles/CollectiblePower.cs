using UnityEngine;

namespace Collectibles
{
    public class CollectiblePower : MonoBehaviour
    {
        public int points = 100;
        private SpriteRenderer _spriteRenderer;
        private Collider2D _collider2D;

        private void Awake()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
            _collider2D = GetComponent<Collider2D>();
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (!collision.gameObject.CompareTag(TagsConstants.PlayerTag)) return;
            if (ScoreHandler.ScoreHandler.Instance != null)
                ScoreHandler.ScoreHandler.Instance.AddScore(points);
            if (GameHandler.GameHandler.Instance != null)
                GameHandler.GameHandler.Instance.SwitchingFrightenedMode();

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
