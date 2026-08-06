using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NeonKatana
{
    /// <summary>
    /// Shows one random juice splash where the fruit was cut and then fades it out. Before, a
    /// splash was switched on and left at full strength until the sliced fruit was deleted, so it
    /// hung in mid-air and then vanished in a single frame.
    /// </summary>
    public class JuiceSplash : MonoBehaviour
    {
        [Tooltip("The splash variants inside the prefab. Leave empty to use every child with a SpriteRenderer.")]
        [SerializeField] GameObject[] variants;

        [Header("Timing")]
        [Tooltip("Seconds the splash stays at full strength before it starts to fade.")]
        [SerializeField, Min(0f)] float holdDuration = 0.3f;
        [Tooltip("Seconds the fade itself takes.")]
        [SerializeField, Min(0.05f)] float fadeDuration = 1.2f;

        [Header("Placement")]
        [Tooltip("Turn the splash to face the camera, so it reads as juice on the backdrop.")]
        [SerializeField] bool faceCamera = true;
        [Tooltip("Random turn around the view axis, so the same splash does not always look the same.")]
        [SerializeField] Vector2 randomRoll = new Vector2(0f, 360f);

        void Start()
        {
            GameObject splash = ChooseVariant();
            if (splash == null) return;

            SettleInPlace(splash.transform);
            StartCoroutine(FadeAway(splash));
        }

        /// <summary>
        /// Takes the splash out of the pieces' hierarchy and points it at the camera. The splash
        /// ships as a child of a half, and the halves are spun every frame as they fall — left
        /// where it was, the splash tumbles with the fruit instead of staying on the backdrop.
        /// </summary>
        void SettleInPlace(Transform splash)
        {
            splash.SetParent(transform, worldPositionStays: true);

            if (!faceCamera) return;

            Camera camera = Camera.main;
            if (camera == null) return;

            splash.rotation = camera.transform.rotation
                              * Quaternion.Euler(0f, 0f, Random.Range(randomRoll.x, randomRoll.y));
        }

        GameObject ChooseVariant()
        {
            if (variants == null || variants.Length == 0) variants = FindSplashChildren();
            if (variants.Length == 0) return null;

            GameObject chosen = variants[Random.Range(0, variants.Length)];

            // The prefabs ship with their splash already switched on, so showing one variant means
            // switching the others off rather than just turning the winner on.
            foreach (GameObject variant in variants)
                if (variant != null) variant.SetActive(variant == chosen);

            return chosen;
        }

        /// <summary>
        /// The splash is the only sprite in a sliced fruit — the halves themselves are meshes — so
        /// every child with a SpriteRenderer is a splash variant.
        /// </summary>
        GameObject[] FindSplashChildren()
        {
            var found = new List<GameObject>();

            foreach (SpriteRenderer sprite in GetComponentsInChildren<SpriteRenderer>(includeInactive: true))
                found.Add(sprite.gameObject);

            return found.ToArray();
        }

        IEnumerator FadeAway(GameObject splash)
        {
            List<Fader> faders = CollectFaders(splash);

            if (holdDuration > 0f) yield return new WaitForSeconds(holdDuration);

            for (float elapsed = 0f; elapsed < fadeDuration; elapsed += Time.deltaTime)
            {
                float strength = 1f - (elapsed / fadeDuration);
                foreach (Fader fader in faders) fader.SetStrength(strength);
                yield return null;
            }

            splash.SetActive(false);
        }

        static List<Fader> CollectFaders(GameObject splash)
        {
            var faders = new List<Fader>();

            foreach (Renderer child in splash.GetComponentsInChildren<Renderer>(includeInactive: true))
            {
                Fader fader = Fader.For(child);
                if (fader != null) faders.Add(fader);
            }

            if (faders.Count == 0)
                Debug.LogWarning($"Nothing on '{splash.name}' can be faded, so the splash will pop out instead.", splash);

            return faders;
        }

        /// <summary>
        /// Dims one renderer, whichever way that renderer happens to store its colour. Sprites have
        /// a colour of their own; everything else is tinted through its material, which is named
        /// differently between the built-in shaders and URP.
        /// </summary>
        sealed class Fader
        {
            static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
            static readonly int ColorId = Shader.PropertyToID("_Color");

            readonly SpriteRenderer sprite;
            readonly Material material;
            readonly int colorId;
            readonly Color fullStrength;

            Fader(SpriteRenderer sprite)
            {
                this.sprite = sprite;
                fullStrength = sprite.color;
            }

            Fader(Material material, int colorId)
            {
                this.material = material;
                this.colorId = colorId;
                fullStrength = material.GetColor(colorId);
            }

            /// <summary>Returns null when the renderer has no colour anyone can fade.</summary>
            public static Fader For(Renderer renderer)
            {
                if (renderer is SpriteRenderer spriteRenderer) return new Fader(spriteRenderer);

                Material shared = renderer.sharedMaterial;
                if (shared == null) return null;

                if (shared.HasProperty(BaseColorId)) return new Fader(renderer.material, BaseColorId);
                if (shared.HasProperty(ColorId)) return new Fader(renderer.material, ColorId);

                return null;
            }

            public void SetStrength(float strength)
            {
                Color faded = fullStrength;
                faded.a = fullStrength.a * strength;

                if (sprite != null) sprite.color = faded;
                else material.SetColor(colorId, faded);
            }
        }
    }
}
