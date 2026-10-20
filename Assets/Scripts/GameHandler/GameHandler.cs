using System;
using System.Collections;
using Collectibles;
using Door;
using GamePauseUi;
using Ghosts;
using Player;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace GameHandler
{
    public class GameHandler : MonoBehaviour
    {
        // Singleton
        public static GameHandler Instance { get; private set; }

        // Events for RL Agent (1 Death = 1 Episode)
        public event Action OnPlayerDied;
        public event Action OnBoardCleared;

        // Current Game Mode
        public GhostMode GameGhostsMode { get; private set; }

        // Ghosts
        private GhostAiMovement[] _ghosts;

        // Player
        private PlayerController _player;

        // Pac-gum and Collectibles
        private int _pacGumCount;
        private CollectiblePacGum[] _allPacGums;
        private CollectiblePower[] _allPowerPellets;
        private int _totalPacGumCount;

        // Timer for scatter and chase
        public uint[] ghostsModeTimes = { 7, 20, 7, 20, 5, 20, 5 };
        private int _ghostsModeTimesIndex;
        private float _switcherModeTimer;
        private bool _allTimersPaused;

        // Timer for ghost frightened
        public bool IsFrightenedMode => _switcherModeTimerPaused;
        public uint frightenedTime = 10;
        private float _frightenTimer;
        private bool _switcherModeTimerPaused;

        // Door
        public DoorHandler doorHandler;

        // Current Ghost mode text
        public TextMeshProUGUI currentModeText;

        // UI elements
        public TextMeshProUGUI gameOverText;
        public TextMeshProUGUI winText;
        public TextMeshProUGUI gamePausedText;

        public GameObject pauseMenuUi;

        public GamePauseUiHandler gamePauseUiHandler;

        public int GhostCountEaten { get; set; }


        #region Awake Singleton
 
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
            EnsureInitialized();
        }

        public void EnsureInitialized()
        {
            if (_ghosts == null || _ghosts.Length == 0)
                _ghosts = FindObjectsOfType<GhostAiMovement>(true);
            if (_player == null)
                _player = FindObjectOfType<PlayerController>(true);
            if (_allPacGums == null || _allPacGums.Length == 0)
            {
                _allPacGums = FindObjectsOfType<CollectiblePacGum>(true);
                _totalPacGumCount = _allPacGums != null ? _allPacGums.Length : 0;
                _pacGumCount = _totalPacGumCount;
            }
            if (_allPowerPellets == null || _allPowerPellets.Length == 0)
                _allPowerPellets = FindObjectsOfType<CollectiblePower>(true);
        }

        #endregion

        #region Start and Update

        private void Start()
        {
            EnsureInitialized();
            if (winText != null) winText.enabled = false;
            if (gamePausedText != null) gamePausedText.enabled = false;
            if (gameOverText != null) gameOverText.enabled = false;
            UpdateGhostsMode();
            if (gamePauseUiHandler != null) gamePauseUiHandler.Reset();
            if (pauseMenuUi != null) pauseMenuUi.SetActive(false);
        }

        private void Update()
        {
            if ((GameStartHandler.Instance != null && GameStartHandler.Instance.enabled) || _allTimersPaused)
                return;

            if (!_switcherModeTimerPaused)
            {
                // If the index is out of bounds, we are in the last mode, which is chase mode.
                if (_ghostsModeTimesIndex >= ghostsModeTimes.Length)
                {
                    if (GameGhostsMode != GhostMode.Chase)
                    {
                        // If we are not in chase mode, switch to chase mode.
                        SwitchingChaseMode();
                        PrintCurrentMode();
                    }

                    return;
                }

                // Update the timer and check if it's time to switch mode.
                _switcherModeTimer += Time.deltaTime;
                if (_switcherModeTimer >= ghostsModeTimes[_ghostsModeTimesIndex])
                {
                    _ghostsModeTimesIndex++;
                    _switcherModeTimer = 0;
                    UpdateGhostsMode();
                }

                // Update the current mode text.
                PrintCurrentMode();
            }
            else
            {
                // Update the timer and check if it's time to end the frighten mode (10 seconds)
                _frightenTimer += Time.deltaTime;
                if (_frightenTimer >= frightenedTime)
                {
                    _frightenTimer = 0;
                    _switcherModeTimerPaused = false;
                    if (doorHandler != null) doorHandler.CloseDoor();
                    UpdateGhostsMode();
                }

                PrintFrightenMode();
            }
        }

        #endregion

        #region Ghosts Mode

        private void UpdateGhostsMode()
        {
            if (_ghostsModeTimesIndex % 2 != 0)
                SwitchingChaseMode();
            else
                SwitchingScatterMode();

            if (GameStartHandler.Instance == null || !GameStartHandler.Instance.enabled)
            {
                if (MusicHandler.MusicHandler.Instance != null)
                {
                    MusicHandler.MusicHandler.Instance.StopMusic();
                    MusicHandler.MusicHandler.Instance.PlayGhostChase();
                }
            }
        }

        private void SwitchingChaseMode()
        {
            GameGhostsMode = GhostMode.Chase;
            foreach (var ghost in _ghosts)
                ghost.SetGhostMode(GhostMode.Chase);
        }

        private void SwitchingScatterMode()
        {
            GameGhostsMode = GhostMode.Scatter;
            foreach (var ghost in _ghosts)
                ghost.SetGhostMode(GhostMode.Scatter);
        }

        public void SwitchingFrightenedMode()
        {
            GhostCountEaten = 0;
            _frightenTimer = 0; // Reset the 10-second timer
            _switcherModeTimerPaused = true;

            if (_ghosts != null)
            {
                foreach (var ghost in _ghosts)
                {
                    if (ghost != null)
                        ghost.SetGhostMode(GhostMode.Frightened);
                }
            }

            if (doorHandler != null)
                doorHandler.OpenDoor();

            if (MusicHandler.MusicHandler.Instance != null)
            {
                MusicHandler.MusicHandler.Instance.StopMusic();
                MusicHandler.MusicHandler.Instance.PlayPacmanChase();
            }
        }

        #endregion

        #region Print text for current mode

        private void PrintCurrentMode()
        {
            if (currentModeText == null) return;

            // If we are in the last mode, which is chase mode.
            // or if the last mode in the array is chase mode then we are in final chase mode.
            if (_ghostsModeTimesIndex >= ghostsModeTimes.Length ||
                (_ghostsModeTimesIndex == ghostsModeTimes.Length - 1 && GameGhostsMode == GhostMode.Chase))
            {
                currentModeText.text = "Chase";
                return;
            }

            // Print the current mode.
            currentModeText.text =
                $"{GameGhostsMode} for {ghostsModeTimes[_ghostsModeTimesIndex] - _switcherModeTimer:F2}s";
        }

        private void PrintFrightenMode()
        {
            if (currentModeText != null)
                currentModeText.text = $"Frighten for {Mathf.Max(0, frightenedTime - _frightenTimer):F2}s";
        }

        #endregion

        #region Reset functions

        public void KillPlayer()
        {
            // Stop music
            if (MusicHandler.MusicHandler.Instance != null)
                MusicHandler.MusicHandler.Instance.StopMusic();

            // Reset the player destination
            if (_player != null)
                _player.Immobilize();

            // Reset the ghost mode.
            _allTimersPaused = true;

            // Deactivate the ghosts
            if (_ghosts != null)
            {
                foreach (var ghost in _ghosts)
                {
                    if (ghost != null)
                    {
                        ghost.gameObject.SetActive(false);
                        if (ghost.bodyRenderer != null) ghost.bodyRenderer.enabled = true;
                    }
                }
            }

            // Exit frighten timer
            _switcherModeTimerPaused = false;

            // In RL: 1 Death = 1 Episode (Lives system is disabled)
            var playerLife = FindObjectOfType<PlayerLife>();
            if (playerLife != null)
            {
                playerLife.Kill();
            }

            if (ScoreHandler.ScoreHandler.Instance != null)
                ScoreHandler.ScoreHandler.Instance.UpdateHighScore();

            // Trigger death event for RL agent (EndEpisode + negative reward)
            OnPlayerDied?.Invoke();

            // In RL: Reset entire board (all pellets + all ghosts) fresh in 0.0s (no 3s Invoke wait)
            ResetBoard();
        }

        public void ResetGhostsAndPlayer()
        {
            // In RL: 1 Death = 1 Episode, so reset the entire board
            ResetBoard();
        }

        /// <summary>
        /// Resets the entire board (all pellets + all ghosts + player + mode timers + score) fresh in 0.0 seconds.
        /// Can be called directly or from RL Agent's OnEpisodeBegin().
        /// </summary>
        public void ResetBoard()
        {
            EnsureInitialized();

            // Cancel any pending invokes
            CancelInvoke();

            // Reset ghosts mode timers
            _ghostsModeTimesIndex = 0;
            _switcherModeTimer = 0f;
            _frightenTimer = 0f;
            _switcherModeTimerPaused = false;
            _allTimersPaused = false;
            GhostCountEaten = 0;

            // Reset UI
            if (gameOverText != null) gameOverText.enabled = false;
            if (winText != null) winText.enabled = false;

            // Reset all pellets (pac-gums and power pellets)
            if (_allPacGums != null)
            {
                foreach (var pacGum in _allPacGums)
                {
                    if (pacGum != null) pacGum.ResetPellet();
                }
            }

            if (_allPowerPellets != null)
            {
                foreach (var power in _allPowerPellets)
                {
                    if (power != null) power.ResetPellet();
                }
            }

            _pacGumCount = _totalPacGumCount;

            // Reset ghosts
            if (_ghosts != null)
            {
                foreach (var ghost in _ghosts)
                {
                    if (ghost != null)
                    {
                        ghost.gameObject.SetActive(true);
                        ghost.enabled = true;
                        if (ghost.bodyAnimator != null) ghost.bodyAnimator.enabled = true;
                        ghost.Reset();
                    }
                }
            }

            // Reset player
            if (_player != null)
            {
                _player.gameObject.SetActive(true);
                _player.enabled = true;
                if (_player.animator != null)
                {
                    _player.animator.enabled = true;
                    _player.animator.SetBool(AnimationsConstants.PlayerIsDead, false);
                    _player.animator.SetBool(AnimationsConstants.PlayerIsMoving, false);
                }
                _player.Reset();
            }

            // Reset player life state
            var playerLife = FindObjectOfType<PlayerLife>();
            if (playerLife != null)
            {
                playerLife.ResetLife();
            }

            // Reset door
            if (doorHandler != null)
            {
                doorHandler.CloseDoor();
            }

            // Reset score for the new episode
            if (ScoreHandler.ScoreHandler.Instance != null)
            {
                ScoreHandler.ScoreHandler.Instance.ResetScore();
            }

            // Play music if available
            if (MusicHandler.MusicHandler.Instance != null)
            {
                MusicHandler.MusicHandler.Instance.PlayGhostChase();
            }

            // Update ghosts mode
            UpdateGhostsMode();
        }

        public void ActivateGhostsAndPlayer()
        {
            foreach (var ghost in _ghosts)
            {
                if (ghost != null)
                {
                    if (ghost.bodyAnimator != null) ghost.bodyAnimator.enabled = true;
                    ghost.enabled = true;
                }
            }

            if (_player != null)
            {
                _player.enabled = true;
                if (_player.animator != null) _player.animator.enabled = true;
            }

            if (doorHandler != null)
                doorHandler.CloseDoor();

            if (MusicHandler.MusicHandler.Instance != null)
                MusicHandler.MusicHandler.Instance.PlayGhostChase();
        }

        public void RestartGame()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            _allTimersPaused = false;
            TogglePause();
        }

        public void DecrementPacGumNumber()
        {
            _pacGumCount--;
            if (_pacGumCount <= 0) NextLevel();
        }

        private void NextLevel()
        {
            // In RL: Level cleared = episode success. Reset fresh in 0.0s (no 6s wait timer)
            OnBoardCleared?.Invoke();
            ResetBoard();
        }

        public void TogglePause()
        {
            if (winText.enabled || gameOverText.enabled) return;

            if (gamePausedText.enabled)
            {
                ResumeGame();
                gamePauseUiHandler.Reset();
            }
            else
            {
                PauseGame();
            }
        }


        private void PauseGame()
        {
            Time.timeScale = 0;
            Time.fixedDeltaTime = 0;
            gamePausedText.enabled = true;
            pauseMenuUi.SetActive(true);
        }

        private void ResumeGame()
        {
            Time.timeScale = TimeConstants.TimeScaleNormal;
            Time.fixedDeltaTime = TimeConstants.FixedDeltaTime;
            gamePausedText.enabled = false;
            pauseMenuUi.SetActive(false);
        }

        public void GoToMainMenu()
        {
            Time.timeScale = TimeConstants.TimeScaleNormal;
            Time.fixedDeltaTime = TimeConstants.FixedDeltaTime;
            SceneManager.LoadScene(SceneNameConstants.TitleScreen);
        }

        #endregion
    }
}
