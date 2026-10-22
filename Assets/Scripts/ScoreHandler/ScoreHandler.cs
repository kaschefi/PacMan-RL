using TMPro;
using UnityEngine;

namespace ScoreHandler
{
    public class ScoreHandler : MonoBehaviour
    {
        public string highScoreKey = "HighScore";
        public TextMeshProUGUI scoreText;
        public TextMeshProUGUI highScoreText;
        private bool _reachHighScore;
        private int _score;
        public static ScoreHandler Instance { get; private set; }

        private AudioSource _audioSource;

        public int CurrentScore => _score;

        private void Awake()
        {
            if (Instance == null)
                Instance = this;
        }

        private void Start()
        {
            _audioSource = GetComponent<AudioSource>();
            if (scoreText != null)
                scoreText.SetText("Score: " + _score);
            if (highScoreText != null)
                highScoreText.SetText("High Score: " + PlayerPrefs.GetInt(highScoreKey));
        }

        public void ResetScore()
        {
            _score = 0;
            if (scoreText != null)
                scoreText.SetText("Score: " + _score);
        }

        public void AddScore(int score)
        {
            _score += score;
            if (scoreText != null)
                scoreText.SetText("Score: " + _score);

            if (_score > PlayerPrefs.GetInt(highScoreKey))
            {
                PlayerPrefs.SetInt(highScoreKey, _score);
                if (highScoreText != null)
                    highScoreText.SetText("High Score: " + _score);

                if (!_reachHighScore && _audioSource != null)
                    _audioSource.Play();
                _reachHighScore = true;
            }
        }

        public void UpdateHighScore()
        {
            if (_score <= PlayerPrefs.GetInt(highScoreKey)) return;
            PlayerPrefs.SetInt(highScoreKey, _score);
        }
    }
}
