using UnityEngine;
using UnityEngine.UI;

namespace NeonKatana
{
    /// <summary>
    /// The flag button. Pressing it moves to the next language and shows that language's flag.
    /// It hooks up its own button, so nothing has to be wired by hand.
    /// </summary>
    [DisallowMultipleComponent]
    public class LanguageSwitcher : MonoBehaviour
    {
        [Tooltip("The image showing the flag. Falls back to an Image on a child.")]
        [SerializeField] Image flag;

        [Header("Flags")]
        [SerializeField] Sprite englishFlag;
        [SerializeField] Sprite persianFlag;
        [SerializeField] Sprite japaneseFlag;

        Button button;

        void Awake()
        {
            if (flag == null) flag = FindFlagImage();

            WarnIfFlagBelongsToSomebodyElse();

            if (TryGetComponent(out button)) button.onClick.AddListener(Next);
        }

        /// <summary>
        /// Two menu screens can each hold a flag, and the two objects have the same name. Dragging
        /// the wrong one in leaves this switcher painting the other screen's flag while its own
        /// keeps whatever sprite it was built with — which reads as one screen being stuck in the
        /// wrong language.
        /// </summary>
        void WarnIfFlagBelongsToSomebodyElse()
        {
            if (flag == null || flag.transform.IsChildOf(transform)) return;

            Debug.LogWarning(
                $"The flag on '{name}' points at '{flag.name}', which is not part of it. " +
                "That is almost always the other screen's flag picked by mistake.", this);
        }

        /// <summary>
        /// A screen that starts switched off never reaches Start until it is first opened, so the
        /// flag is re-read every time the screen appears rather than only once.
        /// </summary>
        void OnEnable()
        {
            if (LocalizationService.Instance != null) ShowCurrentFlag();
        }

        /// <summary>
        /// The deepest child image, and only this object's own image when it has no children with
        /// one. A flag usually sits inside an outline and a mask, both of which are images in their
        /// own right — taking the first one found would replace the outline with the flag.
        /// </summary>
        Image FindFlagImage()
        {
            Image[] candidates = GetComponentsInChildren<Image>(includeInactive: true);
            Image deepestChild = null;

            foreach (Image candidate in candidates)
                if (candidate.gameObject != gameObject) deepestChild = candidate;

            if (deepestChild != null) return deepestChild;

            return TryGetComponent(out Image own) ? own : null;
        }

        void Start()
        {
            if (LocalizationService.Instance == null)
            {
                Debug.LogWarning($"{nameof(LanguageSwitcher)} found no {nameof(LocalizationService)}.", this);
                return;
            }

            LocalizationService.Instance.LanguageChanged += ShowCurrentFlag;
            ShowCurrentFlag();
        }

        void OnDestroy()
        {
            if (button != null) button.onClick.RemoveListener(Next);
            if (LocalizationService.Instance != null) LocalizationService.Instance.LanguageChanged -= ShowCurrentFlag;
        }

        public void Next()
        {
            if (LocalizationService.Instance != null) LocalizationService.Instance.NextLanguage();
        }

        void ShowCurrentFlag()
        {
            if (flag == null) return;

            Sprite chosen = LocalizationService.Instance.CurrentLanguage switch
            {
                GameLanguage.Persian => persianFlag,
                GameLanguage.Japanese => japaneseFlag,
                _ => englishFlag,
            };

            if (chosen != null) flag.sprite = chosen;
        }
    }
}
