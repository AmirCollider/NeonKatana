using System.Collections.Generic;
using UnityEngine;

namespace NeonKatana
{
    /// <summary>
    /// A fruit the player is meant to cut. Cutting it swaps it for its halved prefab; letting it
    /// fall past the blade costs a life.
    /// </summary>
    public class Fruit : Throwable
    {
        [Header("Slicing")]
        [Tooltip("The matching prefab from Prefabs/3DModels/HalvedFruits.")]
        [SerializeField] SlicedFruit slicedPrefab;
        [Tooltip("Points for cutting this fruit.")]
        [SerializeField, Min(0)] int scoreValue = 1;

        static readonly List<Fruit> InFlight = new List<Fruit>();

        /// <summary>Every fruit currently on screen. A bomb going off needs to reach all of them.</summary>
        public static IReadOnlyList<Fruit> InFlightFruit => InFlight;

        void OnEnable() => InFlight.Add(this);

        void OnDisable() => InFlight.Remove(this);

        /// <summary>
        /// Cuts the fruit in two. Public because a bomb bursts every fruit on screen as well — and
        /// that burst is the player losing, so it is the one case that earns nothing.
        /// </summary>
        public void Slice(bool earnsScore)
        {
            if (!TryClaim()) return;

            if (slicedPrefab != null)
                // Upright, not at the fruit's tumbling angle: the halves and the flat juice splash
                // both read better facing the camera than spun to wherever the fruit happened to be.
                Instantiate(slicedPrefab, transform.position, Quaternion.identity);
            else
                Debug.LogWarning($"'{name}' has no sliced prefab assigned, so it just disappears.", this);

            if (earnsScore && ScoreKeeper.Instance != null) ScoreKeeper.Instance.Add(scoreValue);

            Destroy(gameObject);
        }

        protected override void OnSliced() => Slice(earnsScore: true);

        protected override void OnMissed()
        {
            if (GameManager.Instance != null) GameManager.Instance.LoseLife();
            Destroy(gameObject);
        }

        /// <summary>Empties the list when play mode starts without a domain reload.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStaticState() => InFlight.Clear();
    }
}
