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

        /// <summary>How many times a picture that would not load is asked for again.</summary>
        const int AttemptLimit = 3;

        /// <summary>The wait before the next attempt, multiplied by which attempt this is.</summary>
        const float RetryDelaySeconds = 1.5f;

        string showing;
        Coroutine loading;

        /// <summary>The services this is listening to. See <see cref="SignInDisplay"/>.</summary>
        SignInService signInService;
        ProgressService progressService;

        /// <summary>Whether the "signed in but no picture" note has already been made.</summary>
        bool saidThereIsNoPicture;

        void Awake()
        {
            if (picture == null) picture = GetComponent<Image>();

            // Whatever the scene was built with is the placeholder, unless one was named.
            if (signedOutPicture == null && picture != null) signedOutPicture = picture.sprite;
        }

        void Start()
        {
            signInService = SignInService.Instance;
            progressService = ProgressService.Instance;

            if (signInService != null) signInService.SignedInChanged += Refresh;
            if (progressService != null) progressService.ProfileChanged += Refresh;

            Refresh();
        }

        /// <summary>
        /// Catches up on a change made while this screen was hidden. See <see cref="SignInDisplay"/>.
        /// <para>
        /// It matters twice over here: <see cref="Refresh"/> cannot start a download on a switched
        /// off object, so a new player's face is not merely missed while the screen is closed — it
        /// is skipped, and without this nothing would ever ask for it again.
        /// </para>
        /// </summary>
        void OnEnable()
        {
            if (signInService != null || progressService != null) Refresh();
        }

        void OnDestroy()
        {
            if (signInService != null) signInService.SignedInChanged -= Refresh;
            if (progressService != null) progressService.ProfileChanged -= Refresh;
        }

        /// <summary>Puts the right face on the button for whoever is signed in.</summary>
        public void Refresh()
        {
            string wanted = WantedPicture();

            if (string.IsNullOrEmpty(wanted))
            {
                ExplainWhyThereIsNoPicture();

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

            // A coroutine cannot be started on an object that is switched off, and both menu
            // screens carry one of these — the account screen's spends most of its life inactive
            // while the services it listens to go on raising events at it.
            if (!isActiveAndEnabled) return;

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

        /// <summary>
        /// Says, once, which of the three sources came back empty.
        /// <para>
        /// "The picture never changes" is the same on screen whether nobody is signed in, the
        /// record has not arrived, or the token carries no <c>picture</c> claim — and those are
        /// three different things to go and fix. Staying quiet about it is what made this take so
        /// long to place.
        /// </para>
        /// </summary>
        void ExplainWhyThereIsNoPicture()
        {
            SignInService signIn = SignInService.Instance;

            if (signIn == null)
            {
                Debug.LogWarning($"'{name}' found no {nameof(SignInService)}, so it can only ever show the placeholder.", this);
                return;
            }

            if (!signIn.IsSignedIn) return;   // Signed out is not a fault. The placeholder is right.

            // Once. This runs on every sign-in change and every record that lands, and there is a
            // perfectly ordinary moment — between the token arriving and the record following it —
            // when there is genuinely nothing to show yet. Saying so each time buries the case
            // where it never resolves, which is the only case worth reading.
            if (saidThereIsNoPicture) return;
            saidThereIsNoPicture = true;

            PlayerProfile profile = ProgressService.Instance != null ? ProgressService.Instance.Profile : null;

            Debug.LogWarning(
                $"'{name}' is signed in but has no picture to show. " +
                $"The player's record {(profile == null ? "has not arrived yet" : "carries no photoURL")}, " +
                $"and the sign-in token carries no picture claim either. " +
                "Nothing is broken here — whichever of those two should be carrying the address is not.",
                this);
        }

        /// <summary>
        /// Fetches the picture, and asks again when the answer was not really an answer.
        /// <para>
        /// One attempt was not enough. A failed load reports <c>0</c> and a transport message —
        /// "Access denied", "Cannot connect", an empty error — none of which mean the picture is
        /// missing; they mean the request did not complete. In the editor a scene change is a
        /// reliable way to produce one, and there was nothing after it: the placeholder went up
        /// and stayed up until the whole app was restarted, because the only thing that asks again
        /// is somebody signing in or out.
        /// </para>
        /// </summary>
        IEnumerator Load(string url)
        {
            for (int attempt = 0; attempt < AttemptLimit; attempt++)
            {
                if (attempt > 0) yield return new WaitForSecondsRealtime(RetryDelaySeconds * attempt);

                // Whoever we are meant to be showing may have changed while we waited.
                if (WantedPicture() != url) break;

                bool worthRetrying;
                Texture2D downloaded = null;

                using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(url))
                {
                    request.timeout = requestTimeoutSeconds;

                    yield return request.SendWebRequest();

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        downloaded = DownloadHandlerTexture.GetContent(request);
                        worthRetrying = false;
                    }
                    else
                    {
                        // A 404 or a 403 is the address being wrong, and asking again will not
                        // make it right. Anything else — a timeout, no route, a status of nothing
                        // at all — is worth another go.
                        worthRetrying = request.responseCode < 400 || request.responseCode >= 500;

                        // The placeholder is already on screen and is a perfectly good answer. A
                        // player with no signal does not need to be told their own face is missing.
                        Debug.Log(
                            $"The player's picture could not be loaded " +
                            $"(attempt {attempt + 1} of {AttemptLimit}): {request.responseCode} {request.error}");
                    }
                }

                // Outside the block on purpose. Disposing the request disposes its download
                // handler, and the texture belongs to that handler until somebody else claims it —
                // which is what Adopt does, with a hide flag, on its first line.
                if (downloaded != null)
                {
                    loading = null;
                    Adopt(url, downloaded);
                    yield break;
                }

                if (!worthRetrying) break;
            }

            loading = null;
        }

        /// <summary>Turns a downloaded texture into the sprite on the button.</summary>
        void Adopt(string url, Texture2D texture)
        {
            if (texture == null) return;

            // Claimed first. See the note further down: this is what stops the scene loader — and
            // the request's own download handler — from taking it back.
            texture.hideFlags = HideFlags.HideAndDontSave;

            texture.wrapMode = TextureWrapMode.Clamp;

            if (circular) RoundOff(texture);

            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f));

            // ==========================================
            // Kept out of the way of the scene loader.
            //
            // Loading a scene runs UnloadUnusedAssets, which destroys textures and sprites nothing
            // in the new scene refers to. This cache is a static and holds them across exactly
            // that boundary — which is the whole reason it exists, and also how it ended up
            // holding a dictionary of destroyed objects after the first run: the entry was still
            // there, the picture behind it was not, and the button either drew nothing or fell
            // back to downloading the same face again on every scene change.
            //
            // HideAndDontSave is what tells the unloader these are spoken for. The texture is
            // marked as it arrives, a few lines above; the sprite is marked here.
            // ==========================================
            sprite.hideFlags = HideFlags.HideAndDontSave;

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

        /// <summary>
        /// Empties the cache when play mode starts without a domain reload.
        /// <para>
        /// The pictures are destroyed by hand rather than dropped. They are marked
        /// <see cref="HideFlags.HideAndDontSave"/> so a scene load cannot take them, which also
        /// means nothing else ever will: forgetting the dictionary would leave every face from
        /// every previous play session sitting in memory for as long as the editor is open.
        /// </para>
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStaticState()
        {
            foreach (Sprite sprite in Loaded.Values)
            {
                if (sprite == null) continue;

                if (sprite.texture != null) Destroy(sprite.texture);

                Destroy(sprite);
            }

            Loaded.Clear();
        }
    }
}
