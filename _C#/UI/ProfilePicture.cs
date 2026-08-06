using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace NeonKatana
{
    /// <summary>
    /// Shows the signed-in player's picture, and puts the placeholder back when they leave.
    /// <para>
    /// Nothing loaded it before. <see cref="PlayerProfile.photoURL"/> was read off the wire and
    /// thrown away, and the token's own <c>picture</c> claim was never even parsed — so the button
    /// stayed on its placeholder no matter who was signed in.
    /// </para>
    /// <para>
    /// It can cut the picture to a circle itself. That is worth having instead of a
    /// <see cref="Mask"/> over the button: a mask clips whatever moves underneath it, and this
    /// button has an <see cref="IdleMotion"/> on it that never stops moving — so the avatar drifts
    /// in and out of its own frame, half of it shaved off at the top of every bob. Cutting the
    /// picture leaves the button free to move as a whole.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public class ProfilePicture : MonoBehaviour
    {
        [Tooltip("The image to paint. Falls back to an Image on this object.")]
        [SerializeField] Image picture;

        [Tooltip("Shown while nobody is signed in. Left empty, whatever the image already had is used.")]
        [SerializeField] Sprite signedOutPicture;

        [Tooltip(
            "Cut the picture to a circle as it loads, rather than putting a Mask over the button. " +
            "For it to read as a circle rather than an oval, the image's own rectangle has to be " +
            "square — width and height the same on its RectTransform.")]
        [SerializeField] bool circular = true;

        [SerializeField, Min(1)] int requestTimeoutSeconds = 10;

        /// <summary>
        /// One download per address for the whole session. Both menu screens carry one of these,
        /// and the same face on both of them is not worth asking for twice.
        /// </summary>
        static readonly Dictionary<string, Sprite> Loaded = new Dictionary<string, Sprite>();

        string showing;
        Coroutine loading;

        void Awake()
        {
            if (picture == null) picture = GetComponent<Image>();

            // Whatever the scene was built with is the placeholder, unless one was named.
            if (signedOutPicture == null && picture != null) signedOutPicture = picture.sprite;
        }

        void Start()
        {
            if (SignInService.Instance != null) SignInService.Instance.SignedInChanged += Refresh;
            if (ProgressService.Instance != null) ProgressService.Instance.ProfileChanged += Refresh;

            Refresh();
        }

        void OnDestroy()
        {
            if (SignInService.Instance != null) SignInService.Instance.SignedInChanged -= Refresh;
            if (ProgressService.Instance != null) ProgressService.Instance.ProfileChanged -= Refresh;
        }

        /// <summary>Puts the right face on the button for whoever is signed in.</summary>
        public void Refresh()
        {
            string wanted = WantedPicture();

            if (string.IsNullOrEmpty(wanted))
            {
                showing = null;
                Show(signedOutPicture);
                return;
            }

            if (wanted == showing) return;

            if (Loaded.TryGetValue(wanted, out Sprite alreadyHere) && alreadyHere != null)
            {
                showing = wanted;
                Show(alreadyHere);
                return;
            }

            if (loading != null) StopCoroutine(loading);
            loading = StartCoroutine(Load(wanted));
        }

        /// <summary>
        /// Which picture belongs on screen. The player's own record wins: somebody who set a
        /// picture on the site meant it, and the token only ever carries whatever Google has.
        /// </summary>
        static string WantedPicture()
        {
            SignInService signIn = SignInService.Instance;

            if (signIn == null || !signIn.IsSignedIn) return null;

            PlayerProfile profile = ProgressService.Instance != null
                ? ProgressService.Instance.Profile
                : null;

            if (profile != null && !string.IsNullOrWhiteSpace(profile.photoURL)) return profile.photoURL.Trim();

            return !string.IsNullOrWhiteSpace(signIn.PictureUrl) ? signIn.PictureUrl.Trim() : null;
        }

        IEnumerator Load(string url)
        {
            using UnityWebRequest request = UnityWebRequestTexture.GetTexture(url);
            request.timeout = requestTimeoutSeconds;

            yield return request.SendWebRequest();

            loading = null;

            if (request.result != UnityWebRequest.Result.Success)
            {
                // The placeholder is already on screen and is a perfectly good answer. A player
                // with no signal does not need to be told their own face is missing.
                Debug.Log($"The player's picture could not be loaded: {request.responseCode} {request.error}");
                yield break;
            }

            Texture2D texture = DownloadHandlerTexture.GetContent(request);
            if (texture == null) yield break;

            texture.wrapMode = TextureWrapMode.Clamp;

            if (circular) RoundOff(texture);

            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f));

            Loaded[url] = sprite;
            showing = url;

            Show(sprite);
        }

        /// <summary>
        /// Clears the corners of a square picture, leaving the largest circle that fits inside it.
        /// The last pixel of the rim is faded rather than cut, or the edge draws as a staircase.
        /// </summary>
        static void RoundOff(Texture2D texture)
        {
            int width = texture.width;
            int height = texture.height;

            float outer = Mathf.Min(width, height) * 0.5f;
            float inner = Mathf.Max(0f, outer - 1f);
            var middle = new Vector2(width * 0.5f, height * 0.5f);

            Color32[] pixels = texture.GetPixels32();

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), middle);
                    if (distance <= inner) continue;

                    int index = y * width + x;
                    Color32 pixel = pixels[index];

                    float strength = distance >= outer ? 0f : 1f - (distance - inner) / (outer - inner);

                    pixels[index] = new Color32(pixel.r, pixel.g, pixel.b, (byte)(pixel.a * strength));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
        }

        void Show(Sprite sprite)
        {
            if (picture == null || sprite == null) return;

            picture.sprite = sprite;
        }

        /// <summary>Empties the cache when play mode starts without a domain reload.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStaticState() => Loaded.Clear();
    }
}
