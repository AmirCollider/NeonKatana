using System.Collections;
using UnityEngine;

namespace NeonKatana
{
    /// <summary>
    /// A panel that rises and pops into view instead of snapping on. Everything runs on unscaled
    /// time, so the pause menu still animates while the game itself is frozen.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasGroup))]
    public class AnimatedPanel : MonoBehaviour
    {
        [Header("Timing")]
        [SerializeField, Min(0.01f)] float showDuration = 0.35f;
        [SerializeField, Min(0.01f)] float hideDuration = 0.18f;

        [Header("Entrance")]
        [Tooltip("Scale the panel grows from.")]
        [SerializeField, Range(0.1f, 1f)] float hiddenScale = 0.8f;
        [Tooltip("How far below its resting place the panel starts, in canvas units.")]
        [SerializeField] float riseDistance = 60f;
        [Tooltip("Shape of the entrance. The default overshoots a little before settling.")]
        [SerializeField] AnimationCurve entrance = new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 2.4f),
            new Keyframe(0.55f, 1.08f),
            new Keyframe(1f, 1f, 0f, 0f));

        RectTransform panel;
        CanvasGroup group;
        Vector2 restingPosition;
        bool measured;
        Coroutine running;

        public bool IsShown { get; private set; }

        void Awake() => Measure();

        /// <summary>
        /// Reads the panel's authored resting place once. Safe to call while the GameObject is
        /// switched off, which matters because panels start that way and Awake never runs.
        /// </summary>
        void Measure()
        {
            if (measured) return;

            panel = transform as RectTransform;
            group = GetComponent<CanvasGroup>();
            restingPosition = panel != null ? panel.anchoredPosition : Vector2.zero;
            measured = true;

            if (panel == null)
                Debug.LogWarning($"'{name}' is not a UI object, so it will fade but not move.", this);
        }

        public void Show()
        {
            Measure();
            IsShown = true;

            gameObject.SetActive(true);
            Restart(ShowRoutine());
        }

        public void Hide(bool immediately = false)
        {
            Measure();
            IsShown = false;

            if (!gameObject.activeInHierarchy || immediately)
            {
                Stop();
                Apply(0f);
                gameObject.SetActive(false);
                return;
            }

            Restart(HideRoutine());
        }

        IEnumerator ShowRoutine()
        {
            group.interactable = false;
            group.blocksRaycasts = false;

            for (float elapsed = 0f; elapsed < showDuration; elapsed += Time.unscaledDeltaTime)
            {
                Apply(entrance.Evaluate(elapsed / showDuration));
                yield return null;
            }

            Apply(1f);
            group.interactable = true;
            group.blocksRaycasts = true;
            running = null;
        }

        IEnumerator HideRoutine()
        {
            group.interactable = false;
            group.blocksRaycasts = false;

            for (float elapsed = 0f; elapsed < hideDuration; elapsed += Time.unscaledDeltaTime)
            {
                Apply(1f - (elapsed / hideDuration));
                yield return null;
            }

            Apply(0f);
            gameObject.SetActive(false);
            running = null;
        }

        /// <summary>
        /// Places the panel somewhere between hidden (0) and settled (1). Values above 1 are
        /// expected — that is what gives the entrance its overshoot.
        /// </summary>
        void Apply(float progress)
        {
            group.alpha = Mathf.Clamp01(progress);

            if (panel == null) return;

            panel.localScale = Vector3.one * Mathf.LerpUnclamped(hiddenScale, 1f, progress);
            panel.anchoredPosition = restingPosition + Vector2.down * (riseDistance * (1f - progress));
        }

        void Restart(IEnumerator routine)
        {
            Stop();
            running = StartCoroutine(routine);
        }

        void Stop()
        {
            if (running == null) return;

            StopCoroutine(running);
            running = null;
        }
    }
}
