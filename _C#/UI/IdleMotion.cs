using UnityEngine;

namespace NeonKatana
{
    /// <summary>
    /// Keeps something gently moving while nothing is happening — a logo breathing, a button
    /// drifting, a badge tilting. Every instance is given its own starting phase, so a row of them
    /// looks like several things being alive rather than one thing copied.
    /// <para>Runs on unscaled time, so it carries on behind a paused game.</para>
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

        Vector3 restingPosition;
        Vector3 restingScale;
        Quaternion restingRotation;
        float phase;

        void Awake()
        {
            restingPosition = transform.localPosition;
            restingScale = transform.localScale;
            restingRotation = transform.localRotation;

            phase = randomStart ? Random.Range(0f, Mathf.PI * 2f) : 0f;
        }

        void OnDisable()
        {
            // Put it back where it was authored, or a hidden panel keeps a half-finished pose.
            transform.localPosition = restingPosition;
            transform.localScale = restingScale;
            transform.localRotation = restingRotation;
        }

        void Update()
        {
            float time = Time.unscaledTime * cyclesPerSecond * Mathf.PI * 2f + phase;
            float wave = Mathf.Sin(time);

            transform.localPosition = restingPosition + new Vector3(
                Mathf.Sin(time * 0.6f) * swayDistance,
                wave * bobDistance,
                0f);

            if (pulseAmount > 0f) transform.localScale = restingScale * (1f + wave * pulseAmount);

            if (tiltAngle != 0f)
                transform.localRotation = restingRotation * Quaternion.Euler(0f, 0f, wave * tiltAngle);
        }
    }
}
