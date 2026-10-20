using AudioHandler;
using UnityEngine;

namespace Collectibles
{
    public class CollectiblePacGum : MonoBehaviour
    {
        public int points = 10;

        private AudioSource _audioSource;
        private SpriteRenderer _spriteRenderer;
        private Collider2D _collider2D;

        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
            _spriteRenderer = GetComponent<SpriteRenderer>();
            _collider2D = GetComponent<Collider2D>();
        }

        private void Start()
        {
            if (_audioSource == null) _audioSource = GetComponent<AudioSource>();
            if (_spriteRenderer == null) _spriteRenderer = GetComponent<SpriteRenderer>();
            if (_collider2D == null) _collider2D = GetComponent<Collider2D>();
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (!collision.gameObject.CompareTag(TagsConstants.PlayerTag)) return;
            if (ScoreHandler.ScoreHandler.Instance != null)
                ScoreHandler.ScoreHandler.Instance.AddScore(points);
            if (_collider2D != null) _collider2D.enabled = false;
            if (_spriteRenderer != null) _spriteRenderer.enabled = false;
            if (CollectibleAudioHandler.Instance != null && CollectibleAudioHandler.Instance.PlaySound())
            {
                if (_audioSource != null) _audioSource.Play();
            }
            if (GameHandler.GameHandler.Instance != null)
                GameHandler.GameHandler.Instance.DecrementPacGumNumber();

            // In RL: Deactivate rather than Destroy, so pellets can be reset in 0.0s for the next episode
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
