using UnityEngine;

namespace NeonKatana
{
    /// <summary>
    /// Anything the spawner throws into the play area. A throwable is launched once, tumbles while
    /// it flies, and reacts to exactly two things: the blade cutting it, and the kill zone below
    /// the screen. Subclasses decide what each of those means.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public abstract class Throwable : MonoBehaviour
    {
        [Header("Tumble")]
        [Tooltip("Degrees per second the object spins as it flies. A speed is drawn once, so the spin stays smooth.")]
        [SerializeField] Vector2 spinSpeed = new Vector2(100f, 200f);

        Rigidbody body;
        Vector3 spinPerSecond;
        bool alreadySettled;

        protected virtual void Awake()
        {
            body = GetComponent<Rigidbody>();
            spinPerSecond = new Vector3(RandomSpin(), RandomSpin(), 0f);
        }

        /// <summary>Sets the object flying. Called by the spawner right after it is created.</summary>
        public void Launch(Vector3 velocity)
        {
            body.linearVelocity = velocity;
        }

        protected virtual void Update()
        {
            transform.Rotate(spinPerSecond * Time.deltaTime, Space.Self);
        }

        void OnTriggerEnter(Collider other)
        {
            if (alreadySettled) return;

            if (other.TryGetComponent(out Blade _))
            {
                // Subclasses claim it themselves, because a bomb blast can cut a fruit too.
                OnSliced();
                return;
            }

            // Claimed here so that a fruit crossing the kill zone on the very frame the blade
            // reaches it cannot both cost a life and burst into halves.
            if (other.TryGetComponent(out KillZone _) && TryClaim())
            {
                OnMissed();
            }
        }

        /// <summary>
        /// Claims this object's one and only ending. Two colliders can report a hit on the same
        /// frame, so every path that finishes the object has to come through here first.
        /// </summary>
        protected bool TryClaim()
        {
            if (alreadySettled) return false;

            alreadySettled = true;
            return true;
        }

        /// <summary>The blade reached the object.</summary>
        protected abstract void OnSliced();

        /// <summary>The object fell past the blade and left the screen.</summary>
        protected virtual void OnMissed() => Destroy(gameObject);

        float RandomSpin() => Random.Range(spinSpeed.x, spinSpeed.y) * (Random.value < 0.5f ? -1f : 1f);
    }
}
