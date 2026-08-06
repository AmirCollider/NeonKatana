using TMPro;
using UnityEngine;

namespace NeonKatana
{
    /// <summary>
    /// The sign-in corner of the menu: a hint while nobody is signed in, a name once somebody is.
    /// It follows <see cref="SignInService"/> rather than asking the server every time, so it is
    /// right the instant somebody signs in or out.
    /// </summary>
    [DisallowMultipleComponent]
    public class SignInDisplay : MonoBehaviour
    {
        [Tooltip("Shown while signed out — \"sign in to save your score\", and so on.")]
        [SerializeField] GameObject signedOutHint;
        [Tooltip("Shown once signed in.")]
        [SerializeField] TMP_Text userNameLabel;

        void Start()
        {
            if (SignInService.Instance != null) SignInService.Instance.SignedInChanged += Refresh;

            // The chosen name arrives a moment after signing in, in the player's record, so this
            // corner has to be told twice: once when they sign in, and again when the record lands.
            if (ProgressService.Instance != null) ProgressService.Instance.ProfileChanged += Refresh;

            Refresh();
        }

        void OnDestroy()
        {
            if (SignInService.Instance != null) SignInService.Instance.SignedInChanged -= Refresh;
            if (ProgressService.Instance != null) ProgressService.Instance.ProfileChanged -= Refresh;
        }

        /// <summary>Puts the corner back in step with who is signed in.</summary>
        public void Refresh()
        {
            SignInService signIn = SignInService.Instance;

            if (signIn == null || !signIn.IsSignedIn)
            {
                ShowSignedOut();
                return;
            }

            ShowSignedIn(NameToShow(signIn));
        }

        /// <summary>
        /// What this player is called, in the order the account rules put it.
        /// <list type="number">
        /// <item>the username they chose on the site — 3 to 12 English letters and digits</item>
        /// <item>until then, their Google account's own name, cut to the same 12 characters</item>
        /// <item>and failing even that, the id, which is their address without its domain</item>
        /// </list>
        /// <para>
        /// The order is the whole point. This used to show whatever the token happened to carry —
        /// the account name when there was one, the entire address when there was not — so the same
        /// player was called three different things depending on what Google had sent that day, and
        /// a name they had actually chosen was never shown at all.
        /// </para>
        /// </summary>
        static string NameToShow(SignInService signIn)
        {
            string chosen = ChosenUserName();

            if (!string.IsNullOrWhiteSpace(chosen)) return chosen.Trim();

            // SignInService has already worked the rest of the ladder out for itself, so a menu
            // opened before the record arrives says the same thing as one opened after.
            return signIn.UserName;
        }

        /// <summary>The name out of the player's record, or null while there is no record yet.</summary>
        static string ChosenUserName()
        {
            PlayerProfile profile = ProgressService.Instance != null
                ? ProgressService.Instance.Profile
                : null;

            if (profile == null) return null;

            return !string.IsNullOrWhiteSpace(profile.username) ? profile.username : profile.displayName;
        }

        void ShowSignedOut()
        {
            if (signedOutHint != null) signedOutHint.SetActive(true);
            if (userNameLabel != null) userNameLabel.gameObject.SetActive(false);
        }

        void ShowSignedIn(string userName)
        {
            if (signedOutHint != null) signedOutHint.SetActive(false);

            if (userNameLabel == null) return;

            userNameLabel.gameObject.SetActive(true);
            userNameLabel.text = userName;
        }
    }
}
