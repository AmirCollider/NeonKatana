using System;
using UnityEngine;

namespace NeonKatana
{
    /// <summary>
    /// Counts the run's score and files the record away when the run ends. Kept apart from
    /// <see cref="GameManager"/> so scoring rules can change without touching how a run works.
    /// </summary>
    [DisallowMultipleComponent]
    public class ScoreKeeper : MonoBehaviour
    {
        public static ScoreKeeper Instance { get; private set; }

        public int Score { get; private set; }

        /// <summary>True once this run has passed the saved record.</summary>
        public bool BeatTheRecord { get; private set; }

        public int HighScore => Mathf.Max(PlayerProgress.HighScore, Score);

        /// <summary>Raised with the new total every time the score changes.</summary>
        public event Action<int> ScoreChanged;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"A second {nameof(ScoreKeeper)} is in the scene. Keeping the first one.", this);
                enabled = false;
                return;
            }

            Instance = this;
        }

        void Start()
        {
            if (GameManager.Instance != null) GameManager.Instance.GameOverReached += SaveRecord;

            ScoreChanged?.Invoke(Score);
        }

        void OnDestroy()
        {
            if (GameManager.Instance != null) GameManager.Instance.GameOverReached -= SaveRecord;
            if (Instance == this) Instance = null;
        }

        public void Add(int points)
        {
            if (points == 0) return;

            Score = Mathf.Max(0, Score + points);
            if (Score > PlayerProgress.HighScore) BeatTheRecord = true;

            ScoreChanged?.Invoke(Score);
        }

        void SaveRecord(GameOverCause cause)
        {
            PlayerProgress.TrySetHighScore(Score);
            PlayerProgress.CountRun();
            PlayerProgress.Save();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStaticState() => Instance = null;
    }
}
