using TMPro;
using UnityEngine;

namespace NeonKatana
{
    /// <summary>
    /// Puts this label's text under the localisation service's control. Drop it on any TextMeshPro
    /// object, type the key, and the words follow whatever language is selected.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TMP_Text))]
    public class LocalizedLabel : MonoBehaviour
    {
        [Tooltip("The key to look up, e.g. \"menu.resume\".")]
        [SerializeField] string key;

        TMP_Text label;

        void Awake() => label = GetComponent<TMP_Text>();

        void Start()
        {
            if (LocalizationService.Instance == null)
            {
                Debug.LogWarning($"'{name}' found no {nameof(LocalizationService)}, so its text is left alone.", this);
                return;
            }

            LocalizationService.Instance.LanguageChanged += Refresh;
            Refresh();
        }

        void OnDestroy()
        {
            if (LocalizationService.Instance != null) LocalizationService.Instance.LanguageChanged -= Refresh;
        }

        /// <summary>Points the label at a different key at runtime.</summary>
        public void SetKey(string newKey)
        {
            key = newKey;
            Refresh();
        }

        void Refresh()
        {
            if (LocalizationService.Instance == null) return;

            label.text = LocalizationService.Instance.Get(key);
        }
    }
}
