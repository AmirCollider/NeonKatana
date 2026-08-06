using System.Collections;
using UnityEngine;

namespace NeonKatana
{
    /// <summary>
    /// Brings a row of things in one after another instead of all at once, which is most of the
    /// difference between a menu that appears and a menu that arrives.
    /// Put it on the parent; every child rises and fades in, a beat apart.
    /// </summary>
    [DisallowMultipleComponent]
    public class StaggeredEntrance : MonoBehaviour
    {
        [SerializeField, Min(0f)] float startDelay = 0.1f;
        [Tooltip("Seconds between one child starting and the next.")]
        [SerializeField, Min(0f)] float gapBetweenChildren = 0.08f;
        [SerializeField, Min(0.05f)] float childDuration = 0.4f;

        [Tooltip("How far below its place each child starts.")]
        [SerializeField] float riseDistance = 50f;
        [SerializeField, Range(0.1f, 1f)] float startScale = 0.85f;

        [Tooltip("Shape of each child's arrival. The default overshoots a little.")]
        [SerializeField] AnimationCurve entrance = new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 2.4f),
            new Keyframe(0.55f, 1.08f),
            new Keyframe(1f, 1f, 0f, 0f));

        void OnEnable() => StartCoroutine(BringChildrenIn());

        IEnumerator BringChildrenIn()
        {
            var children = new RectTransform[transform.childCount];
            var restingPositions = new Vector2[children.Length];

            for (int index = 0; index < children.Length; index++)
            {
                Transform child = transform.GetChild(index);

                // A switched-off child is left alone. Moving one would strand it out of place for
                // whenever it is switched on, and it is not part of this arrival anyway.
                if (!child.gameObject.activeSelf) continue;

                children[index] = child as RectTransform;
                if (children[index] == null) continue;

                restingPositions[index] = children[index].anchoredPosition;
                Place(children[index], restingPositions[index], 0f);
            }

            if (startDelay > 0f) yield return new WaitForSecondsRealtime(startDelay);

            for (int index = 0; index < children.Length; index++)
            {
                if (children[index] == null) continue;

                StartCoroutine(BringIn(children[index], restingPositions[index]));

                if (gapBetweenChildren > 0f) yield return new WaitForSecondsRealtime(gapBetweenChildren);
            }
        }

        IEnumerator BringIn(RectTransform child, Vector2 restingPosition)
        {
            for (float elapsed = 0f; elapsed < childDuration; elapsed += Time.unscaledDeltaTime)
            {
                Place(child, restingPosition, entrance.Evaluate(elapsed / childDuration));
                yield return null;
            }

            Place(child, restingPosition, 1f);
        }

        /// <summary>Places a child between hidden (0) and settled (1). Above 1 is the overshoot.</summary>
        void Place(RectTransform child, Vector2 restingPosition, float progress)
        {
            child.anchoredPosition = restingPosition + Vector2.down * (riseDistance * (1f - progress));
            child.localScale = Vector3.one * Mathf.LerpUnclamped(startScale, 1f, progress);

            if (child.TryGetComponent(out CanvasGroup group)) group.alpha = Mathf.Clamp01(progress);
        }
    }
}
