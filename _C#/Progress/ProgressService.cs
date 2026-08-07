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

        /// <summary>The run this scene belongs to, captured so the handlers come off it again.</summary>
        GameManager game;

        /// <summary>One run per scene. Losing and then leaving must not queue the same run twice.</summary>
        bool recorded;

        /// <summary>
        /// Hands back the current Google id_token, or null when nobody is signed in. Sign-in is
        /// deliberately not built in here — set this from whichever flow the app ends up using.
        /// </summary>
        public Func<string> IdTokenProvider { get; set; }

        /// <summary>The signed-in player's id. Must match the one the token resolves to.</summary>
        public Func<string> PlayerIdProvider { get; set; }

        /// <summary>
        /// The token to send, from whoever can supply one.
        /// <para>
        /// The provider is set by <see cref="SignInService"/>, which only exists in the menu — so
        /// the copy of this service sitting on <c>LevelManager</c> in the game scene had no
        /// provider, no token, and therefore no way to send anything. Every finished run went into
        /// the outbox to wait for a menu that had itself come back signed out. Falling through to
        /// <see cref="SignInSession"/> is what lets a run upload from the scene it happened in.
        /// </para>
        /// </summary>
        string CurrentIdToken
        {
            get
            {
                string fromProvider = IdTokenProvider?.Invoke();

                return !string.IsNullOrEmpty(fromProvider) ? fromProvider : SignInSession.IdToken;
            }
        }

        /// <summary>The player id to write under, from whoever can supply one.</summary>
        string CurrentPlayerId
        {
            get
            {
                string fromProvider = PlayerIdProvider?.Invoke();

                return !string.IsNullOrEmpty(fromProvider) ? fromProvider : SignInSession.PlayerId;
            }
        }

        /// <summary>True when the server is configured and reachable right now.</summary>
        public bool IsOnline => backend != null && backend.IsAvailable;

        /// <summary>How many finished runs are still waiting to reach the server.</summary>
        public int PendingRuns => outbox.Count;

        /// <summary>
        /// The player's record as the server last described it, or null when it has not been read
        /// yet. Held here rather than fetched by each label that wants a piece of it: the chosen
        /// name, the record and the picture all come out of one request.
        /// <para>
        /// Held statically rather than on the component, for the same reason
        /// <see cref="SignInSession"/> exists: this service is rebuilt by every scene load, and a
        /// record that has to be fetched again each time is a menu that draws itself with no name,
        /// no picture and no record until the network answers — every single time the player comes
        /// back from a run.
        /// </para>
        /// </summary>
        public PlayerProfile Profile
        {
            get => cachedProfile;
            private set => cachedProfile = value;
        }

        static PlayerProfile cachedProfile;

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
            // Captured, not looked up again in OnDestroy. Instance is a static, and by the time
            // this object is torn down it may already have been cleared or replaced by the next
            // scene's — in which case the handler below would never come off, and the manager
            // would keep calling into a destroyed component.
            game = GameManager.Instance;

            if (game != null)
            {
                game.GameOverReached += OnRunFinished;
                game.RunAbandoned += OnRunAbandoned;
            }

            FlushOutbox();
        }

        void OnDestroy()
        {
            if (game != null)
            {
                game.GameOverReached -= OnRunFinished;
                game.RunAbandoned -= OnRunAbandoned;
            }

            if (Instance == this) Instance = null;
        }

        IProgressBackend BuildBackend()
        {
            if (string.IsNullOrWhiteSpace(serverBaseUrl)) return new LocalProgressBackend();

            return new AmirColliderBackend(
                host: this,
                baseUrl: serverBaseUrl,
                gameId: gameId,
                idTokenProvider: () => CurrentIdToken,
                playerIdProvider: () => CurrentPlayerId,
                timeoutSeconds: requestTimeoutSeconds);
        }

        void OnRunFinished(GameOverCause cause) => RecordRun(cause);

        /// <summary>
        /// A run the player walked out of. Recorded exactly like one they lost — it is the same
        /// run, and the score in it is the same score. Nothing recorded these before, so a record
        /// broken on a run that ended at the pause menu never reached the server at all.
        /// </summary>
        void OnRunAbandoned() => RecordRun(GameOverCause.Abandoned);

        void RecordRun(GameOverCause cause)
        {
            if (recorded) return;
            recorded = true;

            GameManager manager = game != null ? game : GameManager.Instance;
            ScoreKeeper score = ScoreKeeper.Instance;

            RunResult run = RunResult.From(
                score: score != null ? score.Score : 0,
                durationSeconds: manager != null ? manager.RunSeconds : 0f,
                livesLost: manager != null ? Mathf.Max(0, manager.StartingLives - manager.LivesRemaining) : 0,
                cause: cause);

            Enqueue(run);
            FlushOutbox();
        }

        void Enqueue(RunResult run)
        {
            // Stamped with whoever was signed in when it happened. An unsent run belongs to that
            // person, and a phone that changes hands must not post it under the next player's name.
            run.playerId = CurrentPlayerId ?? PlayerProgress.Owner ?? string.Empty;

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

            // A coroutine cannot be started on a component that is on its way out, and the flush
            // is triggered from scene teardown now that leaving mid-run counts as a run. The queue
            // is on disk; whichever service starts next picks it up.
            if (!isActiveAndEnabled) return;

            string owner = CurrentPlayerId;
            RunResult next = NextRunFor(owner);

            if (next == null) return;

            sending = true;

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

        /// <summary>
        /// The oldest queued run that belongs to whoever is signed in now, skipping any left
        /// behind by a previous account. Runs stamped before this field existed carry no id and
        /// are treated as the current player's, which is what they were.
        /// </summary>
        RunResult NextRunFor(string owner)
        {
            foreach (RunResult run in outbox)
            {
                if (string.IsNullOrEmpty(run.playerId)) return run;
                if (string.Equals(run.playerId, owner, StringComparison.OrdinalIgnoreCase)) return run;
            }

            return null;
        }

        public void LoadLeaderboard(Action<LeaderboardEntry[], string> onDone) =>
            backend.LoadLeaderboard(onDone);

        public void LoadProfile(Action<PlayerProfile, string> onDone) =>
            backend.LoadProfile(onDone);

        /// <summary>
        /// Goes and reads the player's record, and tells everyone showing part of it. Nothing did
        /// this before, which is why signing in left the menu still showing whatever it had.
        /// </summary>
        public void RefreshProfile() => RefreshProfile(null);

        /// <summary>
        /// The same, telling <paramref name="onDone"/> when the record has landed — or when it
        /// turned out there was none to land. Anything that must not run on a stale total waits
        /// for this rather than firing alongside it.
        /// </summary>
        public void RefreshProfile(Action onDone)
        {
            if (!IsOnline)
            {
                onDone?.Invoke();
                return;
            }

            backend.LoadProfile((profile, error) =>
            {
                if (profile == null)
                {
                    // Not worth interrupting the menu over: the local save is still the truth, and
                    // the message is in the log for whoever is looking for it.
                    Debug.Log($"The player's record could not be read: {error}");
                    onDone?.Invoke();
                    return;
                }

                Profile = profile;

                // Before anything is sent back. What goes to the server is this device's running
                // total, not the difference — so a device that has just been reinstalled has to
                // learn the real history before it reports one, or it reports a history of one
                // run and the server is asked to believe it.
                PlayerProgress.AdoptIfHigher(profile.highScore, profile.gamesPlayed, profile.totalPlayTime);

                ProfileChanged?.Invoke();
                onDone?.Invoke();
            });
        }

        /// <summary>
        /// Call once sign-in finishes, so anything held while signed out goes out now.
        /// <para>
        /// The record is read <b>first</b>, and the outbox is only sent once it has arrived. These
        /// two used to run the other way round, and the order was the whole difference between a
        /// player's history surviving a reinstall and being overwritten by it: what the outbox
        /// sends is this device's running total, so sending it before the real history is known is
        /// sending a total that is wrong.
        /// </para>
        /// </summary>
        public void OnSignedIn() => RefreshProfile(FlushOutbox);

        /// <summary>Call on signing out: what is on screen belonged to whoever has just left.</summary>
        public void OnSignedOut()
        {
            Profile = null;
            ProfileChanged?.Invoke();
        }

        /// <summary>
        /// Call when a <b>different</b> account signs in. Puts away everything the previous one
        /// left: their record, and the runs of theirs still waiting to go out.
        /// <para>
        /// Their record mattered most. It was kept until a fresh one arrived, so the menu went on
        /// showing the first account's name, picture and high score after the second had signed
        /// in — and if the new record failed to load, it showed them for good. Their queued runs
        /// mattered more quietly: the outbox is sent with whatever token is current, so runs the
        /// first player made were about to be posted as the second player's scores.
        /// </para>
        /// </summary>
        public void OnAccountChanged(string newPlayerId)
        {
            Profile = null;

            int carriedOver = outbox.RemoveAll(run =>
                !string.IsNullOrEmpty(run.playerId) &&
                !string.Equals(run.playerId, newPlayerId, StringComparison.OrdinalIgnoreCase));

            // Runs from before this stamp existed have no id on them and cannot be told apart, so
            // they go too. One lost run beats one run credited to the wrong person.
            carriedOver += outbox.RemoveAll(run => string.IsNullOrEmpty(run.playerId));

            if (carriedOver > 0)
            {
                Debug.Log($"{carriedOver} unsent run(s) belonged to the previous account and were dropped.");
                SaveOutbox();
            }

            recorded = false;

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
