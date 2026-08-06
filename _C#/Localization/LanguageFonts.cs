using TMPro;
using UnityEngine;

namespace NeonKatana
{
    /// <summary>
    /// Gives every label in the scene the right typeface for the language on screen. One component
    /// handles the whole scene rather than one per label, so adding a label later needs no extra
    /// setup and no font is left behind when the language changes.
    /// <para>
    /// Put a <see cref="KeepOwnFont"/> on anything that should be left alone — numbers, icon
    /// glyphs, anything that is not really language.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public class LanguageFonts : MonoBehaviour
    {
        [Tooltip("Used for English. Also the fallback when a language has none set.")]
        [SerializeField] TMP_FontAsset englishFont;
        [SerializeField] TMP_FontAsset persianFont;
        [SerializeField] TMP_FontAsset japaneseFont;

        [Tooltip("Only labels under here are touched. Empty means the whole scene.")]
        [SerializeField] Transform root;

        void Start()
        {
            if (LocalizationService.Instance == null)
            {
                Debug.LogWarning($"{nameof(LanguageFonts)} found no {nameof(LocalizationService)}, so fonts stay as they are.", this);
                return;
            }

            LocalizationService.Instance.LanguageChanged += Apply;
            Apply();
        }

        void OnDestroy()
        {
            if (LocalizationService.Instance != null) LocalizationService.Instance.LanguageChanged -= Apply;
        }

        void Apply()
        {
            TMP_FontAsset font = FontFor(LocalizationService.Instance.CurrentLanguage);
            if (font == null) return;

            foreach (TMP_Text label in FindLabels())
            {
                if (label.GetComponent<KeepOwnFont>() != null) continue;

                label.font = font;
            }
        }

        TMP_FontAsset FontFor(GameLanguage language)
        {
            TMP_FontAsset chosen = language switch
            {
                GameLanguage.Persian => persianFont,
                GameLanguage.Japanese => japaneseFont,
                _ => englishFont,
            };

            return chosen != null ? chosen : englishFont;
        }

        /// <summary>Inactive labels are included, or a panel would keep the old font until reopened.</summary>
        TMP_Text[] FindLabels() => root != null
            ? root.GetComponentsInChildren<TMP_Text>(includeInactive: true)
            : FindObjectsByType<TMP_Text>(FindObjectsInactive.Include);
    }
}
