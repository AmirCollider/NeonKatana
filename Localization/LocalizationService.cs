using System;
using UnityEngine;

namespace NeonKatana
{
    /// <summary>
    /// Holds which language the game is in, and remembers the choice between sessions. Anything
    /// that shows words listens to <see cref="LanguageChanged"/> and asks
    /// <see cref="CurrentLanguage"/> what to show.
    ///
    /// <para>
    /// It used to hand out the words as well, out of a <c>LocalizationTable</c> asset keyed by
    /// strings like <c>"menu.resume"</c>. That is gone: the translations live in
    /// <see cref="GameText"/>, in code, keyed by the Persian already in the scene — so there is no
    /// asset to keep in step with the labels and no key to invent for each one.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public class LocalizationService : MonoBehaviour
    {
        public static LocalizationService Instance { get; private set; }

        [Tooltip("Used the very first time the game runs, before the player has picked a language.")]
        [SerializeField] GameLanguage defaultLanguage = GameLanguage.English;

        public GameLanguage CurrentLanguage { get; private set; }

        /// <summary>Raised after the language changes, so everything showing words can refresh.</summary>
        public event Action LanguageChanged;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"A second {nameof(LocalizationService)} is in the scene. Keeping the first one.", this);
                enabled = false;
                return;
            }

            Instance = this;
            CurrentLanguage = PlayerProgress.GetLanguage(defaultLanguage);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Switches language, saves the choice, and tells everything to refresh.</summary>
        public void SetLanguage(GameLanguage language)
        {
            if (language == CurrentLanguage) return;

            CurrentLanguage = language;
            PlayerProgress.SetLanguage(language);
            LanguageChanged?.Invoke();
        }

        /// <summary>Convenient for a dropdown or a row of buttons in the inspector.</summary>
        public void SetLanguageByIndex(int index) => SetLanguage((GameLanguage)index);

        /// <summary>Moves to the next language and wraps around — what the flag button does.</summary>
        public void NextLanguage()
        {
            GameLanguage[] all = (GameLanguage[])Enum.GetValues(typeof(GameLanguage));
            int next = (Array.IndexOf(all, CurrentLanguage) + 1) % all.Length;

            SetLanguage(all[next]);
        }

        /// <summary>The current language as a BCP-47 tag — <c>en</c>, <c>fa</c> or <c>ja</c>.</summary>
        public string CurrentLanguageTag => PlayerProgress.LanguageTag(CurrentLanguage);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStaticState() => Instance = null;
    }
}
