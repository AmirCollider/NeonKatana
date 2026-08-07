using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NeonKatana
{
    /// <summary>
    /// Everything the player sees once the run ends: the bomb flash when a bomb was cut, the screen
    /// darkening, the score, and the buttons rising into view.
    /// </summary>
    public class GameOverScreen : MonoBehaviour
    {
        [Header("Panels")]
        [Tooltip("The panel holding the lose text, the score and the buttons.")]
        [SerializeField] AnimatedPanel buttonsPanel;
        [Tooltip("The bomb panel, shown only when the run ended on a bomb.")]
        [SerializeField] AnimatedPanel bombPanel;
        [Tooltip("Full-screen image that darkens the game behind the buttons.")]
        [SerializeField] Image blackout;

        [Header("Score")]
        [Tooltip("Optional. Shows what the player scored this run.")]
        [SerializeField] TMP_Text scoreLabel;
        [Tooltip("Optional. Shows the best score so far.")]
        [SerializeField] TMP_Text highScoreLabel;
        [Tooltip("Optional. Switched on only when this run set a new record.")]
        [SerializeField] GameObject newRecordBadge;

        [Header("Timing")]
        [Tooltip("Seconds the bomb panel is shown before the buttons appear.")]
        [SerializeField, Min(0f)] float bombPanelDuration = 1.5f;
        [Tooltip("Seconds the screen takes to darken once the run is over.")]
        [SerializeField, Min(0f)] float blackoutFadeDuration = 0.6f;
        [Tooltip("How dark the screen is while the game is being played. 0 leaves the game clear.")]
        [SerializeField, Range(0f, 1f)] float playingOpacity;
        [Tooltip("How dark the screen goes once the run is over.")]
        [SerializeField, Range(0f, 1f)] float blackoutOpacity = 0.9f;

        [Header("Sound")]
        [Tooltip("Where the explosion is played from. Falls back to a source on the bomb panel.")]
        [SerializeField] AudioSource explosionAudio;
        [SerializeField] AudioClip explosionSound;

        void Awake()
        {
            if (explosionAudio == null && bombPanel != null)
                explosionAudio = bombPanel.GetComponentInChildren<AudioSource>(includeInactive: true);

            if (buttonsPanel != null) buttonsPanel.Hide(immediately: true);
            if (bombPanel != null) bombPanel.Hide(immediately: true);
            if (newRecordBadge != null) newRecordBadge.SetActive(false);

            // This canvas stays switched on for the whole run so it can hear the run end, which
            // means the overlay has to be cleared here rather than left at its authored tint.
            SetBlackoutOpacity(playingOpacity);
        }

        void Start()
        {
            if (GameManager.Instance == null)
            {
                Debug.LogError($"{nameof(GameOverScreen)} found no {nameof(GameManager)}, so it can never be shown.", this);
                return;
            }

            GameManager.Instance.GameOverReached += Show;
        }

        void OnDestroy()
        {
            if (GameManager.Instance != null) GameManager.Instance.GameOverReached -= Show;
        }

        void Show(GameOverCause cause) => StartCoroutine(ShowRoutine(cause));

        IEnumerator ShowRoutine(GameOverCause cause)
        {
            if (cause == GameOverCause.BombSliced) yield return ShowBombPanel();

            yield return FadeInBlackout();

            FillInTheScore();
            if (buttonsPanel != null) buttonsPanel.Show();
        }

        IEnumerator ShowBombPanel()
        {
            if (bombPanel == null) yield break;

            bombPanel.Show();

            if (explosionAudio != null && explosionSound != null)
                explosionAudio.PlayOneShot(explosionSound);

            yield return new WaitForSeconds(bombPanelDuration);

            bombPanel.Hide();
        }

        IEnumerator FadeInBlackout()
        {
            if (blackout == null || blackoutOpacity <= playingOpacity) yield break;

            for (float elapsed = 0f; elapsed < blackoutFadeDuration; elapsed += Time.deltaTime)
            {
                SetBlackoutOpacity(Mathf.Lerp(playingOpacity, blackoutOpacity, elapsed / blackoutFadeDuration));
                yield return null;
            }

            SetBlackoutOpacity(blackoutOpacity);
        }

        void FillInTheScore()
        {
            ScoreKeeper score = ScoreKeeper.Instance;
            if (score == null) return;

            if (scoreLabel != null) scoreLabel.text = score.Score.ToString();
            if (highScoreLabel != null) highScoreLabel.text = score.HighScore.ToString();
            if (newRecordBadge != null) newRecordBadge.SetActive(score.BeatTheRecord);
        }

        void SetBlackoutOpacity(float opacity)
        {
            if (blackout == null) return;

            Color tint = blackout.color;
            tint.a = opacity;
            blackout.color = tint;

            // An invisible full-screen image is still a full-screen image as far as the event
            // system is concerned. This canvas is on top and stays switched on for the whole run,
            // so while it was left catching rays nothing underneath it could be pressed — the
            // pause button included. Slicing kept working, which is what made it look like the
            // button rather than the sheet over it.
            blackout.raycastTarget = opacity > 0f;
        }
    }
}
