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

            Refresh();
        }

        void OnDestroy()
        {
            if (SignInService.Instance != null) SignInService.Instance.SignedInChanged -= Refresh;
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

            // A player can be signed in before the server has told us their chosen name.
            ShowSignedIn(string.IsNullOrWhiteSpace(signIn.UserName) ? signIn.PlayerId : signIn.UserName);
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
