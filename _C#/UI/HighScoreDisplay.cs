using TMPro;
using UnityEngine;

namespace NeonKatana
{
    /// <summary>
    /// Shows the best score the player has ever managed. This is the menu's number — unlike
    /// <see cref="ScoreDisplay"/>, which follows a run in progress and has nothing to follow here.
    /// The saved value is shown at once and replaced if the server knows a better one.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TMP_Text))]
    public class HighScoreDisplay : MonoBehaviour
    {
        [Tooltip("Ask the server for the record too, in case it was set on another device.")]
        [SerializeField] bool askTheServer = true;

        TMP_Text label;

        void Awake() => label = GetComponent<TMP_Text>();

        void Start()
        {
            Show(PlayerProgress.HighScore);

            if (!askTheServer || ProgressService.Instance == null || !ProgressService.Instance.IsOnline) return;

            ProgressService.Instance.LoadProfile((profile, error) =>
            {
                // Silently keeping the local number is the right answer here: the menu should not
                // scold a player for being offline.
                if (profile == null || this == null) return;

                if (profile.highScore > PlayerProgress.HighScore)
                {
                    PlayerProgress.TrySetHighScore(profile.highScore);
                    PlayerProgress.Save();
                }

                Show(Mathf.Max(profile.highScore, PlayerProgress.HighScore));
            });
        }

        void Show(int score) => label.text = score.ToString();
    }
}
