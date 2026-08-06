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

        Vector3 restingScale;
        bool held;

        void Awake() => restingScale = transform.localScale;

        void OnDisable()
        {
            held = false;
            transform.localScale = restingScale;
        }

        void Update()
        {
            Vector3 wanted = held ? restingScale * pressedScale : restingScale;

            // Framerate-independent easing: the same feel at 30 and at 120.
            transform.localScale = Vector3.Lerp(
                transform.localScale, wanted, 1f - Mathf.Exp(-springSpeed * Time.unscaledDeltaTime));
        }

        public void OnPointerDown(PointerEventData eventData) => held = true;

        public void OnPointerUp(PointerEventData eventData) => held = false;
    }
}
