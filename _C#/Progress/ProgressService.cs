using System;
using System.Collections.Generic;
using UnityEngine;

namespace NeonKatana
{
    /// <summary>
    /// Watches runs finish and gets them to wherever progress is kept. The device save is always
    /// written first and is treated as the truth; the server is an extra that may be missing, slow
    /// or signed out, so runs it could not take are held in an outbox and retried later.
    /// </summary>
    [DisallowMultipleComponent]
    public class ProgressService : MonoBehaviour
    {
        public static ProgressService Instance { get; private set; }

        [Header("Server")]
        [Tooltip("Leave empty to stay offline. The game plays exactly the same either way.")]
        [SerializeField] string serverBaseUrl = string.Empty;
        [Tooltip("The id this game is registered under on the server.")]
        [SerializeField] string gameId = "neon-katana";
        [SerializeField, Min(1)] int requestTimeoutSeconds = 10;

        [Header("Outbox")]
        [Tooltip("How many unsent runs to keep. The oldest are dropped once it is full.")]
        [SerializeField, Min(1)] int outboxLimit = 20;

        const string OutboxKey = "NeonKatana.Outbox";

        readonly List<RunResult> outbox = new List<RunResult>();

        IProgressBackend backend;
        bool sending;

        /// <summary>
        /// Hands back the current Google id_token, or null when nobody is signed in. Sign-in is
        /// deliberately not built in here — set this from whichever flow the app ends up using.
        /// </summary>
        public Func<string> IdTokenProvider { get; set; }

        /// <summary>The signed-in player's id. Must match the one the token resolves to.</summary>
        public Func<string> PlayerIdProvider { get; set; }

        /// <summary>True when the server is configured and reachable right now.</summary>
        public bool IsOnline => backend != null && backend.IsAvailable;

        /// <summary>How many finished runs are still waiting to reach the server.</summary>
        public int PendingRuns => outbox.Count;

        /// <summary>
        /// The player's record as the server last described it, or null when it has not been read
        /// yet. Held here rather than fetched by each label that wants a piece of it: the chosen
        /// name, the record and the picture all come out of one request.
        /// </summary>
        public PlayerProfile Profile { get; private set; }

        /// <summary>Raised when <see cref="Profile"/> arrives or is cleared.</summary>
        public event Action ProfileChanged;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"A second {nameof(ProgressService)} is in the scene. Keeping the first one.", this);
                enabled = false;
                return;
            }

            Instance = this;
            backend = BuildBackend();
            LoadOutbox();
        }

        void Start()
        {
            if (GameManager.Instance != null) GameManager.Instance.GameOverReached += OnRunFinished;

            FlushOutbox();
        }

        void OnDestroy()
        {
            if (GameManager.Instance != null) GameManager.Instance.GameOverReached -= OnRunFinished;
            if (Instance == this) Instance = null;
        }

        IProgressBackend BuildBackend()
        {
            if (string.IsNullOrWhiteSpace(serverBaseUrl)) return new LocalProgressBackend();

            return new AmirColliderBackend(
                host: this,
                baseUrl: serverBaseUrl,
                gameId: gameId,
                idTokenProvider: () => IdTokenProvider?.Invoke(),
                playerIdProvider: () => PlayerIdProvider?.Invoke(),
                timeoutSeconds: requestTimeoutSeconds);
        }

        void OnRunFinished(GameOverCause cause)
        {
            GameManager game = GameManager.Instance;
            ScoreKeeper score = ScoreKeeper.Instance;

            RunResult run = RunResult.From(
                score: score != null ? score.Score : 0,
                durationSeconds: game != null ? game.RunSeconds : 0f,
                livesLost: game != null ? Mathf.Max(0, game.StartingLives - game.LivesRemaining) : 0,
                cause: cause);

            Enqueue(run);
            FlushOutbox();
        }

        void Enqueue(RunResult run)
        {
            outbox.Add(run);

            // Losing the oldest beats letting a device that has been offline for weeks grow a
            // save file without a ceiling.
            if (outbox.Count > outboxLimit) outbox.RemoveRange(0, outbox.Count - outboxLimit);

            SaveOutbox();
        }

        /// <summary>Sends the queued runs, oldest first, one at a time.</summary>
        public void FlushOutbox()
        {
            if (sending || outbox.Count == 0 || !IsOnline) return;

            sending = true;
            RunResult next = outbox[0];

            backend.SubmitRun(next, PlayerProfile.FromLocalSave(), (succeeded, error) =>
            {
                sending = false;

                if (!succeeded)
                {
                    // Left in the outbox on purpose: the next flush will try it again.
                    Debug.Log($"A finished run is waiting to be sent: {error}");
                    return;
                }

                outbox.Remove(next);
                SaveOutbox();

                FlushOutbox();
            });
        }

        public void LoadLeaderboard(Action<LeaderboardEntry[], string> onDone) =>
            backend.LoadLeaderboard(onDone);

        public void LoadProfile(Action<PlayerProfile, string> onDone) =>
            backend.LoadProfile(onDone);

        /// <summary>
        /// Goes and reads the player's record, and tells everyone showing part of it. Nothing did
        /// this before, which is why signing in left the menu still showing whatever it had.
        /// </summary>
        public void RefreshProfile()
        {
            if (!IsOnline) return;

            backend.LoadProfile((profile, error) =>
            {
                if (profile == null)
                {
                    // Not worth interrupting the menu over: the local save is still the truth, and
                    // the message is in the log for whoever is looking for it.
                    Debug.Log($"The player's record could not be read: {error}");
                    return;
                }

                Profile = profile;
                ProfileChanged?.Invoke();
            });
        }

        /// <summary>Call once sign-in finishes, so anything held while signed out goes out now.</summary>
        public void OnSignedIn()
        {
            FlushOutbox();
            RefreshProfile();
        }

        /// <summary>Call on signing out: what is on screen belonged to whoever has just left.</summary>
        public void OnSignedOut()
        {
            Profile = null;
            ProfileChanged?.Invoke();
        }

        // --- Outbox storage ---

        void LoadOutbox()
        {
            outbox.Clear();

            string stored = PlayerPrefs.GetString(OutboxKey, string.Empty);
            if (string.IsNullOrEmpty(stored)) return;

            try
            {
                Outbox saved = JsonUtility.FromJson<Outbox>(stored);
                if (saved?.runs != null) outbox.AddRange(saved.runs);
            }
            catch (Exception failure)
            {
                Debug.LogWarning($"The outbox could not be read and was cleared: {failure.Message}", this);
                PlayerPrefs.DeleteKey(OutboxKey);
            }
        }

        void SaveOutbox()
        {
            PlayerPrefs.SetString(OutboxKey, JsonUtility.ToJson(new Outbox { runs = outbox.ToArray() }));
            PlayerPrefs.Save();
        }

        [Serializable]
        class Outbox
        {
            public RunResult[] runs = Array.Empty<RunResult>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStaticState() => Instance = null;
    }
}
