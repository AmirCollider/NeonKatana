using System.Collections.Generic;
using UnityEngine;

namespace NeonKatana
{
    /// <summary>
    /// The one thing the player must not cut. Cutting it bursts every fruit on screen and ends the
    /// run straight away, whatever lives were left. Letting it fall is harmless.
    /// </summary>
    public class Bomb : Throwable
    {
        [Header("Explosion")]
        [Tooltip("Optional effect spawned where the bomb was cut.")]
        [SerializeField] GameObject explosionEffect;
        [SerializeField] AudioClip explosionSound;
        [SerializeField, Range(0f, 1f)] float explosionVolume = 1f;

        protected override void OnSliced()
        {
            if (!TryClaim()) return;

            BurstEveryFruitOnScreen();

            if (explosionEffect != null)
                Instantiate(explosionEffect, transform.position, Quaternion.identity);

            if (explosionSound != null)
                AudioSource.PlayClipAtPoint(explosionSound, transform.position, explosionVolume);

            if (GameManager.Instance != null) GameManager.Instance.EndRun(GameOverCause.BombSliced);

            Destroy(gameObject);
        }

        /// <summary>The blast takes the rest of the screen with it.</summary>
        static void BurstEveryFruitOnScreen()
        {
            // Copy first: slicing a fruit takes it out of the shared list while we walk it.
            var caughtInBlast = new List<Fruit>(Fruit.InFlightFruit);

            foreach (Fruit fruit in caughtInBlast)
                if (fruit != null) fruit.Slice(earnsScore: false);
        }
    }
}
