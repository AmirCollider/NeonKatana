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

            // The record comes out of the same read as the name and the picture, which
            // ProgressService does once and hands round. This used to ask for a copy of its own on
            // the first frame of the menu, when nobody is signed in yet and the answer is always
            // "offline" — so a record set on another device never appeared.
            if (ProgressService.Instance != null) ProgressService.Instance.ProfileChanged += ShowServerRecord;

            ShowServerRecord();
        }

        void OnDestroy()
        {
            if (ProgressService.Instance != null) ProgressService.Instance.ProfileChanged -= ShowServerRecord;
        }

        void ShowServerRecord()
        {
            if (!askTheServer || ProgressService.Instance == null) return;

            PlayerProfile profile = ProgressService.Instance.Profile;

            // Silently keeping the local number is the right answer here: the menu should not
            // scold a player for being offline.
            if (profile == null) return;

            if (profile.highScore > PlayerProgress.HighScore)
            {
                PlayerProgress.TrySetHighScore(profile.highScore);
                PlayerProgress.Save();
            }

            Show(Mathf.Max(profile.highScore, PlayerProgress.HighScore));
        }

        void Show(int score) => label.text = score.ToString();
    }
}
