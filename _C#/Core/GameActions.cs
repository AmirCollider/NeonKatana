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

                case GameAction.SignIn: WithSignIn(signIn => signIn.BeginSignIn()); break;
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

            if (GameManager.Instance != null) GameManager.Instance.BankPlaytimeNow();

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

            Application.OpenURL(url);
        }

        static void Quit()
        {
            if (GameManager.Instance != null) GameManager.Instance.BankPlaytimeNow();

            PlayerProgress.Save();

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
