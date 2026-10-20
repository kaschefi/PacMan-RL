using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Player
{
    public class PlayerLife : MonoBehaviour
    {
        private static readonly int IsDead = Animator.StringToHash(AnimationsConstants.PlayerIsDead);

        // Animations and sprites
        public Animator anim;
        public Sprite heartTexture;
        public Vector3 heartSpriteScale = new(1f, 1f);

        // Lives: in RL mode, 1 Death = 1 Episode. Lives system is disabled.
        public int life = 1;

        // Hearts display properties
        public GameObject heartsParent;
        public float heartOffset = 1.5f;
        private GameObject[] _hearts;

        // Respawn coordinates
        private Vector2 _respawnPoint;

        // Death audio
        private AudioSource _deathAudio;

        private void Start()
        {
            // Set respawn point
            _respawnPoint = transform.position;

            // In RL: Hide hearts UI since lives system is disabled
            if (heartsParent != null)
                heartsParent.SetActive(false);

            // Get death audio
            _deathAudio = GetComponent<AudioSource>();
        }

        private void GenerateHearts()
        {
            if (heartsParent == null) return;
            _hearts = new GameObject[life];

            for (var i = 0; i < life; i++)
            {
                var heart = new GameObject();
                heart.AddComponent<SpriteRenderer>().sprite = heartTexture;
                var position = heartsParent.transform.position;
                heart.transform.position =
                    new Vector2(position.x + i * heartOffset, position.y);
                heart.transform.localScale = heartSpriteScale;
                _hearts[i] = heart;
            }
        }

        public bool Kill()
        {
            if (_deathAudio != null)
                _deathAudio.Play();

            life = 0;
            CancelInvoke();

            // In RL: 1 Death = 1 Episode. No 2-second wait timer (Invoke removed).
            // Reset happens instantly in 0.0 seconds.
            return true;
        }

        public void ResetLife()
        {
            life = 1;
            CancelInvoke();
        }

        private void DisableGameObject()
        {
            gameObject.SetActive(false);
        }
    }
}
