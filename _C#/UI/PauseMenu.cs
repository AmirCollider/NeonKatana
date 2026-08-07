using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace NeonKatana
{
    /// <summary>
    /// The stop menu. It only reflects what <see cref="GameManager"/> says the state is, so the
    /// pause button, the escape key and anything added later all go through the same path.
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] AnimatedPanel menuPanel;
        [Tooltip("Full-screen image behind the menu. Optional.")]
        [SerializeField] Image blackout;
        [SerializeField, Range(0f, 1f)] float blackoutOpacity = 0.7f;

        [Header("Buttons")]
        [Tooltip("The pause button on the play screen, hidden while the menu is open.")]
        [SerializeField] GameObject pauseButton;

        [Header("Keyboard")]
        [Tooltip("Key that opens and closes the menu while testing on a desktop.")]
        [SerializeField] Key pauseKey = Key.Escape;

        void Awake()
        {
            if (menuPanel != null) menuPanel.Hide(immediately: true);
            SetBlackoutOpacity(0f);
        }

        void Start()
        {
            if (GameManager.Instance == null)
            {
                Debug.LogError($"{nameof(PauseMenu)} found no {nameof(GameManager)}, so it can never open.", this);
                return;
            }

            GameManager.Instance.PauseChanged += OnPauseChanged;
            GameManager.Instance.GameOverReached += OnGameOver;
        }

        void OnDestroy()
        {
            if (GameManager.Instance == null) return;

            GameManager.Instance.PauseChanged -= OnPauseChanged;
            GameManager.Instance.GameOverReached -= OnGameOver;
        }

        void Update()
        {
            Keyboard keyboard = Keyboard.current;

            if (keyboard != null && keyboard[pauseKey].wasPressedThisFrame) TogglePause();
        }

        // --- Wired to the buttons in the inspector ---

        public void TogglePause()
        {
            if (GameManager.Instance != null) GameManager.Instance.TogglePause();
        }

        public void Resume()
        {
            if (GameManager.Instance != null) GameManager.Instance.Resume();
        }

        void OnPauseChanged(bool paused)
        {
            if (menuPanel != null)
            {
                if (paused) menuPanel.Show();
                else menuPanel.Hide();
            }

            SetBlackoutOpacity(paused ? blackoutOpacity : 0f);

            if (pauseButton != null) pauseButton.SetActive(!paused);
        }

        /// <summary>The run is over, so the menu has nothing left to pause.</summary>
        void OnGameOver(GameOverCause cause)
        {
            if (menuPanel != null) menuPanel.Hide(immediately: true);

            SetBlackoutOpacity(0f);

            if (pauseButton != null) pauseButton.SetActive(false);
        }

        void SetBlackoutOpacity(float opacity)
        {
            if (blackout == null) return;

            Color tint = blackout.color;
            tint.a = opacity;
            blackout.color = tint;
            blackout.raycastTarget = opacity > 0f;
        }
    }
}
