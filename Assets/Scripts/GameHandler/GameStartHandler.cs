using UnityEngine;

namespace GameHandler
{
    public class GameStartHandler : MonoBehaviour
    {
        public static GameStartHandler Instance { get; private set; }
        public AudioSource StartAudioSource { get; private set; }
        public bool IsStarterPaused { get; private set; }

        [SerializeField] private GameHandler gameHandler;

        private void Awake()
        {
            if (Instance == null)
                Instance = this;
        }

        private void Start()
        {
            enabled = false;

            StartAudioSource = GetComponent<AudioSource>();
            if (StartAudioSource != null)
                StartAudioSource.Play();

            if (gameHandler == null)
            {
                Transform areaRoot = PacManArea.FindAreaRoot(transform);
                Transform searchRoot = areaRoot != null ? areaRoot : (transform.parent != null ? transform.parent : transform);
                gameHandler = searchRoot.GetComponentInChildren<GameHandler>(true) ?? GetComponentInParent<GameHandler>() ?? GameHandler.Instance;
            }

            // In RL: Activate ghosts and player immediately in 0.0s without waiting for intro music to finish
            if (gameHandler != null)
                gameHandler.ActivateGhostsAndPlayer();
        }

        private void Update()
        {
            // No-op in RL mode: Update was only used to wait for StartAudioSource to finish playing
        }

        public void TogglePause()
        {
            if (StartAudioSource != null)
            {
                if (StartAudioSource.isPlaying)
                    StartAudioSource.Pause();
                else
                    StartAudioSource.Play();
            }

            IsStarterPaused = !IsStarterPaused;
        }
    }
}
