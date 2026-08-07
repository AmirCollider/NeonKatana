using UnityEngine;
using UnityDirectTMP;

namespace NeonKatana
{
    /// <summary>
    /// Gives every label in the scene the right typeface for the language on screen. One component
    /// handles the whole scene rather than one per label, so adding a label later needs no extra
    /// setup and no font is left behind when the language changes.
    ///
    /// <para>
    /// <b>Raw font files, not TextMeshPro font assets.</b> This used to hold three
    /// <c>TMP_FontAsset</c>s and write them straight onto every label's <c>font</c>. That is the
    /// wrong end of the pipe in a project using Unity DirectTMP: the whole point of that package is
    /// that a label is drawn from a <c>.ttf</c> with no font asset to build, and its
    /// <see cref="DirectFont"/> writes the label's <c>font</c> itself every time the text changes
    /// script. Two things writing the same field every frame is a fight, and DirectFont — which
    /// runs in <c>LateUpdate</c> — wins it, so the fonts set here would have been quietly undone.
    /// </para>
    /// <para>
    /// So it hands the <c>.ttf</c> to the label's own <see cref="DirectFont"/> instead, which is
    /// the component that owns that decision. Put a <see cref="KeepOwnFont"/> on anything that
    /// should be left alone — numbers, icon glyphs, anything that is not really language.
    /// </para>
    /// <para>
    /// It is optional. <see cref="DirectFont"/> has three per-language fields of its own
    /// (Persian/Arabic, Japanese/Chinese/Korean, Latin) and picks between them from the script the
    /// text is actually in. Filling those in per label and deleting this component is the simpler
    /// setup; this exists for setting the whole scene from one place.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public class LanguageFonts : MonoBehaviour
    {
        [Tooltip("The .ttf used for English. Also the fallback when a language has none set.")]
        [SerializeField] Font englishFont;

        [Tooltip("The .ttf used for Persian.")]
        [SerializeField] Font persianFont;

        [Tooltip("The .ttf used for Japanese.")]
        [SerializeField] Font japaneseFont;

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
            Font font = FontFor(LocalizationService.Instance.CurrentLanguage);
            if (font == null) return;

            foreach (DirectFont label in Labels())
            {
                if (label == null || label.GetComponent<KeepOwnFont>() != null) continue;

                // Setting the property rather than the field: DirectFont throws its cached asset
                // away and rebuilds on the next frame, which is the only way the change takes.
                if (label.Font != font) label.Font = font;
            }
        }

        Font FontFor(GameLanguage language)
        {
            Font chosen = language switch
            {
                GameLanguage.Persian => persianFont,
                GameLanguage.Japanese => japaneseFont,
                _ => englishFont,
            };

            return chosen != null ? chosen : englishFont;
        }

        /// <summary>Inactive labels are included, or a panel would keep the old font until reopened.</summary>
        DirectFont[] Labels() => root != null
            ? root.GetComponentsInChildren<DirectFont>(includeInactive: true)
            : FindObjectsByType<DirectFont>(FindObjectsInactive.Include);
    }
}
