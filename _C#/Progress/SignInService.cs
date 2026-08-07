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
        [Tooltip(
            "The app's own address on Android. THREE PLACES MUST AGREE and the server is the one " +
            "that decides:\n\n" +
            "  this field\n" +
            "  Assets/Plugins/Android/AndroidManifest.xml  (android:scheme)\n" +
            "  the Worker  (Config.js fallback.deepLinkScheme, or the TheGod panel)\n\n" +
            "The Worker builds the return address from ITS value and ignores whatever redirect_uri " +
            "we sent, so a scheme that disagrees means the browser opens an address no app on the " +
            "phone claims. Nothing errors. The player is simply left on a blank tab.")]
        [SerializeField] string deepLinkScheme = "com.amircollidergames.neonkatana";
        [SerializeField] string deepLinkHost = "oauth";
        [Tooltip("Used on desktop and in the editor, where there is no deep link to come back through.")]
        [SerializeField] string desktopRedirectUri = "http://127.0.0.1:7890/";

        [Header("Behaviour")]
        [Tooltip("Remember who was signed in between sessions.")]
        [SerializeField] bool rememberMe = true;

        [Tooltip(
            "Keep the sign-in itself between sessions, by storing the refresh token on this device.\n\n" +
            "With this off the player is sent to the browser again on every launch — and a run " +
            "finished in the game scene can never be uploaded, because the menu comes back signed " +
            "out and the outbox has nobody to send as.")]
        [SerializeField] bool keepSignedIn = true;

        [Tooltip("Keep signing-in alive through scene changes, so a run can upload without returning to the menu.")]
        [SerializeField] bool surviveSceneChanges;

        [Tooltip("On desktop, watch the clipboard while the browser is open and take the code by itself.")]
        [SerializeField] bool watchClipboard = true;
        [Tooltip("Seconds to keep watching before giving up on the browser.")]
        [SerializeField, Min(10f)] float clipboardWatchSeconds = 300f;

        const string PlayerIdKey = "NeonKatana.PlayerId";
        const string UserNameKey = "NeonKatana.UserName";
        const string RefreshTokenKey = "NeonKatana.RefreshToken";

        /// <summary>How long before the token runs out we go and get a fresh one.</summary>
        const float RenewAheadSeconds = 300f;

        string pendingState;
        string lastClipboardSeen;
        string refreshToken;
        Coroutine keepingFresh;

        bool UsesDeepLink => Application.platform == RuntimePlatform.Android;

        public bool IsSignedIn => !string.IsNullOrEmpty(PlayerId) && !string.IsNullOrEmpty(IdToken);

        /// <summary>True between opening the browser and the code coming back.</summary>
        public bool IsWaitingForBrowser { get; private set; }

        /// <summary>
        /// The Google id_token. Never written to disk: it expires anyway, and a stale one sitting
        /// on a shared device is worth more to somebody else than it is to this game.
        /// </summary>
        public string IdToken { get; private set; }

        /// <summary>
        /// The id the server keeps this player's row under. See <see cref="PlayerIdFromEmail"/> —
        /// it is derived from the address, not from the token's <c>sub</c>.
        /// </summary>
        public string PlayerId { get; private set; }

        /// <summary>
        /// The token's <c>sub</c> claim: Google's own id for this person. Kept because it is the
        /// only genuinely unique id we have, but it is <em>not</em> what the server's paths use.
        /// </summary>
        public string GoogleSubject { get; private set; }

        /// <summary>
        /// The name on the Google account. It is not a username and cannot become one: the server
        /// only accepts 3 to 12 English letters and digits, and this is whatever the person put on
        /// their account — Persian, spaces and all.
        /// </summary>
        public string GoogleName { get; private set; }

        /// <summary>
        /// What to call this player until the server has a name they chose for themselves: the
        /// Google account's name, cut to <see cref="MaxNameLength"/>. Empty when there is not one
        /// worth showing.
        /// <para>
        /// <b>Never the player id.</b> The id is the address with its domain taken off, so falling
        /// back to it — which this used to do — put a readable form of the player's email address
        /// on screen, to whoever happened to be looking at the phone, every time the account name
        /// was missing or too short. A name nobody chose is not worth a leaked address.
        /// </para>
        /// </summary>
        public string UserName { get; private set; }

        /// <summary>The address on the token. The server's player id is derived from it.</summary>
        public string Email { get; private set; }

        /// <summary>
        /// The picture on the Google account, straight off the token. Shown until the player's own
        /// record arrives, which may name a different one they chose on the site.
        /// </summary>
        public string PictureUrl { get; private set; }

        /// <summary>True when a stored session could be picked back up without a browser.</summary>
        public bool CanResume => !string.IsNullOrEmpty(refreshToken);

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
                UserName = WithoutTheId(PlayerPrefs.GetString(UserNameKey, string.Empty), PlayerId);
            }

            if (keepSignedIn) refreshToken = PlayerPrefs.GetString(RefreshTokenKey, string.Empty);

            Application.deepLinkActivated += OnDeepLinkActivated;

            // The app can be launched *by* the link rather than woken by it, in which case the
            // event has already been and gone and the address is waiting here instead.
            if (!string.IsNullOrEmpty(Application.absoluteURL)) OnDeepLinkActivated(Application.absoluteURL);
        }

        void Start()
        {
            WireUpProgressService();

            // A stored refresh token is the whole point of "keep signed in": without this the menu
            // comes back signed out after every run, and the outbox it is holding never goes out.
            if (CanResume && !IsSignedIn) StartCoroutine(RefreshRoutine(null));
        }

        /// <summary>
        /// Asked for on demand rather than handed over once, so signing in later still reaches the
        /// progress service.
        /// </summary>
        void WireUpProgressService()
        {
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

        /// <summary>
        /// Posts the code to <c>/oauth/token</c>.
        /// <para>
        /// The body is form-encoded, not JSON. <c>Api/OAuthApi.js</c> reads it with
        /// <c>new URLSearchParams(await request.text())</c> — which is what an OAuth token endpoint
        /// is specified to accept, and which finds no <c>code</c> at all in a JSON document: the
        /// whole body becomes one nameless key, the handler answers
        /// <c>400 {"error":"missing_code"}</c>, and the game reports "Signing in failed: 400".
        /// That was the entire bug; the code, the state and the redirect were all fine.
        /// </para>
        /// </summary>
        IEnumerator ExchangeCodeForToken(string code)
        {
            var body = new StringBuilder();
            AppendField(body, "code", code);
            AppendField(body, "redirect_uri", RedirectUri);
            AppendField(body, "platform", UsesDeepLink ? "android" : "web");

            using var request = new UnityWebRequest($"{BaseUrl}/oauth/token", UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body.ToString())),
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = requestTimeoutSeconds,
            };

            request.SetRequestHeader("Content-Type", "application/x-www-form-urlencoded");

            // Named rather than left to the server's fallback, which is "the first game in the
            // registry" — right today by accident, and wrong the day a second game is added ahead
            // of this one.
            request.SetRequestHeader("X-Game-ID", gameId);

            yield return request.SendWebRequest();

            IsWaitingForBrowser = false;
            pendingState = null;

            if (request.result != UnityWebRequest.Result.Success)
            {
                // The body is the useful half: the server names what it objected to, which is the
                // difference between "the request was wrong" and "Google refused the code".
                Debug.LogWarning($"Signing in failed: {request.responseCode} {request.error} {AnswerFrom(request)}", this);
                SignedInChanged?.Invoke();
                yield break;
            }

            TokenResponse tokens = ParseJson<TokenResponse>(request.downloadHandler.text);

            if (tokens == null || string.IsNullOrEmpty(tokens.id_token))
            {
                Debug.LogWarning("The server answered without an id_token.", this);
                SignedInChanged?.Invoke();
                yield break;
            }

            AcceptToken(tokens.id_token, tokens.refresh_token, tokens.expires_in);
        }

        /// <summary>Adds one <c>name=value</c> pair to a form body, escaped.</summary>
        static void AppendField(StringBuilder body, string name, string value)
        {
            if (body.Length > 0) body.Append('&');

            body.Append(name).Append('=').Append(UnityWebRequest.EscapeURL(value ?? string.Empty));
        }

        // --- Keeping the session ---

        /// <summary>
        /// Swaps the stored refresh token for a fresh id_token through <c>POST /auth/refresh</c>.
        /// This is what makes a session outlive both the hour Google gives a token and the scene
        /// change into the game.
        /// </summary>
        IEnumerator RefreshRoutine(Action<bool> onDone)
        {
            string stored = refreshToken;

            if (string.IsNullOrEmpty(stored) || string.IsNullOrWhiteSpace(BaseUrl))
            {
                onDone?.Invoke(false);
                yield break;
            }

            string body = JsonUtility.ToJson(new RefreshRequest { refreshToken = stored });

            using var request = new UnityWebRequest($"{BaseUrl}/auth/refresh", UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body)),
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = requestTimeoutSeconds,
            };

            // This one really is JSON: /auth/refresh reads request.json(), unlike /oauth/token.
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("X-Game-ID", gameId);

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                // A refusal means the stored token is spent and the player has to sign in again.
                // A network failure means nothing of the sort, so the token is kept and the next
                // launch tries once more.
                if (request.responseCode >= 400 && request.responseCode < 500)
                {
                    Debug.Log($"The stored sign-in is no longer good, so the player is signed out: {AnswerFrom(request)}");
                    ForgetStoredSession();
                }

                onDone?.Invoke(false);
                yield break;
            }

            RefreshResponse answer = ParseJson<RefreshResponse>(request.downloadHandler.text);

            if (answer == null || !answer.success || string.IsNullOrEmpty(answer.id_token))
            {
                ForgetStoredSession();
                onDone?.Invoke(false);
                yield break;
            }

            AcceptToken(answer.id_token, answer.refresh_token, answer.expires_in);
            onDone?.Invoke(true);
        }

        /// <summary>
        /// Goes back for a new token shortly before the current one runs out. An id_token lasts
        /// about an hour, which is longer than a run and shorter than an evening — so without this
        /// a long session quietly stops being able to upload anything.
        /// </summary>
        IEnumerator KeepTokenFresh(int expiresInSeconds)
        {
            float lifetime = expiresInSeconds > 0 ? expiresInSeconds : 3600f;

            // Never less than a minute apart, so a server answering with a nonsense lifetime
            // cannot turn this into a request every frame.
            yield return new WaitForSecondsRealtime(Mathf.Max(60f, lifetime - RenewAheadSeconds));

            keepingFresh = null;

            yield return RefreshRoutine(null);
        }

        // --- Holding on to it ---

        /// <summary>
        /// Stores the token and reads who it belongs to out of it.
        /// </summary>
        void AcceptToken(string idToken, string newRefreshToken, int expiresInSeconds)
        {
            GoogleClaims claims = ReadTokenPayload<GoogleClaims>(idToken);

            if (claims == null || string.IsNullOrEmpty(claims.email))
            {
                Debug.LogWarning("The id_token had no address in it, so there is no player id to use.", this);
                SignedInChanged?.Invoke();
                return;
            }

            IdToken = idToken;
            GoogleSubject = claims.sub;
            Email = claims.email;
            PlayerId = PlayerIdFromEmail(claims.email);
            GoogleName = claims.name;
            PictureUrl = claims.picture;
            // No "?? PlayerId". The id is the address without its domain, and there is no state
            // of this game in which showing somebody their own email address is the right answer
            // to not knowing their name.
            UserName = ShortenToNameLength(claims.name) ?? string.Empty;

            if (!string.IsNullOrEmpty(newRefreshToken)) refreshToken = newRefreshToken;

            if (rememberMe)
            {
                PlayerPrefs.SetString(PlayerIdKey, PlayerId);
                PlayerPrefs.SetString(UserNameKey, UserName ?? string.Empty);
            }

            if (keepSignedIn && !string.IsNullOrEmpty(refreshToken))
                PlayerPrefs.SetString(RefreshTokenKey, refreshToken);

            if (rememberMe || keepSignedIn) PlayerPrefs.Save();

            if (keepingFresh != null) StopCoroutine(keepingFresh);
            keepingFresh = StartCoroutine(KeepTokenFresh(expiresInSeconds));

            // A service that started after this one — the game scene's, for instance — has not had
            // the providers set on it yet.
            WireUpProgressService();

            // Anything that could not be sent while signed out goes out now.
            if (ProgressService.Instance != null) ProgressService.Instance.OnSignedIn();

            SignedInChanged?.Invoke();
        }

        /// <summary>
        /// The id the server keeps this player's row under.
        /// <para>
        /// <c>Core/PlayerIdentity.js</c> derives it from the address on the verified token —
        /// <c>email.split('@')[0].toLowerCase().substring(0, 15)</c> — and
        /// <c>Api/PlayerDataApi.js</c> refuses any path naming a different one. The token's
        /// <c>sub</c> claim looks like the right thing to use and is not: every profile read and
        /// every score upload came back <c>403 forbidden</c> against an id the server had never
        /// heard of. The rule is duplicated here on purpose — it is a wire format, and both halves
        /// have to agree on it exactly.
        /// </para>
        /// </summary>
        public static string PlayerIdFromEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return string.Empty;

            string localPart = email.Trim().Split('@')[0].ToLowerInvariant();

            return localPart.Length <= 15 ? localPart : localPart.Substring(0, 15);
        }

        /// <summary>
        /// A remembered name, unless it is really the player id wearing a name's clothes.
        /// <para>
        /// Builds before this one saved <c>UserName</c> with the id as its fallback, so the id is
        /// already sitting in <c>PlayerPrefs</c> on every device that has signed in. It is read in
        /// <c>Awake</c>, before any token, which is precisely why the address flashed up on the
        /// menu during loading — fixing where the value comes from does nothing for the copies
        /// already saved.
        /// </para>
        /// </summary>
        static string WithoutTheId(string remembered, string playerId)
        {
            if (string.IsNullOrWhiteSpace(remembered)) return string.Empty;
            if (string.IsNullOrWhiteSpace(playerId)) return remembered;

            return string.Equals(remembered.Trim(), playerId.Trim(), StringComparison.OrdinalIgnoreCase)
                ? string.Empty
                : remembered;
        }

        /// <summary>The shortest a name may be before it stops being one.</summary>
        public const int MinNameLength = 3;

        /// <summary>The longest a name may be. The server's own limit for a chosen username.</summary>
        public const int MaxNameLength = 12;

        /// <summary>
        /// Cuts a name down to the length a name is allowed to be, or returns null when what is
        /// left would be too short to show. Used on the Google account's name, which is what the
        /// player is called until they choose something on the site.
        /// </summary>
        public static string ShortenToNameLength(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;

            string trimmed = name.Trim();

            if (trimmed.Length > MaxNameLength)
            {
                int cut = MaxNameLength;

                // Never between the two halves of one character. A name ending in an emoji would
                // otherwise be cut into a lone surrogate, which draws as an empty box.
                if (char.IsHighSurrogate(trimmed[cut - 1])) cut--;

                trimmed = trimmed.Substring(0, cut).TrimEnd();
            }

            return trimmed.Length >= MinNameLength ? trimmed : null;
        }

        public void SignOut()
        {
            IdToken = null;
            PlayerId = null;
            GoogleSubject = null;
            GoogleName = null;
            PictureUrl = null;
            UserName = null;
            Email = null;
            pendingState = null;
            IsWaitingForBrowser = false;

            // The record on screen belonged to whoever has just left.
            if (ProgressService.Instance != null) ProgressService.Instance.OnSignedOut();

            if (keepingFresh != null)
            {
                StopCoroutine(keepingFresh);
                keepingFresh = null;
            }

            ForgetStoredSession();

            SignedInChanged?.Invoke();
        }

        void ForgetStoredSession()
        {
            refreshToken = null;

            PlayerPrefs.DeleteKey(PlayerIdKey);
            PlayerPrefs.DeleteKey(UserNameKey);
            PlayerPrefs.DeleteKey(RefreshTokenKey);
            PlayerPrefs.Save();
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

        static T ParseJson<T>(string json) where T : class
        {
            if (string.IsNullOrWhiteSpace(json)) return null;

            try
            {
                return JsonUtility.FromJson<T>(json);
            }
            catch (Exception failure)
            {
                Debug.LogWarning($"The server's answer could not be read: {failure.Message}");
                return null;
            }
        }

        /// <summary>What the server said, short enough to belong in a log line.</summary>
        static string AnswerFrom(UnityWebRequest request)
        {
            string body = request.downloadHandler != null ? request.downloadHandler.text : null;

            if (string.IsNullOrWhiteSpace(body)) return string.Empty;

            return body.Length <= 300 ? body : body.Substring(0, 300);
        }

        [Serializable]
        class TokenResponse
        {
            public string id_token;
            public string refresh_token;
            public int expires_in;
        }

        [Serializable]
        class RefreshRequest
        {
            public string refreshToken;
        }

        [Serializable]
        class RefreshResponse
        {
            public bool success;
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
