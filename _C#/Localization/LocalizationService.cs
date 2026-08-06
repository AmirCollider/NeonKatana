using System;
using System.Collections.Generic;
using UnityEngine;

namespace NeonKatana
{
    /// <summary>
    /// Hands out the text for whichever language is selected, and remembers the choice between
    /// sessions. Labels ask it for a key rather than holding words of their own, so switching
    /// language is a single call that every label reacts to.
    /// </summary>
    [DisallowMultipleComponent]
    public class LocalizationService : MonoBehaviour
    {
        public static LocalizationService Instance { get; private set; }

        [SerializeField] LocalizationTable table;
        [Tooltip("Used the very first time the game runs, before the player has picked a language.")]
        [SerializeField] GameLanguage defaultLanguage = GameLanguage.English;

        Dictionary<string, LocalizationTable.Entry> lookup;

        public GameLanguage CurrentLanguage { get; private set; }

        /// <summary>Raised after the language changes, so every label can refresh itself.</summary>
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
            lookup = table != null ? table.BuildLookup() : new Dictionary<string, LocalizationTable.Entry>();

            if (table == null)
                Debug.LogWarning($"{nameof(LocalizationService)} has no table, so every label will show its own key.", this);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// The text for a key in the current language. Missing keys come back as the key itself,
        /// which makes them obvious on screen instead of silently blank.
        /// </summary>
        public string Get(string key)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;

            if (lookup.TryGetValue(key, out LocalizationTable.Entry entry))
            {
                string text = entry.For(CurrentLanguage);
                if (!string.IsNullOrEmpty(text)) return text;

                // Falling back to English beats showing nothing while a translation is still missing.
                return string.IsNullOrEmpty(entry.english) ? key : entry.english;
            }

            return key;
        }

        /// <summary>Switches language, saves the choice, and tells every label to refresh.</summary>
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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStaticState() => Instance = null;
    }
}
