using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace NeonKatana
{
    /// <summary>
    /// Signs the player in through amircollider.com and holds the token the rest of the game needs.
    /// <para>
    /// The flow, matching <c>Api/OAuthApi.js</c>:
    /// <list type="number">
    /// <item>open <c>/oauth/auth?redirect_uri=…&amp;state=…</c> in the player's browser</item>
    /// <item>Google returns to <c>/oauth/callback</c>, which hands the code back —
    ///       on Android through the app's own deep link, elsewhere as text on a page</item>
    /// <item>the app posts that code to <c>/oauth/token</c> and gets an id_token back</item>
    /// </list>
    /// On a desktop there is no deep link to catch, so the page's copy button is the way home:
    /// the player copies the code and presses the paste button in the game.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public class SignInService : MonoBehaviour
    {
        public static SignInService Instance { get; private set; }

        [Header("Server")]
        [SerializeField] string serverBaseUrl = "https://amircollider.com";
        [SerializeField] string gameId = "neon-katana";
        [SerializeField, Min(1)] int requestTimeoutSeconds = 15;

        [Header("Coming back from the browser")]
        [Tooltip("The app's own address on Android. Must match the deep link registered for this game.")]
        [SerializeField] string deepLinkScheme = "neonkatana";
        [SerializeField] string deepLinkHost = "auth";
        [Tooltip("Used on desktop and in the editor, where there is no deep link to come back through.")]
        [SerializeField] string desktopRedirectUri = "http://127.0.0.1:7890/";

        [Header("Behaviour")]
        [Tooltip("Remember who was signed in between sessions. The token itself is never stored.")]
        [SerializeField] bool rememberMe = true;
        [Tooltip("Keep signing-in alive through scene changes, so a run can upload without returning to the menu.")]
        [SerializeField] bool surviveSceneChanges;

        [Tooltip("On desktop, watch the clipboard while the browser is open and take the code by itself.")]
        [SerializeField] bool watchClipboard = true;
        [Tooltip("Seconds to keep watching before giving up on the browser.")]
        [SerializeField, Min(10f)] float clipboardWatchSeconds = 300f;

        const string PlayerIdKey = "NeonKatana.PlayerId";
        const string UserNameKey = "NeonKatana.UserName";

        string pendingState;
        string lastClipboardSeen;

        bool UsesDeepLink => Application.platform == RuntimePlatform.Android;

        public bool IsSignedIn => !string.IsNullOrEmpty(PlayerId) && !string.IsNullOrEmpty(IdToken);

        /// <summary>True between opening the browser and the code coming back.</summary>
        public bool IsWaitingForBrowser { get; private set; }

        /// <summary>
        /// The Google id_token. Never written to disk: it expires anyway, and a stale one sitting
        /// on a shared device is worth more to somebody else than it is to this game.
        /// </summary>
        public string IdToken { get; private set; }

        public string PlayerId { get; private set; }
        public string UserName { get; private set; }

        /// <summary>Raised whenever somebody signs in or out, and when an attempt fails.</summary>
        public event Action SignedInChanged;

        string RedirectUri => UsesDeepLink ? $"{deepLinkScheme}://{deepLinkHost}" : desktopRedirectUri;

        string BaseUrl => serverBaseUrl != null ? serverBaseUrl.TrimEnd('/') : string.Empty;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"A second {nameof(SignInService)} is in the scene. Keeping the first one.", this);
                enabled = false;
                return;
            }

            Instance = this;

            if (surviveSceneChanges) DontDestroyOnLoad(gameObject);

            if (rememberMe)
            {
                PlayerId = PlayerPrefs.GetString(PlayerIdKey, string.Empty);
                UserName = PlayerPrefs.GetString(UserNameKey, string.Empty);
            }

            Application.deepLinkActivated += OnDeepLinkActivated;

            // The app can be launched *by* the link rather than woken by it, in which case the
            // event has already been and gone and the address is waiting here instead.
            if (!string.IsNullOrEmpty(Application.absoluteURL)) OnDeepLinkActivated(Application.absoluteURL);
        }

        void Start()
        {
            // Asked for on demand rather than handed over once, so signing in later still reaches
            // the progress service.
            if (ProgressService.Instance == null) return;

            ProgressService.Instance.IdTokenProvider = () => IdToken;
            ProgressService.Instance.PlayerIdProvider = () => PlayerId;
        }

        void OnDestroy()
        {
            Application.deepLinkActivated -= OnDeepLinkActivated;

            if (Instance == this) Instance = null;
        }

        // --- Going out ---

        /// <summary>Opens the sign-in page in the player's browser.</summary>
        public void BeginSignIn()
        {
            if (string.IsNullOrWhiteSpace(BaseUrl))
            {
                Debug.LogWarning($"{nameof(SignInService)} has no server address, so the button does nothing.", this);
                return;
            }

            // The state is checked when the code comes back, so a link arriving out of the blue
            // cannot sign anybody in.
            pendingState = Guid.NewGuid().ToString("N");
            IsWaitingForBrowser = true;
            lastClipboardSeen = SafeClipboard();

            var url = new StringBuilder($"{BaseUrl}/oauth/auth");
            url.Append("?redirect_uri=").Append(UnityWebRequest.EscapeURL(RedirectUri));
            url.Append("&state=").Append(UnityWebRequest.EscapeURL(pendingState));
            url.Append("&game=").Append(UnityWebRequest.EscapeURL(gameId));

            // The sign-in page is a web page and picks its own language unless told. Sending the
            // game's language keeps the two halves of signing in speaking the same one.
            url.Append("&lang=").Append(UnityWebRequest.EscapeURL(CurrentLanguageTag()));

            if (UsesDeepLink) url.Append("&platform=android");

            Application.OpenURL(url.ToString());

            // No deep link on a desktop, so the page's copy button is the way back. Watching the
            // clipboard turns that into something the player does not have to be told about.
            if (watchClipboard && !UsesDeepLink) StartCoroutine(WatchClipboardForCode());
        }

        static string CurrentLanguageTag()
        {
            GameLanguage language = LocalizationService.Instance != null
                ? LocalizationService.Instance.CurrentLanguage
                : PlayerProgress.GetLanguage(GameLanguage.English);

            return PlayerProgress.LanguageTag(language);
        }

        /// <summary>
        /// Looks at the clipboard now and then while the browser is open, and takes the code the
        /// moment it appears. Only what was copied *after* sign-in started counts.
        /// </summary>
        IEnumerator WatchClipboardForCode()
        {
            float giveUpAt = Time.realtimeSinceStartup + clipboardWatchSeconds;

            while (IsWaitingForBrowser && Time.realtimeSinceStartup < giveUpAt)
            {
                yield return new WaitForSecondsRealtime(0.5f);

                string clipboard = SafeClipboard();
                if (clipboard == lastClipboardSeen) continue;

                lastClipboardSeen = clipboard;

                string code = ReadCodeFrom(clipboard);
                if (code != null) SubmitCode(code);
            }
        }

        /// <summary>
        /// Pulls an authorisation code out of whatever was copied — the code on its own, or the
        /// whole callback address. Returns null when it is something else entirely, so an unrelated
        /// copy while the browser is open is left alone.
        /// </summary>
        static string ReadCodeFrom(string clipboard)
        {
            if (string.IsNullOrWhiteSpace(clipboard)) return null;

            string trimmed = clipboard.Trim();

            if (trimmed.Contains("code=")) return ReadQueryValue(trimmed, "code");

            bool looksLikeACode = trimmed.Length >= 16 && trimmed.IndexOf(' ') < 0 && trimmed.IndexOf('\n') < 0;

            return looksLikeACode ? trimmed : null;
        }

        static string SafeClipboard()
        {
            try
            {
                return GUIUtility.systemCopyBuffer ?? string.Empty;
            }
            catch (Exception)
            {
                // Some platforms refuse the clipboard outright; that is not worth a warning a
                // frame at a time.
                return string.Empty;
            }
        }

        // --- Coming back ---

        /// <summary>Android wakes the app with <c>scheme://host?code=…</c> once the browser is done.</summary>
        void OnDeepLinkActivated(string link)
        {
            string code = ReadQueryValue(link, "code");

            if (string.IsNullOrEmpty(code))
            {
                string failure = ReadQueryValue(link, "error");
                if (!string.IsNullOrEmpty(failure)) Debug.LogWarning($"Sign-in came back with an error: {failure}", this);
                return;
            }

            if (!StateBelongsToThisAttempt(ReadQueryValue(link, "state")))
            {
                Debug.LogWarning("Sign-in came back with the wrong state and was ignored.", this);
                return;
            }

            SubmitCode(code);
        }

        /// <summary>
        /// Checks the state that came back against the one we sent.
        /// <para>
        /// The server does not hand our state back as-is: it signs its own state around ours and
        /// keeps ours inside as <c>originalState</c>. Comparing the two strings directly would
        /// reject every real sign-in, so ours is unwrapped first.
        /// </para>
        /// </summary>
        bool StateBelongsToThisAttempt(string returnedState)
        {
            if (string.IsNullOrEmpty(pendingState) || string.IsNullOrEmpty(returnedState)) return true;
            if (returnedState == pendingState) return true;

            ServerState wrapped = ReadTokenPayload<ServerState>(returnedState);

            return wrapped != null && wrapped.originalState == pendingState;
        }

        /// <summary>
        /// Takes the code the desktop page copied to the clipboard. There is no deep link on a
        /// desktop, so this is how the code gets home while testing.
        /// </summary>
        public void PasteCodeFromClipboard()
        {
            string code = ReadCodeFrom(SafeClipboard());

            if (code == null)
            {
                Debug.LogWarning("There is no sign-in code on the clipboard. Copy it from the page first.", this);
                return;
            }

            SubmitCode(code);
        }

        /// <summary>Trades an authorisation code for a token. Public so any flow can finish here.</summary>
        public void SubmitCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                Debug.LogWarning("Sign-in was given an empty code.", this);
                return;
            }

            StartCoroutine(ExchangeCodeForToken(code.Trim()));
        }

        IEnumerator ExchangeCodeForToken(string code)
        {
            string body = JsonUtility.ToJson(new TokenRequest
            {
                code = code,
                redirect_uri = RedirectUri,
                platform = UsesDeepLink ? "android" : "web",
            });

            using var request = new UnityWebRequest($"{BaseUrl}/oauth/token", UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body)),
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = requestTimeoutSeconds,
            };

            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            IsWaitingForBrowser = false;
            pendingState = null;

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"Signing in failed: {request.responseCode} {request.error}", this);
                SignedInChanged?.Invoke();
                yield break;
            }

            TokenResponse tokens = ParseTokens(request.downloadHandler.text);

            if (tokens == null || string.IsNullOrEmpty(tokens.id_token))
            {
                Debug.LogWarning("The server answered without an id_token.", this);
                SignedInChanged?.Invoke();
                yield break;
            }

            AcceptToken(tokens.id_token);
        }

        /// <summary>
        /// Stores the token and reads who it belongs to out of it. The id is taken from the token's
        /// own <c>sub</c> claim rather than from anything the app chose, which is the same thing
        /// the server checks ownership against.
        /// </summary>
        void AcceptToken(string idToken)
        {
            GoogleClaims claims = ReadTokenPayload<GoogleClaims>(idToken);

            if (claims == null || string.IsNullOrEmpty(claims.sub))
            {
                Debug.LogWarning("The id_token had no player id in it.", this);
                SignedInChanged?.Invoke();
                return;
            }

            IdToken = idToken;
            PlayerId = claims.sub;
            UserName = !string.IsNullOrWhiteSpace(claims.name) ? claims.name : claims.email;

            if (rememberMe)
            {
                PlayerPrefs.SetString(PlayerIdKey, PlayerId);
                PlayerPrefs.SetString(UserNameKey, UserName ?? string.Empty);
                PlayerPrefs.Save();
            }

            // Anything that could not be sent while signed out goes out now.
            if (ProgressService.Instance != null) ProgressService.Instance.OnSignedIn();

            SignedInChanged?.Invoke();
        }

        public void SignOut()
        {
            IdToken = null;
            PlayerId = null;
            UserName = null;
            pendingState = null;
            IsWaitingForBrowser = false;

            PlayerPrefs.DeleteKey(PlayerIdKey);
            PlayerPrefs.DeleteKey(UserNameKey);
            PlayerPrefs.Save();

            SignedInChanged?.Invoke();
        }

        // --- Reading things apart ---

        /// <summary>Pulls one value out of a query string, whatever kind of address it belongs to.</summary>
        static string ReadQueryValue(string url, string key)
        {
            if (string.IsNullOrEmpty(url)) return null;

            int queryStart = url.IndexOf('?');
            string query = queryStart >= 0 ? url.Substring(queryStart + 1) : url;

            foreach (string pair in query.Split('&'))
            {
                int split = pair.IndexOf('=');
                if (split <= 0) continue;

                if (pair.Substring(0, split) == key) return UnityWebRequest.UnEscapeURL(pair.Substring(split + 1));
            }

            return null;
        }

        /// <summary>
        /// Reads the middle section of a signed token — the id_token from Google, or the state the
        /// server signs around ours. The signature is not checked here on purpose: the server
        /// checks it on every request, and a token this app forged for itself would buy nothing.
        /// </summary>
        static T ReadTokenPayload<T>(string token) where T : class
        {
            if (string.IsNullOrEmpty(token)) return null;

            string[] parts = token.Split('.');
            if (parts.Length < 2) return null;

            try
            {
                string payload = parts[1].Replace('-', '+').Replace('_', '/');
                payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');

                return JsonUtility.FromJson<T>(Encoding.UTF8.GetString(Convert.FromBase64String(payload)));
            }
            catch (Exception failure)
            {
                Debug.LogWarning($"A signed token could not be read: {failure.Message}");
                return null;
            }
        }

        static TokenResponse ParseTokens(string json)
        {
            try
            {
                return JsonUtility.FromJson<TokenResponse>(json);
            }
            catch (Exception failure)
            {
                Debug.LogWarning($"The token answer could not be read: {failure.Message}");
                return null;
            }
        }

        [Serializable]
        class TokenRequest
        {
            public string code;
            public string redirect_uri;
            public string platform;
        }

        [Serializable]
        class TokenResponse
        {
            public string id_token;
            public string refresh_token;
            public int expires_in;
        }

        [Serializable]
        class GoogleClaims
        {
            public string sub;
            public string name;
            public string email;
            public string picture;
        }

        /// <summary>The state the server signs around ours. Only <c>originalState</c> is ours.</summary>
        [Serializable]
        class ServerState
        {
            public string originalState;
            public string originalRedirectUri;
            public string language;
            public string gameId;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStaticState() => Instance = null;
    }
}
