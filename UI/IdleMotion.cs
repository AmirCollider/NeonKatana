using UnityEngine;

namespace NeonKatana
{
    /// <summary>
    /// Keeps something gently moving while nothing is happening — a logo breathing, a button
    /// drifting, a badge tilting. Every instance is given its own starting phase, so a row of them
    /// looks like several things being alive rather than one thing copied.
    /// <para>Runs on unscaled time, so it carries on behind a paused game.</para>
    /// <para>
    /// The movement is handed to <see cref="UITransformDriver"/> rather than written to the
    /// transform, because on the login button this is not the only thing moving it.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public class IdleMotion : MonoBehaviour
    {
        [Header("Drift")]
        [Tooltip("How far it moves up and down.")]
        [SerializeField] float bobDistance = 8f;
        [Tooltip("How far it moves side to side.")]
        [SerializeField] float swayDistance;

        [Header("Breathe")]
        [Tooltip("How much bigger and smaller it gets. 0.02 is a light breath.")]
        [SerializeField, Range(0f, 0.3f)] float pulseAmount = 0.02f;

        [Header("Tilt")]
        [Tooltip("Degrees it rocks either side of straight.")]
        [SerializeField] float tiltAngle;

        [Header("Speed")]
        [Tooltip("Full trips per second.")]
        [SerializeField, Min(0.01f)] float cyclesPerSecond = 0.3f;
        [Tooltip("Start somewhere random in the cycle, so copies do not move as one.")]
        [SerializeField] bool randomStart = true;

        UITransformDriver driver;
        float phase;

        void Awake()
        {
            driver = UITransformDriver.For(this);
            phase = randomStart ? Random.Range(0f, Mathf.PI * 2f) : 0f;
        }

        void OnDisable()
        {
            // Stop counting, or a hidden panel keeps a half-finished pose.
            if (driver != null) driver.Clear(UIMotionChannel.Idle);
        }

        void Update()
        {
            if (driver == null) return;

            float time = Time.unscaledTime * cyclesPerSecond * Mathf.PI * 2f + phase;
            float wave = Mathf.Sin(time);

            driver.SetOffset(UIMotionChannel.Idle, new Vector3(
                Mathf.Sin(time * 0.6f) * swayDistance,
                wave * bobDistance,
                0f));

            driver.SetScale(UIMotionChannel.Idle,
                pulseAmount > 0f ? Vector3.one * (1f + wave * pulseAmount) : Vector3.one);

            driver.SetRotation(UIMotionChannel.Idle,
                tiltAngle != 0f ? Quaternion.Euler(0f, 0f, wave * tiltAngle) : Quaternion.identity);
        }
    }
}
