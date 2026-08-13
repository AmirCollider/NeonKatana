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

        /// <summary>Kept so the handler comes off the service it actually went on to.</summary>
        ProgressService progressService;

        void Awake() => label = GetComponent<TMP_Text>();

        void Start()
        {
            progressService = ProgressService.Instance;

            Show(PlayerProgress.HighScore);

            // The record comes out of the same read as the name and the picture, which
            // ProgressService does once and hands round. This used to ask for a copy of its own on
            // the first frame of the menu, when nobody is signed in yet and the answer is always
            // "offline" — so a record set on another device never appeared.
            if (progressService != null) progressService.ProfileChanged += ShowServerRecord;

            ShowServerRecord();
        }

        /// <summary>Catches up on a change made while this screen was hidden. See SignInDisplay.</summary>
        void OnEnable()
        {
            if (progressService != null) ShowServerRecord();
        }

        void OnDestroy()
        {
            if (progressService != null) progressService.ProfileChanged -= ShowServerRecord;
        }

        void ShowServerRecord()
        {
            if (!askTheServer || progressService == null)
            {
                Show(PlayerProgress.HighScore);
                return;
            }

            PlayerProfile profile = progressService.Profile;

            if (profile == null)
            {
                // The device's own number, NOT whatever is already on the label.
                //
                // This used to return and leave the label alone, which is right when the answer is
                // "we are offline" and wrong when the answer is "somebody else is signed in now".
                // Switching accounts clears the record and raises this event, so the new player was
                // shown the previous player's high score — and went on being shown it, because
                // nothing else ever writes this label.
                Show(PlayerProgress.HighScore);
                return;
            }

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
