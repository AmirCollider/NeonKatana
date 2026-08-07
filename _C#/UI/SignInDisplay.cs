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

        [Tooltip(
            "Shown while signed in but with no name to show yet. Never the player id — that is " +
            "the player's email address with the domain taken off.")]
        [SerializeField] string nameWhileUnknown = "Player";

        /// <summary>
        /// Claims the name label before anything can mistake it for a line of the scene. In
        /// <c>Awake</c> because Unity runs every Awake before any Start, and
        /// <see cref="TextTranslator"/> takes its first look in Start.
        /// </summary>
        void Awake()
        {
            if (userNameLabel != null) TextTranslator.LeaveAlone(userNameLabel);
        }

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
        /// <item>and failing even that, a neutral word — never anything derived from the address</item>
        /// </list>
        /// <para>
        /// The bottom of that ladder used to be the player id, which is the email address with its
        /// domain taken off. So a player whose Google account had no usable name — and every player
        /// during the moment between signing in and their record arriving — had a readable form of
        /// their own email address on the menu, in front of whoever else was looking at the phone.
        /// </para>
        /// <para>
        /// A name is a convenience; an address is not ours to put on screen. When there is no name
        /// the answer is to say nothing identifying, not to reach one rung lower.
        /// </para>
        /// </summary>
        string NameToShow(SignInService signIn)
        {
            string chosen = ChosenUserName();

            if (!string.IsNullOrWhiteSpace(chosen)) return chosen.Trim();

            // SignInService has already cut the Google account name to length, and gives back
            // nothing at all rather than falling through to the id.
            string fromGoogle = signIn.UserName;

            if (!string.IsNullOrWhiteSpace(fromGoogle)) return fromGoogle.Trim();

            return nameWhileUnknown;
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
