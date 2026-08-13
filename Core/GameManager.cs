using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NeonKatana
{
    /// <summary>Why the current run ended.</summary>
    public enum GameOverCause
    {
        OutOfLives,
        BombSliced,

        /// <summary>
        /// The player left rather than lost — through the pause menu, by quitting, or because the
        /// phone took the app away. New on the end: Unity saves the number, so inserting a value
        /// ahead of the others would re-point every saved reference to them.
        /// </summary>
        Abandoned,
    }

    /// <summary>
    /// The single owner of a run: how many lives are left, whether it is paused or finished, how
    /// long it has lasted, and the scene-level buttons. Everything else reaches it through
    /// <see cref="Instance"/> instead of searching the scene for a tag.
    /// </summary>
    [DisallowMultipleComponent]
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Rules")]
        [SerializeField, Min(1)] int startingLives = 3;

        // Scene names live in Scenes.cs, not in a text field here: a typo should be a compile
        // error, not a button that quietly does nothing.

        /// <summary>How many lives a run begins with. Needed to work out how many were lost.</summary>
        public int StartingLives => startingLives;

        public int LivesRemaining { get; private set; }
        public bool IsGameOver { get; private set; }
        public bool IsPaused { get; private set; }

        /// <summary>True only while the player is actually playing: not paused, not finished.</summary>
        public bool IsPlaying => !IsGameOver && !IsPaused;

        /// <summary>Seconds of actual play in this run, pauses excluded. Also drives the difficulty.</summary>
        public float RunSeconds { get; private set; }

        /// <summary>How much of <see cref="RunSeconds"/> has already been written to the save.</summary>
        float savedSeconds;

        /// <summary>Fired with the number of lives left, every time one is lost.</summary>
        public event Action<int> LivesChanged;

        /// <summary>Fired with the new state whenever the game is paused or resumed.</summary>
        public event Action<bool> PauseChanged;

        /// <summary>Fired exactly once, when the run ends in a loss.</summary>
        public event Action<GameOverCause> GameOverReached;

        /// <summary>
        /// Fired exactly once, when the run ends because the player left rather than lost. Kept
        /// apart from <see cref="GameOverReached"/> because the two want different audiences: this
        /// one is for whoever writes the score down, and never for the lose screen.
        /// </summary>
        public event Action RunAbandoned;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"A second {nameof(GameManager)} is in the scene. Keeping the first one.", this);
                enabled = false;
                return;
            }

            Instance = this;
            LivesRemaining = startingLives;
            IsGameOver = false;
            IsPaused = false;
            Time.timeScale = 1f;
        }

        void Start() => StartCoroutine(CheckSomeoneIsListening());

        /// <summary>
        /// A screen sitting on a switched-off GameObject never gets to subscribe, and the run would
        /// then end with nothing on screen to say so. The check waits a frame because Unity gives
        /// no order to Start across objects — checking inside Start would accuse listeners that
        /// simply had not reached their own Start yet.
        /// </summary>
        IEnumerator CheckSomeoneIsListening()
        {
            yield return null;

            if (GameOverReached == null)
                Debug.LogWarning("Nothing is listening for the end of the run. Is the game-over screen's GameObject switched off?", this);
        }

        void Update()
        {
            // Unscaled, because pausing already stops this from counting at all.
            if (IsPlaying) RunSeconds += Time.unscaledDeltaTime;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // --- Lives ---

        /// <summary>Called when a fruit falls past the blade. Ends the run once no lives are left.</summary>
        public void LoseLife()
        {
            if (IsGameOver) return;

            LivesRemaining = Mathf.Max(0, LivesRemaining - 1);
            LivesChanged?.Invoke(LivesRemaining);

            if (LivesRemaining == 0) EndRun(GameOverCause.OutOfLives);
        }

        // --- Pausing ---

        public void SetPaused(bool paused)
        {
            if (IsGameOver || paused == IsPaused) return;

            IsPaused = paused;
            Time.timeScale = paused ? 0f : 1f;
            PauseChanged?.Invoke(paused);
        }

        public void TogglePause() => SetPaused(!IsPaused);

        public void Resume() => SetPaused(false);

        // --- Ending ---

        /// <summary>Ends the run. Safe to call more than once; only the first call is acted on.</summary>
        public void EndRun(GameOverCause cause)
        {
            if (IsGameOver) return;

            IsGameOver = true;
            IsPaused = false;
            Time.timeScale = 1f;

            BankPlaytime();
            FileTheRun();

            GameOverReached?.Invoke(cause);
        }

        /// <summary>
        /// Writes the run's score and count to the save, before anybody is told the run is over.
        /// <para>
        /// The order is the point. What <see cref="ProgressService"/> uploads is read back out of
        /// the device's save, so a run announced before its own numbers were written is a run
        /// reported one short — and the server takes the larger of the two totals, so the short
        /// one is not corrected until the next run happens to overtake it.
        /// </para>
        /// </summary>
        static void FileTheRun()
        {
            if (ScoreKeeper.Instance != null) ScoreKeeper.Instance.FileRun();
        }

        // --- Buttons ---
        // Kept so anything still pointing here keeps working; the real work is in GameActions,
        // which is also what a UIActionButton calls.

        public void RestartRun() => GameActions.Run(GameAction.RestartRun);

        public void LoadMainMenu() => GameActions.Run(GameAction.OpenMainMenu);

        public void QuitGame() => GameActions.Run(GameAction.QuitGame);

        // --- Leaving early ---

        /// <summary>
        /// Ends a run the player walked away from, so everything in it is filed exactly as it
        /// would have been had they lost.
        /// <para>
        /// Nothing did this. A run only ever ended through <see cref="EndRun"/>, which only ever
        /// ran when the last life went or a bomb was cut — so a player who beat their record and
        /// then pressed Exit, or quit the app, or was pushed to the background by Android and
        /// never came back, had beaten it for nothing. The score was not saved, the run was not
        /// counted, and nothing was queued for the server.
        /// </para>
        /// <para>
        /// Safe to call at any time, including on a run that has already finished or has not
        /// really started: a finished run is left alone, and a run worth nothing is not worth
        /// reporting as one.
        /// </para>
        /// </summary>
        public void AbandonRun()
        {
            if (IsGameOver)
            {
                BankPlaytime();
                return;
            }

            // Asked before IsGameOver is set, because it reads the run that is still in progress.
            bool worthReporting = WorthReporting();

            IsGameOver = true;
            IsPaused = false;

            // Leaving through the pause menu leaves the clock at zero otherwise, and the next
            // scene inherits it as a game that will not move.
            Time.timeScale = 1f;

            BankPlaytime();

            // A scene that was opened and immediately left is not a run. Reporting it would put an
            // empty entry in the outbox every time somebody looked at the game and changed their
            // mind, and the counters the server keeps would slowly fill with them.
            if (!worthReporting) return;

            FileTheRun();

            // Deliberately NOT GameOverReached. That event is what raises the lose screen, and a
            // player who pressed "back to menu" has not lost — they would get a flash of the
            // game-over panel on their way out. This one is only listened to by the two things
            // that keep score, both of which are safe to run while the scene is being torn down.
            RunAbandoned?.Invoke();
        }

        /// <summary>Whether anything happened in this run that is worth telling anybody about.</summary>
        bool WorthReporting()
        {
            bool scored = ScoreKeeper.Instance != null && ScoreKeeper.Instance.Score > 0;
            bool lostALife = LivesRemaining < startingLives;

            return scored || lostALife || RunSeconds >= 1f;
        }

        // --- Playtime ---

        /// <summary>Writes the unsaved part of this run away. Called before leaving the scene.</summary>
        public void BankPlaytimeNow() => BankPlaytime();

        /// <summary>
        /// Moves whatever part of this run has not been saved yet into the stored total. Android
        /// can kill the app without ever reaching OnApplicationQuit, so this also runs the moment
        /// the game goes to the background.
        /// <para>
        /// It deliberately does not reset <see cref="RunSeconds"/>: that clock also drives how hard
        /// the game has become, and backgrounding the app must not hand the player an easy round.
        /// </para>
        /// </summary>
        void BankPlaytime()
        {
            float unsaved = RunSeconds - savedSeconds;
            if (unsaved <= 0f) return;

            PlayerProgress.AddPlaySeconds(unsaved);
            PlayerProgress.Save();
            savedSeconds = RunSeconds;
        }

        /// <summary>
        /// The phone taking the app away. This may be the last moment the game ever gets — Android
        /// can reclaim a backgrounded app without another word — so everything earned so far is
        /// written to disk here.
        /// <para>
        /// It does <b>not</b> end the run, because most of the time this is a player glancing at a
        /// message and coming straight back, and ending their run for it would be its own bug.
        /// Nothing is lost by leaving it open: the record is written the moment it is beaten now,
        /// not when the run ends, so a run that never resumes has already saved the only part of
        /// itself the player would miss.
        /// </para>
        /// </summary>
        void OnApplicationPause(bool paused)
        {
            if (!paused) return;

            BankPlaytime();
            PlayerProgress.Save();
        }

        void OnApplicationQuit() => AbandonRun();

        /// <summary>
        /// The editor's stop button, a desktop window being closed, and the scene being unloaded.
        /// None of them reliably reach <see cref="OnApplicationQuit"/> in the middle of a run.
        /// </summary>
        void OnDisable() => AbandonRun();

        /// <summary>Clears the cached instance when play mode starts without a domain reload.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStaticState() => Instance = null;
    }
}
