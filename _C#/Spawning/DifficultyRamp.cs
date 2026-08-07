using System;
using UnityEngine;
using Random = UnityEngine.Random;

namespace NeonKatana
{
    /// <summary>
    /// How hard the game is right now. Pressure is read from two things at once — how long the run
    /// has lasted and how well it is going — so a player who scores quickly meets the harder game
    /// sooner, and a player who is struggling is not rushed into it by the clock alone.
    /// <para>
    /// Everything it returns is continuous. Nothing here rounds or crosses a threshold, because a
    /// rounded wave size is exactly what made the old ramp feel like three separate settings
    /// rather than one gradual climb.
    /// </para>
    /// </summary>
    [Serializable]
    public class DifficultyRamp
    {
        [Header("What raises the pressure")]
        [Tooltip("Seconds of play that count as a full run.")]
        [SerializeField, Min(1f)] float secondsToPeak = 180f;
        [Tooltip("Score that counts as a full run.")]
        [SerializeField, Min(1)] int scoreToPeak = 120;
        [Tooltip("0 leans entirely on the clock, 1 entirely on the score.")]
        [SerializeField, Range(0f, 1f)] float scoreWeight = 0.5f;
        [Tooltip("Shape of the climb. Straight by default, so the change is even the whole way.")]
        [SerializeField] AnimationCurve climb = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        [Header("Pace")]
        [SerializeField] Vector2 openingDelay = new Vector2(0.9f, 1.4f);
        [SerializeField] Vector2 hardestDelay = new Vector2(0.25f, 0.55f);

        [Header("Bombs")]
        [SerializeField, Range(0f, 1f)] float openingBombChance = 0.03f;
        [SerializeField, Range(0f, 1f)] float hardestBombChance = 0.22f;
        [Tooltip("Seconds of play before bombs appear at all, so the first moments are safe.")]
        [SerializeField, Min(0f)] float bombFreeOpening = 8f;

        [Header("Volume")]
        [Tooltip("Things in the air at once at the start. Fractions are fine — see ThrowCountAt.")]
        [SerializeField, Min(1f)] float openingThrowCount = 1f;
        [SerializeField, Min(1f)] float hardestThrowCount = 3f;

        /// <summary>
        /// Where the run sits between its opening (0) and its hardest (1). Time and score are
        /// blended before the curve, so the curve shapes the whole feeling rather than one input.
        /// </summary>
        public float PressureAt(float playedSeconds, int score)
        {
            float fromTime = Mathf.Clamp01(playedSeconds / secondsToPeak);
            float fromScore = Mathf.Clamp01(score / (float)scoreToPeak);

            return climb.Evaluate(Mathf.Lerp(fromTime, fromScore, scoreWeight));
        }

        public float DelayAfterThrow(float pressure) => Random.Range(
            Mathf.Lerp(openingDelay.x, hardestDelay.x, pressure),
            Mathf.Lerp(openingDelay.y, hardestDelay.y, pressure));

        public float BombChanceAt(float pressure, float playedSeconds)
        {
            if (playedSeconds < bombFreeOpening) return 0f;

            return Mathf.Lerp(openingBombChance, hardestBombChance, pressure);
        }

        /// <summary>
        /// How many things to throw, as a fraction. A value of 1.3 means every wave is at least
        /// one and roughly a third of them are two — which is how the count grows smoothly instead
        /// of every wave in the game changing size the moment a threshold is crossed.
        /// The caller turns the fraction into a whole number.
        /// </summary>
        public float ThrowCountAt(float pressure) =>
            Mathf.Lerp(openingThrowCount, hardestThrowCount, pressure);
    }
}
