using UnityEngine;

namespace NeonKatana
{
    /// <summary>
    /// The strip below the play area. Anything that reaches it has left the screen for good.
    /// It carries no logic of its own: throwables look for this component instead of a tag, so the
    /// zone can be renamed or moved without breaking anything.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public class KillZone : MonoBehaviour
    {
        void Reset()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        void OnValidate()
        {
            if (TryGetComponent(out Collider zoneCollider) && !zoneCollider.isTrigger)
                Debug.LogWarning($"The collider on '{name}' is not a trigger, so nothing will fall through it.", this);
        }
    }
}
