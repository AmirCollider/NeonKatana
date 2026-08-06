using UnityEngine;

namespace NeonKatana
{
    /// <summary>
    /// Moves an element depending on the language. Persian reads right to left, so a hint that
    /// belongs beside a button on one side belongs on the other side entirely once the text flips.
    /// <para>
    /// Use <c>Reset</c> (the component's context menu) to fill all three with where the element
    /// currently sits, then change only the ones that differ.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public class LocalizedLayout : MonoBehaviour
    {
        [SerializeField] Vector2 englishPosition;
        [SerializeField] Vector2 persianPosition;
        [SerializeField] Vector2 japanesePosition;

        RectTransform element;

        void Awake() => element = (RectTransform)transform;

        void Reset()
        {
            Vector2 here = ((RectTransform)transform).anchoredPosition;

            englishPosition = here;
            persianPosition = here;
            japanesePosition = here;
        }

        void Start()
        {
            if (LocalizationService.Instance == null)
            {
                Debug.LogWarning($"'{name}' found no {nameof(LocalizationService)}, so it stays where it is.", this);
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
            element.anchoredPosition = LocalizationService.Instance.CurrentLanguage switch
            {
                GameLanguage.Persian => persianPosition,
                GameLanguage.Japanese => japanesePosition,
                _ => englishPosition,
            };
        }
    }
}
