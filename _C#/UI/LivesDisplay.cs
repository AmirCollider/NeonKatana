using UnityEngine;
using UnityEngine.UI;

namespace NeonKatana
{
    /// <summary>
    /// Keeps the row of life icons in step with the lives left. Nothing used to update them, so the
    /// player had no way of telling how close the run was to ending.
    /// </summary>
    public class LivesDisplay : MonoBehaviour
    {
        [Tooltip("The icons, left to right. Leave empty to use every Image on a child.")]
        [SerializeField] Image[] lifeIcons;

        [Header("Sprites")]
        [Tooltip("Shown for a life the player still has.")]
        [SerializeField] Sprite lifeRemainingSprite;
        [Tooltip("Shown once that life has been used up.")]
        [SerializeField] Sprite lifeLostSprite;

        void Start()
        {
            if (lifeIcons == null || lifeIcons.Length == 0)
                lifeIcons = GetComponentsInChildren<Image>(includeInactive: true);

            if (GameManager.Instance == null)
            {
                Debug.LogWarning($"{nameof(LivesDisplay)} found no {nameof(GameManager)}, so the icons stay as they are.", this);
                return;
            }

            GameManager.Instance.LivesChanged += Refresh;
            Refresh(GameManager.Instance.LivesRemaining);
        }

        void OnDestroy()
        {
            if (GameManager.Instance != null) GameManager.Instance.LivesChanged -= Refresh;
        }

        void Refresh(int livesRemaining)
        {
            for (int index = 0; index < lifeIcons.Length; index++)
            {
                Image icon = lifeIcons[index];
                if (icon == null) continue;

                bool stillHeld = index < livesRemaining;

                // With both sprites set the icon is swapped; with neither it is simply hidden.
                if (lifeRemainingSprite != null && lifeLostSprite != null)
                    icon.sprite = stillHeld ? lifeRemainingSprite : lifeLostSprite;
                else
                    icon.enabled = stillHeld;
            }
        }
    }
}
