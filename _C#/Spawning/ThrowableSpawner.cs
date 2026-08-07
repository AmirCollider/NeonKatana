using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NeonKatana
{
    /// <summary>
    /// Keeps throwing fruit — and, once the player has settled in, bombs — up from the spawn points
    /// until the run ends. The spawner is the only thing that knows where a throwable came from, so
    /// it hands the launch velocity straight to what it created.
    /// </summary>
    public class ThrowableSpawner : MonoBehaviour
    {
        [Header("What to throw")]
        [SerializeField] Fruit[] fruitPrefabs;
        [SerializeField] Bomb bombPrefab;

        [Header("Where from")]
        [Tooltip("Leave empty to use every SpawnPoint in the scene.")]
        [SerializeField] SpawnPoint[] spawnPoints;

        [Header("Difficulty")]
        [SerializeField, Min(0f)] float firstThrowDelay = 1f;
        [SerializeField] DifficultyRamp difficulty = new DifficultyRamp();

        /// <summary>Reused between waves so a busy wave does not allocate.</summary>
        readonly List<SpawnPoint> pointsThisWave = new List<SpawnPoint>();

        void Start()
        {
            if (spawnPoints == null || spawnPoints.Length == 0)
                spawnPoints = FindObjectsByType<SpawnPoint>();

            if (spawnPoints.Length == 0)
            {
                Debug.LogError($"{nameof(ThrowableSpawner)} has no spawn points, so nothing will be thrown.", this);
                return;
            }

            if ((fruitPrefabs == null || fruitPrefabs.Length == 0) && bombPrefab == null)
            {
                Debug.LogError($"{nameof(ThrowableSpawner)} has nothing to throw.", this);
                return;
            }

            StartCoroutine(ThrowUntilTheRunEnds());
        }

        IEnumerator ThrowUntilTheRunEnds()
        {
            yield return new WaitForSeconds(firstThrowDelay);

            while (GameManager.Instance == null || !GameManager.Instance.IsGameOver)
            {
                float playedSeconds = GameManager.Instance != null ? GameManager.Instance.RunSeconds : 0f;
                int score = ScoreKeeper.Instance != null ? ScoreKeeper.Instance.Score : 0;
                float pressure = difficulty.PressureAt(playedSeconds, score);

                ThrowWave(pressure, playedSeconds);

                yield return new WaitForSeconds(difficulty.DelayAfterThrow(pressure));
            }
        }

        /// <summary>
        /// Throws one wave. Each thing in a wave leaves from a different point, so a wave arrives
        /// spread across the screen instead of stacked on top of itself.
        /// </summary>
        void ThrowWave(float pressure, float playedSeconds)
        {
            int count = Mathf.Min(WholeCountFrom(difficulty.ThrowCountAt(pressure)), spawnPoints.Length);
            float bombChance = difficulty.BombChanceAt(pressure, playedSeconds);

            PickPointsForWave(count);

            // At most one bomb per wave: two at once is a coin flip, not a decision.
            bool bombThrown = false;

            foreach (SpawnPoint point in pointsThisWave)
            {
                bool wantsBomb = !bombThrown && bombPrefab != null && Random.value < bombChance;
                Throwable prefab = wantsBomb ? bombPrefab : PickFruit();

                if (prefab == null) continue;
                if (wantsBomb) bombThrown = true;

                Throwable thrown = Instantiate(prefab, point.transform.position, Quaternion.identity);
                thrown.Launch(point.CreateLaunchVelocity());
            }
        }

        /// <summary>
        /// Turns a fractional wave size into a real one. At 1.3 this gives one most of the time
        /// and two now and then, so the wave size grows a little every wave instead of the whole
        /// game changing shape the moment a threshold is crossed.
        /// </summary>
        static int WholeCountFrom(float wanted)
        {
            int whole = Mathf.FloorToInt(wanted);

            if (Random.value < wanted - whole) whole++;

            return Mathf.Max(1, whole);
        }

        /// <summary>Takes <paramref name="count"/> different points, chosen without repeats.</summary>
        void PickPointsForWave(int count)
        {
            pointsThisWave.Clear();
            pointsThisWave.AddRange(spawnPoints);

            // Partial shuffle: only the first `count` entries have to end up random.
            for (int index = 0; index < count; index++)
            {
                int pick = Random.Range(index, pointsThisWave.Count);
                (pointsThisWave[index], pointsThisWave[pick]) = (pointsThisWave[pick], pointsThisWave[index]);
            }

            pointsThisWave.RemoveRange(count, pointsThisWave.Count - count);
        }

        Throwable PickFruit()
        {
            if (fruitPrefabs == null || fruitPrefabs.Length == 0) return bombPrefab;

            return fruitPrefabs[Random.Range(0, fruitPrefabs.Length)];
        }
    }
}
