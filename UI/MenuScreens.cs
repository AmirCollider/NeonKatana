using UnityEngine;

namespace NeonKatana
{
    /// <summary>The full-screen pages the menu can be showing.</summary>
    public enum MenuScreen
    {
        Main,
        Account,
    }

    /// <summary>
    /// Swaps between the menu's pages. It is the only thing that knows which canvas is which, so a
    /// button only has to say where it wants to go.
    /// </summary>
    [DisallowMultipleComponent]
    public class MenuScreens : MonoBehaviour
    {
        public static MenuScreens Instance { get; private set; }

        [SerializeField] Canvas mainScreen;
        [SerializeField] Canvas accountScreen;
        [SerializeField] MenuScreen openOnStart = MenuScreen.Main;

        public MenuScreen Current { get; private set; }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"A second {nameof(MenuScreens)} is in the scene. Keeping the first one.", this);
                enabled = false;
                return;
            }

            Instance = this;
            WarnIfItWouldSwitchItselfOff();
            Show(openOnStart);
        }

        /// <summary>
        /// Living inside a screen this component switches off is a trap: going to the other screen
        /// takes the services down with it, and whatever they were doing stops mid-way.
        /// </summary>
        void WarnIfItWouldSwitchItselfOff()
        {
            if (!SwitchesOff(mainScreen) && !SwitchesOff(accountScreen)) return;

            Debug.LogWarning(
                $"{nameof(MenuScreens)} sits inside a screen it switches off. Move it — and any " +
                "service beside it — onto an object that stays on for the whole scene.", this);
        }

        bool SwitchesOff(Canvas screen) => screen != null && transform.IsChildOf(screen.transform);

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Show(MenuScreen screen)
        {
            Current = screen;

            if (mainScreen != null) mainScreen.gameObject.SetActive(screen == MenuScreen.Main);
            if (accountScreen != null) accountScreen.gameObject.SetActive(screen == MenuScreen.Account);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStaticState() => Instance = null;
    }
}
