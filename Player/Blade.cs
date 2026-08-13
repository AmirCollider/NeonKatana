using UnityEngine;
using UnityEngine.InputSystem;

namespace NeonKatana
{
    /// <summary>
    /// The katana the player drags across the screen. Its collider is only live while a finger or
    /// mouse button is held down, so a resting cursor cannot cut anything, and the trail is cleared
    /// between swings so lifting a finger and pressing somewhere else does not draw a streak across
    /// the whole screen.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class Blade : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Camera the swipe is read through. Falls back to the main camera.")]
        [SerializeField] Camera viewCamera;
        [Tooltip("Trail drawn behind the blade. Falls back to a TrailRenderer on a child.")]
        [SerializeField] TrailRenderer trail;

        [Header("Feel")]
        [Tooltip("World units per second the blade must travel before it cuts. 0 cuts on any touch.")]
        [SerializeField, Min(0f)] float minimumSliceSpeed = 0f;

        Collider bladeCollider;
        float distanceToBladePlane;
        Vector3 previousPosition;
        bool isSwinging;

        void Awake()
        {
            bladeCollider = GetComponent<Collider>();

            if (viewCamera == null) viewCamera = Camera.main;
            if (trail == null) trail = GetComponentInChildren<TrailRenderer>();

            // The blade slides along a fixed plane in front of the camera; the swipe is projected
            // onto that plane rather than onto the camera itself.
            distanceToBladePlane = viewCamera != null
                ? Mathf.Abs(transform.position.z - viewCamera.transform.position.z)
                : 0f;

            EndSwing();
        }

        void Start()
        {
            if (viewCamera == null)
                Debug.LogError($"{nameof(Blade)} has no camera to read the swipe through.", this);
        }

        void Update()
        {
            if (viewCamera == null) return;

            // Asked for each frame rather than subscribed to, because the blade has to stand down
            // for two different reasons — the run ending and the game being paused.
            if (GameManager.Instance != null && !GameManager.Instance.IsPlaying)
            {
                if (isSwinging) EndSwing();
                return;
            }

            Pointer pointer = Pointer.current;

            if (pointer == null || !pointer.press.isPressed)
            {
                if (isSwinging) EndSwing();
                return;
            }

            Vector2 screenPosition = pointer.position.ReadValue();

            // A touch device has no meaningful position between touches and reports NaN. Handing
            // that to the camera puts the blade at NaN and logs "Screen position out of view
            // frustum" every frame, so the frame is skipped instead.
            if (!IsReadable(screenPosition))
            {
                if (isSwinging) EndSwing();
                return;
            }

            Vector3 target = ScreenToBladePlane(screenPosition);

            if (isSwinging) ContinueSwing(target);
            else BeginSwing(target);
        }

        static bool IsReadable(Vector2 screenPosition) =>
            !float.IsNaN(screenPosition.x) && !float.IsNaN(screenPosition.y) &&
            !float.IsInfinity(screenPosition.x) && !float.IsInfinity(screenPosition.y);

        void BeginSwing(Vector3 target)
        {
            transform.position = target;
            previousPosition = target;
            isSwinging = true;

            // Clear before emitting, or the trail joins this swing to wherever the last one ended.
            if (trail != null)
            {
                trail.Clear();
                trail.emitting = true;
            }

            bladeCollider.enabled = minimumSliceSpeed <= 0f;
        }

        void ContinueSwing(Vector3 target)
        {
            float speed = Time.deltaTime > 0f
                ? Vector3.Distance(target, previousPosition) / Time.deltaTime
                : 0f;

            transform.position = target;
            previousPosition = target;

            bladeCollider.enabled = speed >= minimumSliceSpeed;
        }

        void EndSwing()
        {
            isSwinging = false;
            bladeCollider.enabled = false;
            if (trail != null) trail.emitting = false;
        }

        Vector3 ScreenToBladePlane(Vector2 screenPosition)
        {
            Vector3 onPlane = viewCamera.ScreenToWorldPoint(
                new Vector3(screenPosition.x, screenPosition.y, distanceToBladePlane));

            return new Vector3(onPlane.x, onPlane.y, transform.position.z);
        }
    }
}
