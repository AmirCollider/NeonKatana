using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace NeonKatana
{
    /// <summary>
    /// Swaps every label in the scene into the selected language, using the translations written
    /// down in <see cref="GameText"/>.
    ///
    /// <para>
    /// Put one of these beside <see cref="LocalizationService"/> — on <c>MenuServices</c> and on
    /// <c>LevelManager</c> — and that is the whole of the setup. Nothing goes on the labels
    /// themselves, no keys are invented and no asset has to be kept in step with the scene.
    /// </para>
    /// <para>
    /// It works by remembering what each label said when it was first seen. That first reading is
    /// the Persian the scene was built with, and every later language is worked out from it — so
    /// switching English → Japanese → Persian → English translates from the original each time
    /// rather than translating a translation.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public class TextTranslator : MonoBehaviour
    {
        [Tooltip("Only labels under here are touched. Empty means the whole scene.")]
        [SerializeField] Transform root;

        [Tooltip(
            "Warn about labels with words in them that GameText has no translation for. Worth " +
            "leaving on while there is still text to translate.")]
        [SerializeField] bool reportUntranslated = true;

        /// <summary>
        /// What each label said in the scene, before anything translated it. Keyed by the label
        /// itself, so a label that is switched off and on again is still the same label.
        /// </summary>
        readonly Dictionary<TMP_Text, string> asAuthored = new Dictionary<TMP_Text, string>();

        /// <summary>
        /// Labels something else fills in at runtime, which no translator may touch.
        /// <para>
        /// Static because a label belongs to whoever writes it regardless of which translator is
        /// looking — the menu scene has one and the game scene has another, and the player's name
        /// is not the scene's to translate in either.
        /// </para>
        /// </summary>
        /// <remarks>
        /// The labels themselves rather than their instance ids: <c>GetInstanceID</c> is on its way
        /// out, and a set of the objects says what it means without a number standing in for them.
        /// </remarks>
        static readonly HashSet<TMP_Text> notOurs = new HashSet<TMP_Text>();

        /// <summary>
        /// Says that this label's words are written by code and are not a line of the scene.
        /// Call it from <c>Awake</c>, so it lands before any translator's first pass.
        /// </summary>
        public static void LeaveAlone(TMP_Text label)
        {
            if (label == null) return;

            // Labels from a scene that has been left are destroyed but still in here, and this set
            // outlives scenes. Clearing them as we go keeps a menu the player has opened forty
            // times from being remembered forty times over.
            notOurs.RemoveWhere(gone => gone == null);

            notOurs.Add(label);
        }

        void Start()
        {
            if (LocalizationService.Instance == null)
            {
                Debug.LogWarning($"{nameof(TextTranslator)} found no {nameof(LocalizationService)}, so no words can change.", this);
                return;
            }

            LocalizationService.Instance.LanguageChanged += Apply;
            Apply();
        }

        void OnDestroy()
        {
            if (LocalizationService.Instance != null) LocalizationService.Instance.LanguageChanged -= Apply;
        }

        /// <summary>Puts every label into the language on screen. Safe to call at any time.</summary>
        public void Apply()
        {
            if (LocalizationService.Instance == null) return;

            GameLanguage language = LocalizationService.Instance.CurrentLanguage;
            var missing = new List<string>();

            foreach (TMP_Text label in Labels())
            {
                if (label == null) continue;

                if (WrittenBySomethingElse(label))
                {
                    // Claimed after this translator had already seen it — Awake beats Start, but
                    // a component added at runtime does not. Whatever was remembered was the
                    // placeholder it happened to be showing, and handing that back on the next
                    // language change would wipe out what the real owner had written since.
                    asAuthored.Remove(label);
                    continue;
                }

                if (!asAuthored.TryGetValue(label, out string original))
                {
                    original = label.text;

                    // An empty label is a placeholder or a name filled in at sign-in. There is
                    // nothing to remember, and remembering "" would freeze it that way.
                    if (string.IsNullOrWhiteSpace(original)) continue;

                    asAuthored[label] = original;
                }

                string wanted = GameText.In(original, language);

                if (label.text != wanted) label.text = wanted;

                if (reportUntranslated && language != GameLanguage.Persian && !GameText.Knows(original))
                    missing.Add(original);
            }

            Report(missing);
        }

        /// <summary>
        /// Names what is still untranslated, already written out as rows for
        /// <see cref="GameText"/>.
        /// <para>
        /// Printing the raw line was not enough. The words in a label and the words in the table
        /// can differ by a line break, a double space or a direction mark nobody typed, and two
        /// strings that look identical in a Console then fail to match for a reason the Console
        /// cannot show. Printing the row to paste sidesteps the comparison entirely.
        /// </para>
        /// </summary>
        void Report(List<string> missing)
        {
            if (missing.Count == 0) return;

            var said = new System.Text.StringBuilder();

            said.AppendLine($"{missing.Count} line(s) have no translation in {nameof(GameText)} and were left in Persian.");
            said.AppendLine("Paste these into the Written table and fill in the last two columns:");
            said.AppendLine();

            foreach (string line in missing)
                said.AppendLine($"            {{ \"{GameText.Normalize(line)}\", \"\", \"\" }},");

            Debug.LogWarning(said.ToString(), this);
        }

        /// <summary>
        /// Labels whose words are somebody else's to write — a score counting up, a high score off
        /// the server, a player's name. Translating those would only be overwritten, or would
        /// overwrite them with a stale copy.
        /// </summary>
        static bool WrittenBySomethingElse(TMP_Text label) =>
            notOurs.Contains(label)
            || label.GetComponent<ScoreDisplay>() != null
            || label.GetComponent<HighScoreDisplay>() != null;

        /// <summary>Forgets every claim when play mode starts without a domain reload.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStaticState() => notOurs.Clear();

        /// <summary>Switched-off labels included, or a hidden panel keeps the old language.</summary>
        TMP_Text[] Labels() => root != null
            ? root.GetComponentsInChildren<TMP_Text>(includeInactive: true)
            : FindObjectsByType<TMP_Text>(FindObjectsInactive.Include);
    }
}
