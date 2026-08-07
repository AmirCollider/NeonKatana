using UnityEngine;
using UnityEngine.SceneManagement;

namespace NeonKatana
{
    /// <summary>
    /// Everything a button in this game can do.
    /// <para>
    /// The numbers are written out and must never change. Unity saves the number, not the name, so
    /// inserting a value in the middle silently re-points every button that was saved after it —
    /// which is exactly what happened when SignIn and SignOut were added ahead of QuitGame.
    /// New actions go on the end.
    /// </para>
    /// </summary>
    public enum GameAction
    {
        StartGame = 0,
        RestartRun = 1,
        ResumeGame = 2,
        TogglePause = 3,
        OpenMainMenu = 4,
        OpenAccountScreen = 5,
        CloseAccountScreen = 6,
        NextLanguage = 7,
        OpenWebsite = 8,
        SignIn = 9,
        SignOut = 10,
        QuitGame = 11,
        PasteSignInCode = 12,
    }

    /// <summary>
    /// The one place a button's press is turned into something happening. Buttons name an action
    /// from <see cref="GameAction"/> instead of being pointed at a method in the inspector, so
    /// renaming or moving a method is caught by the compiler rather than silently unhooking a
    /// button, and a scene can be rebuilt without re-picking anything from a dropdown.
    /// </summary>
    public static class GameActions
    {
        public static void Run(GameAction action, string url = null)
        {
            switch (action)
            {
                case GameAction.StartGame: Load(Scenes.MainGame); break;
                case GameAction.OpenMainMenu: Load(Scenes.MainMenu); break;
                case GameAction.RestartRun: Load(SceneManager.GetActiveScene().name); break;

                case GameAction.ResumeGame:
                    if (GameManager.Instance != null) GameManager.Instance.SetPaused(false);
                    break;

                case GameAction.TogglePause:
                    if (GameManager.Instance != null) GameManager.Instance.TogglePause();
                    break;

                case GameAction.OpenAccountScreen: ShowScreen(MenuScreen.Account); break;
                case GameAction.CloseAccountScreen: ShowScreen(MenuScreen.Main); break;

                case GameAction.NextLanguage:
                    if (LocalizationService.Instance != null) LocalizationService.Instance.NextLanguage();
                    break;

                case GameAction.OpenWebsite: OpenWebsite(url); break;

                // Pressed while somebody is already signed in, this is a request to be somebody
                // else — there is no other reason to press it twice. It used to start the same
                // flow either way, and the flow put the new token on top of the old account's
                // record, totals and outbox without noticing the person had changed.
                case GameAction.SignIn:
                    WithSignIn(signIn =>
                    {
                        if (signIn.IsSignedIn) signIn.SwitchAccount();
                        else signIn.BeginSignIn();
                    });
                    break;
                case GameAction.SignOut: WithSignIn(signIn => signIn.SignOut()); break;
                case GameAction.PasteSignInCode: WithSignIn(signIn => signIn.PasteCodeFromClipboard()); break;

                case GameAction.QuitGame: Quit(); break;
            }
        }

        /// <summary>
        /// Loads a scene after letting the run settle its books. Time scale is reset here because
        /// leaving a paused game through a button would otherwise carry the frozen clock across.
        /// </summary>
        static void Load(string sceneName)
        {
            Time.timeScale = 1f;

            // Not just the playtime. A run walked out of through the pause menu is still a run
            // that happened, and the score in it is still a score the player made — abandoning it
            // properly is what files both away. Leaving through this button used to keep the
            // seconds and throw the score away, so a record broken on the way to the menu was
            // gone by the time the menu drew it.
            if (GameManager.Instance != null) GameManager.Instance.AbandonRun();

            SceneManager.LoadScene(sceneName);
        }

        static void ShowScreen(MenuScreen screen)
        {
            if (MenuScreens.Instance == null)
            {
                Debug.LogWarning($"There is no {nameof(MenuScreens)} in this scene, so '{screen}' cannot be shown.");
                return;
            }

            MenuScreens.Instance.Show(screen);
        }

        static void WithSignIn(System.Action<SignInService> use)
        {
            if (SignInService.Instance == null)
            {
                Debug.LogWarning($"There is no {nameof(SignInService)} in this scene, so the button does nothing.");
                return;
            }

            use(SignInService.Instance);
        }

        static void OpenWebsite(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                Debug.LogWarning("A website button was pressed but no address was set on it.");
                return;
            }

            Application.OpenURL(WithLanguage(url));
        }

        /// <summary>
        /// The address with <c>?lang=</c> on it, so the site opens in the language the player is
        /// already reading rather than guessing from their browser.
        /// <para>
        /// The site takes <c>?lang=fa|en|ja</c> and answers it before anything else — ahead of the
        /// cookie and ahead of <c>Accept-Language</c> — and sets the cookie from it, so this
        /// decides the language of the whole visit rather than only the page that opens.
        /// </para>
        /// <para>
        /// An address that already names a language is left as it is: somebody wrote that on
        /// purpose, and the game second-guessing it would make the field impossible to use.
        /// </para>
        /// </summary>
        static string WithLanguage(string url)
        {
            if (LocalizationService.Instance == null) return url;

            // A fragment has to stay at the end. Appending after it puts the query inside the
            // fragment, where it is the browser's business and the server never sees it.
            int fragmentAt = url.IndexOf('#');
            string address = fragmentAt < 0 ? url : url.Substring(0, fragmentAt);
            string fragment = fragmentAt < 0 ? string.Empty : url.Substring(fragmentAt);

            if (address.Contains("lang=")) return url;

            string separator = address.Contains("?") ? "&" : "?";

            return $"{address}{separator}lang={LocalizationService.Instance.CurrentLanguageTag}{fragment}";
        }

        static void Quit()
        {
            if (GameManager.Instance != null) GameManager.Instance.AbandonRun();

            PlayerProgress.Save();

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
