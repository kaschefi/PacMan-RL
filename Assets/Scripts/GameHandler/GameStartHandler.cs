using UnityEngine;

namespace GameHandler
{
    public class GameStartHandler : MonoBehaviour
    {
        public static GameStartHandler Instance { get; private set; }
        public AudioSource StartAudioSource { get; private set; }
        public bool IsStarterPaused { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
                Destroy(this);
            else
                Instance = this;
        }

        private void Start()
        {
            enabled = false;

            StartAudioSource = GetComponent<AudioSource>();
            if (StartAudioSource != null)
                StartAudioSource.Play();

            // In RL: Activate ghosts and player immediately in 0.0s without waiting for intro music to finish
            if (GameHandler.Instance != null)
                GameHandler.Instance.ActivateGhostsAndPlayer();
        }

        private void Update()
        {
            // No-op in RL mode: Update was only used to wait for StartAudioSource to finish playing
        }

        public void TogglePause()
        {
            if (StartAudioSource.isPlaying)
                StartAudioSource.Pause();
            else
                StartAudioSource.Play();

            IsStarterPaused = !IsStarterPaused;
        }
    }
}
