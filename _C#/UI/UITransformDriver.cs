using UnityEngine;

namespace NeonKatana
{
    /// <summary>Who is asking for a piece of an element's movement.</summary>
    public enum UIMotionChannel
    {
        /// <summary>The drift and breathe of <see cref="IdleMotion"/>.</summary>
        Idle = 0,

        /// <summary>The sink under a finger, from <see cref="ButtonPress"/>.</summary>
        Press = 1,

        /// <summary>The arrival, from <see cref="StaggeredEntrance"/>.</summary>
        Entrance = 2,
    }

    /// <summary>
    /// The single owner of one element's position, scale and rotation.
    /// <para>
    /// Three components used to write <c>localScale</c> on the login button —
    /// <see cref="IdleMotion"/> breathing, <see cref="ButtonPress"/> springing and
    /// <see cref="StaggeredEntrance"/> arriving — with nothing deciding between them. Unity puts no
    /// order on <c>Update</c> across components, so the button ended each frame at whichever of the
    /// three ran last, and every frame could pick differently. That is the whole of "the button
    /// changes size on its own".
    /// </para>
    /// <para>
    /// It was also permanent, not just a flicker: <see cref="StaggeredEntrance"/> measured "where
    /// this child belongs" by reading <c>localScale</c>, and reading it after the breathe had
    /// started meant a pulsed value — 1.019, say — was written down as the button's real size and
    /// used for every arrival afterwards.
    /// </para>
    /// <para>
    /// So nobody writes the transform any more. Each of the three says what it wants as its own
    /// contribution, this composes them once in <c>LateUpdate</c>, and the authored pose is read
    /// once, before anything has had a chance to move.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(1000)]
    public class UITransformDriver : MonoBehaviour
    {
        const int Channels = 3;

        /// <summary>Where the scene put this element. Offsets are measured from here.</summary>
        Vector3 basePosition;
        Vector3 baseScale;
        Quaternion baseRotation;

        readonly Vector3[] offsets = new Vector3[Channels];
        readonly Vector3[] scales = new Vector3[Channels];
        readonly Quaternion[] rotations = new Quaternion[Channels];

        RectTransform rect;
        bool measured;

        /// <summary>
        /// The driver for an element, adding one if it has none. Nothing has to be put on the
        /// prefab by hand — a component that wants to move something asks for this and gets it.
        /// </summary>
        public static UITransformDriver For(Component owner)
        {
            if (owner == null) return null;

            if (!owner.TryGetComponent(out UITransformDriver driver))
                driver = owner.gameObject.AddComponent<UITransformDriver>();

            driver.Measure();
            return driver;
        }

        void Awake() => Measure();

        /// <summary>
        /// Reads the authored pose, once.
        /// <para>
        /// It is safe to call from anybody's <c>Awake</c> or <c>OnEnable</c> because nothing writes
        /// the transform before the first <c>Update</c> — so whenever this first runs, what it is
        /// reading is still what the scene was built with.
        /// </para>
        /// </summary>
        void Measure()
        {
            if (measured) return;
            measured = true;

            rect = transform as RectTransform;

            basePosition = rect != null ? (Vector3)rect.anchoredPosition3D : transform.localPosition;
            baseScale = transform.localScale;
            baseRotation = transform.localRotation;

            for (int channel = 0; channel < Channels; channel++)
            {
                offsets[channel] = Vector3.zero;
                scales[channel] = Vector3.one;
                rotations[channel] = Quaternion.identity;
            }
        }

        /// <summary>Where this element belongs, before anything moved it.</summary>
        public Vector3 BasePosition
        {
            get { Measure(); return basePosition; }
        }

        /// <summary>The size the scene was built at, before anything scaled it.</summary>
        public Vector3 BaseScale
        {
            get { Measure(); return baseScale; }
        }

        /// <summary>
        /// Moves the place this element returns to. <see cref="LocalizedLayout"/> is the caller
        /// that needs it: the element has a home per language, and an offset measured from the
        /// wrong one is wrong in every language but the one the scene was saved in.
        /// </summary>
        public void SetBasePosition(Vector2 home)
        {
            Measure();
            basePosition = new Vector3(home.x, home.y, basePosition.z);
        }

        /// <summary>How far this channel is moving the element from where it belongs.</summary>
        public void SetOffset(UIMotionChannel channel, Vector3 offset)
        {
            Measure();
            offsets[(int)channel] = offset;
        }

        /// <summary>What this channel multiplies the element's size by. One leaves it alone.</summary>
        public void SetScale(UIMotionChannel channel, Vector3 multiplier)
        {
            Measure();
            scales[(int)channel] = multiplier;
        }

        /// <summary>What this channel turns the element by, on top of how it was authored.</summary>
        public void SetRotation(UIMotionChannel channel, Quaternion rotation)
        {
            Measure();
            rotations[(int)channel] = rotation;
        }

        /// <summary>This channel is done. Its contribution stops counting.</summary>
        public void Clear(UIMotionChannel channel)
        {
            Measure();

            int index = (int)channel;
            offsets[index] = Vector3.zero;
            scales[index] = Vector3.one;
            rotations[index] = Quaternion.identity;
        }

        void LateUpdate() => Apply();

        void OnDisable()
        {
            // Back to the authored pose, or a hidden panel keeps a half-finished one and shows it
            // the moment it is switched on again.
            for (int channel = 0; channel < Channels; channel++) Clear((UIMotionChannel)channel);

            Apply();
        }

        void Apply()
        {
            Measure();

            Vector3 position = basePosition;
            Vector3 scale = baseScale;
            Quaternion rotation = baseRotation;

            for (int channel = 0; channel < Channels; channel++)
            {
                position += offsets[channel];

                Vector3 multiplier = scales[channel];
                scale = new Vector3(scale.x * multiplier.x, scale.y * multiplier.y, scale.z * multiplier.z);

                rotation *= rotations[channel];
            }

            if (rect != null) rect.anchoredPosition3D = position;
            else transform.localPosition = position;

            transform.localScale = scale;
            transform.localRotation = rotation;
        }
    }
}
