using GameHandler;
using GamePauseUi;
using Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LevelInputHandler
{
    public class LevelInputHandler : MonoBehaviour
    {
        public PlayerController playerController;
        public GamePauseUiHandler gamePauseUiHandler;
        public SettingsController.SettingsController settingsController;
        [SerializeField] private GameHandler.GameHandler gameHandler;

        private void Start()
        {
            Transform areaRoot = PacManArea.FindAreaRoot(transform);
            Transform searchRoot = areaRoot != null ? areaRoot : (transform.parent != null ? transform.parent : transform);

            if (gameHandler == null)
                gameHandler = searchRoot.GetComponentInChildren<GameHandler.GameHandler>(true) ?? GetComponentInParent<GameHandler.GameHandler>() ?? GameHandler.GameHandler.Instance;
            if (playerController == null)
                playerController = searchRoot.GetComponentInChildren<PlayerController>(true);
        }

        private void OnCancel()
        {
            if (GameStartHandler.Instance != null && GameStartHandler.Instance.enabled)
                GameStartHandler.Instance.TogglePause();

            var gh = gameHandler != null ? gameHandler : GameHandler.GameHandler.Instance;
            if (gh != null)
                gh.TogglePause();

            if (MusicHandler.MusicHandler.Instance != null)
                MusicHandler.MusicHandler.Instance.TogglePause();

            if (gamePauseUiHandler != null)
                gamePauseUiHandler.ResetInitialButton();
            if (settingsController != null)
                settingsController.ResetSliderSelection();
        }

        private void OnMove(InputValue value)
        {
            var inputDirection = value.Get<Vector2>();

            if (inputDirection.x != 0) inputDirection.y = 0; // Create a priority for x movement

            if (inputDirection != Vector2.zero)
                inputDirection = inputDirection.normalized; // Normalize the output to be 1 or -1 not floating values

            if (gamePauseUiHandler.gameObject.activeSelf)
                gamePauseUiHandler.MoveSelection(inputDirection);
            else if (settingsController.gameObject.activeSelf)
                settingsController.SetInputDirection(inputDirection);
            else
                playerController.SetInputDirection(inputDirection);
        }
    }
}
