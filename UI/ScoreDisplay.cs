using System.Collections;
using TMPro;
using UnityEngine;

namespace NeonKatana
{
    /// <summary>
    /// Shows the running score, counting up to each new total rather than jumping, so cutting a
    /// fruit reads as something happening.
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    public class ScoreDisplay : MonoBehaviour
    {
        [Tooltip("Seconds the number takes to catch up after a change. 0 shows it straight away.")]
        [SerializeField, Min(0f)] float countUpDuration = 0.25f;
        [Tooltip("How much bigger the label swells on a change.")]
        [SerializeField, Range(1f, 1.5f)] float punchScale = 1.15f;

        TMP_Text label;
        Coroutine counting;
        int shownScore;

        void Awake() => label = GetComponent<TMP_Text>();

        void Start()
        {
            if (ScoreKeeper.Instance == null)
            {
                Debug.LogWarning($"{nameof(ScoreDisplay)} found no {nameof(ScoreKeeper)}, so it stays at zero.", this);
                return;
            }

            ScoreKeeper.Instance.ScoreChanged += OnScoreChanged;
            Show(ScoreKeeper.Instance.Score);
        }

        void OnDestroy()
        {
            if (ScoreKeeper.Instance != null) ScoreKeeper.Instance.ScoreChanged -= OnScoreChanged;
        }

        void OnScoreChanged(int score)
        {
            if (countUpDuration <= 0f)
            {
                Show(score);
                return;
            }

            if (counting != null) StopCoroutine(counting);
            counting = StartCoroutine(CountUpTo(score));
        }

        IEnumerator CountUpTo(int target)
        {
            int from = shownScore;

            for (float elapsed = 0f; elapsed < countUpDuration; elapsed += Time.unscaledDeltaTime)
            {
                float progress = elapsed / countUpDuration;

                Show(Mathf.RoundToInt(Mathf.Lerp(from, target, progress)));
                label.transform.localScale = Vector3.one * Mathf.Lerp(punchScale, 1f, progress);

                yield return null;
            }

            Show(target);
            label.transform.localScale = Vector3.one;
            counting = null;
        }

        void Show(int score)
        {
            shownScore = score;
            label.text = score.ToString();
        }
    }
}
