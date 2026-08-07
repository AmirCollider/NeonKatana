using UnityEngine;

namespace NeonKatana
{
    /// <summary>
    /// One of the launch slots along the bottom of the screen. Each slot carries its own throwing
    /// arc, so a fruit no longer has to work out where it came from by comparing its own position
    /// against every point in the scene.
    /// </summary>
    [DisallowMultipleComponent]
    public class SpawnPoint : MonoBehaviour
    {
        [Tooltip("Sideways speed range. Negative throws to the left, positive throws to the right.")]
        [SerializeField] Vector2 horizontalSpeed = new Vector2(-3.5f, 3.5f);

        [Tooltip("Upward speed range. Higher values throw higher.")]
        [SerializeField] Vector2 verticalSpeed = new Vector2(11.5f, 14.5f);

        /// <summary>Picks a throw somewhere inside this point's arc.</summary>
        public Vector3 CreateLaunchVelocity() => new Vector3(
            Random.Range(horizontalSpeed.x, horizontalSpeed.y),
            Random.Range(verticalSpeed.x, verticalSpeed.y),
            0f);

#if UNITY_EDITOR
        /// <summary>Draws the corners of the arc in the scene view so it can be tuned by eye.</summary>
        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.25f, 1f, 0.6f, 0.9f);

            DrawTrajectory(new Vector3(horizontalSpeed.x, verticalSpeed.x, 0f));
            DrawTrajectory(new Vector3(horizontalSpeed.x, verticalSpeed.y, 0f));
            DrawTrajectory(new Vector3(horizontalSpeed.y, verticalSpeed.x, 0f));
            DrawTrajectory(new Vector3(horizontalSpeed.y, verticalSpeed.y, 0f));
        }

        void DrawTrajectory(Vector3 velocity)
        {
            const float stepSeconds = 0.05f;
            const int steps = 60;

            Vector3 from = transform.position;

            for (int step = 1; step <= steps; step++)
            {
                float elapsed = step * stepSeconds;
                Vector3 to = transform.position + velocity * elapsed + 0.5f * (elapsed * elapsed) * Physics.gravity;
                Gizmos.DrawLine(from, to);
                from = to;
            }
        }
#endif
    }
}
