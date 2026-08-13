using UnityEngine;
using UnityEngine.EventSystems;

namespace NeonKatana
{
    /// <summary>
    /// Gives a button something to say when it is touched: it sinks under the finger and springs
    /// back on release. Without this a press is only a scene change, and the button itself never
    /// admits it was pressed.
    /// </summary>
    [DisallowMultipleComponent]
    public class ButtonPress : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        [Tooltip("How far it sinks while held.")]
        [SerializeField, Range(0.8f, 1f)] float pressedScale = 0.94f;
        [Tooltip("How fast it settles into and out of the press.")]
        [SerializeField, Min(1f)] float springSpeed = 14f;

        UITransformDriver driver;
        bool held;

        /// <summary>
        /// How far into the press it is, as a multiplier of the element's own size.
        /// <para>
        /// The spring is kept here, on a number of this component's own, rather than read back off
        /// <c>localScale</c>. Reading it back is what made this fight <see cref="IdleMotion"/> on
        /// the login button: it would ease toward its resting size from whatever the breathe had
        /// just set, undoing the breathe, which then reapplied it.
        /// </para>
        /// </summary>
        float sunk = 1f;

        void Awake() => driver = UITransformDriver.For(this);

        void OnDisable()
        {
            held = false;
            sunk = 1f;

            if (driver != null) driver.Clear(UIMotionChannel.Press);
        }

        void Update()
        {
            if (driver == null) return;

            float wanted = held ? pressedScale : 1f;

            // Framerate-independent easing: the same feel at 30 and at 120.
            sunk = Mathf.Lerp(sunk, wanted, 1f - Mathf.Exp(-springSpeed * Time.unscaledDeltaTime));

            driver.SetScale(UIMotionChannel.Press, Vector3.one * sunk);
        }

        public void OnPointerDown(PointerEventData eventData) => held = true;

        public void OnPointerUp(PointerEventData eventData) => held = false;
    }
}
