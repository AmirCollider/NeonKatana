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

        /// <summary>Fired exactly once, when the run ends.</summary>
        public event Action<GameOverCause> GameOverReached;

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
            GameOverReached?.Invoke(cause);
        }

        // --- Buttons ---
        // Kept so anything still pointing here keeps working; the real work is in GameActions,
        // which is also what a UIActionButton calls.

        public void RestartRun() => GameActions.Run(GameAction.RestartRun);

        public void LoadMainMenu() => GameActions.Run(GameAction.OpenMainMenu);

        public void QuitGame() => GameActions.Run(GameAction.QuitGame);

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

        void OnApplicationPause(bool paused)
        {
            if (paused) BankPlaytime();
        }

        void OnApplicationQuit() => BankPlaytime();

        /// <summary>Clears the cached instance when play mode starts without a domain reload.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStaticState() => Instance = null;
    }
}
