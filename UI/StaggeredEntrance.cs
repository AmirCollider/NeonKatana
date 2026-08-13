using System.Collections;
using System.Collections.Generic;
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

        RectTransform[] children;

        /// <summary>
        /// Each child's own owner of its transform. Where the child belongs is asked of this rather
        /// than read off <c>localScale</c> and <c>anchoredPosition</c> — a child that is also
        /// breathing has usually moved by the time this component gets to look.
        /// </summary>
        UITransformDriver[] drivers;

        /// <summary>Children that place themselves; see <see cref="RestingPositionOf"/>.</summary>
        LocalizedLayout[] ownLayouts;

        /// <summary>Who is taking part in the arrival that is running. Fixed once it starts.</summary>
        readonly List<int> arriving = new List<int>();

        void OnEnable()
        {
            RememberWhereEverythingBelongs();
            StartCoroutine(BringChildrenIn());
        }

        /// <summary>
        /// Puts every child back where it belongs, because this screen is being closed.
        /// <para>
        /// Switching the canvas off kills the coroutines wherever they had got to, which used to
        /// leave the children stranded — one of them halfway up, the rest still at the bottom with
        /// no alpha. That much was only a frame of ugliness on the way out. The damage was on the
        /// way back in: the next entrance measured "where this child belongs" from wherever it had
        /// been abandoned, so every interrupted close moved the whole screen another
        /// <see cref="riseDistance"/> further down, for good.
        /// </para>
        /// </summary>
        void OnDisable()
        {
            StopAllCoroutines();
            SettleEverything();
        }

        void SettleEverything()
        {
            if (children == null) return;

            for (int index = 0; index < children.Length; index++)
                if (children[index] != null) Place(index, 1f);
        }

        /// <summary>
        /// Finds the children once and gives each one an owner for its transform. The authored
        /// pose is the driver's business, not this component's — which is what stopped an
        /// interrupted entrance, or a child that happened to be mid-breathe, from being written
        /// down as where that child belongs.
        /// </summary>
        void RememberWhereEverythingBelongs()
        {
            // A count that no longer matches means children were added or removed since, so the
            // row is a different row and has to be looked at again.
            if (children != null && children.Length == transform.childCount) return;

            children = new RectTransform[transform.childCount];
            drivers = new UITransformDriver[children.Length];
            ownLayouts = new LocalizedLayout[children.Length];

            for (int index = 0; index < children.Length; index++)
            {
                children[index] = transform.GetChild(index) as RectTransform;
                if (children[index] == null) continue;

                drivers[index] = UITransformDriver.For(children[index]);

                children[index].TryGetComponent(out LocalizedLayout layout);
                ownLayouts[index] = layout;
            }
        }

        IEnumerator BringChildrenIn()
        {
            // Decided once, here. A child that is switched off part-way through — the signed-out
            // hint, the moment somebody turns out to be signed in — still has to be walked to the
            // end of its arrival, or it is left at the bottom of the screen with no alpha for
            // whenever it is switched back on.
            arriving.Clear();

            for (int index = 0; index < children.Length; index++)
            {
                // A child that is already switched off is left alone. Moving one would strand it
                // out of place, and it is not part of this arrival anyway.
                if (children[index] == null || !children[index].gameObject.activeSelf) continue;

                arriving.Add(index);
                Place(index, 0f);
            }

            if (startDelay > 0f) yield return new WaitForSecondsRealtime(startDelay);

            foreach (int index in arriving)
            {
                if (children[index] == null) continue;

                StartCoroutine(BringIn(index));

                if (gapBetweenChildren > 0f) yield return new WaitForSecondsRealtime(gapBetweenChildren);
            }
        }

        IEnumerator BringIn(int index)
        {
            for (float elapsed = 0f; elapsed < childDuration; elapsed += Time.unscaledDeltaTime)
            {
                Place(index, entrance.Evaluate(elapsed / childDuration));
                yield return null;
            }

            Place(index, 1f);
        }

        /// <summary>Places a child between hidden (0) and settled (1). Above 1 is the overshoot.</summary>
        void Place(int index, float progress)
        {
            RectTransform child = children[index];
            UITransformDriver driver = drivers[index];

            if (driver == null) return;

            RestingPositionOf(index, driver);

            driver.SetOffset(UIMotionChannel.Entrance, Vector3.down * (riseDistance * (1f - progress)));

            // A multiplier, not a size. The child's own authored scale is the driver's to hold, so
            // a button built a hair under full size is still that size once it has arrived.
            driver.SetScale(UIMotionChannel.Entrance,
                Vector3.one * Mathf.LerpUnclamped(startScale, 1f, progress));

            if (child.TryGetComponent(out CanvasGroup group)) group.alpha = Mathf.Clamp01(progress);
        }

        /// <summary>
        /// Tells a child's driver where the child belongs. Normally that is where the scene put it,
        /// which the driver already knows — but a child with a <see cref="LocalizedLayout"/> has a
        /// place per language, and its own answer wins over the one language the scene was saved in.
        /// </summary>
        void RestingPositionOf(int index, UITransformDriver driver)
        {
            LocalizedLayout layout = ownLayouts[index];

            if (layout != null && layout.TryGetHome(out Vector2 home)) driver.SetBasePosition(home);
        }
    }
}
