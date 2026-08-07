using System;
using System.Collections;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace NeonKatana
{
    /// <summary>
    /// Talks to the amircollider.com player-data API.
    /// <para>
    /// Routes, taken from <c>Api/PlayerDataApi.js</c>:
    /// <list type="bullet">
    /// <item><c>GET  /database/get/games/{game}/leaderboard</c> — public</item>
    /// <item><c>GET  /database/get/games/{game}/users/{player}</c> — owner only</item>
    /// <item><c>POST /database/set/games/{game}/users/{player}/highScore</c> — body is the bare number</item>
    /// <item><c>POST /database/patch/games/{game}/users/{player}</c> — body is a partial profile</item>
    /// </list>
    /// Ownership comes from the token, never from the path, so the player id in the URL has to be
    /// the one the signed-in token resolves to — which the server derives from the address on the
    /// token, not from its <c>sub</c> claim. See <see cref="SignInService.PlayerIdFromEmail"/>.
    /// </para>
    /// </summary>
    public class AmirColliderBackend : IProgressBackend
    {
        readonly MonoBehaviour host;
        readonly string baseUrl;
        readonly string gameId;
        readonly Func<string> idTokenProvider;
        readonly Func<string> playerIdProvider;
        readonly int timeoutSeconds;

        /// <param name="host">Whatever runs the coroutines; the service passes itself.</param>
        /// <param name="idTokenProvider">
        /// Returns the current Google id_token, or null when nobody is signed in. Sign-in is not
        /// part of this class on purpose — plug in whichever flow the app ends up using.
        /// </param>
        public AmirColliderBackend(
            MonoBehaviour host,
            string baseUrl,
            string gameId,
            Func<string> idTokenProvider,
            Func<string> playerIdProvider,
            int timeoutSeconds = 10)
        {
            this.host = host;
            this.baseUrl = baseUrl != null ? baseUrl.TrimEnd('/') : string.Empty;
            this.gameId = gameId;
            this.idTokenProvider = idTokenProvider;
            this.playerIdProvider = playerIdProvider;
            this.timeoutSeconds = timeoutSeconds;
        }

        /// <summary>
        /// Whether it is worth trying. Deliberately not "whether it will work".
        /// <para>
        /// <see cref="Application.internetReachability"/> used to be part of this and is not any
        /// more. It reports what kind of connection the device <em>has</em>, not whether anything
        /// is reachable over it, and it answers <c>NotReachable</c> on plenty of machines that are
        /// perfectly online — editors on Linux, desktops behind a VPN, anything where Unity cannot
        /// read the interface. On those, this returned false forever: no profile was ever read, no
        /// score was ever sent, and nothing said why, because "offline" is not an error anybody
        /// logs. A request that has nowhere to go fails in a second and says so, which is a better
        /// answer than never asking.
        /// </para>
        /// </summary>
        public bool IsAvailable =>
            !string.IsNullOrEmpty(baseUrl) &&
            !string.IsNullOrEmpty(gameId) &&
            !string.IsNullOrEmpty(IdToken) &&
            !string.IsNullOrEmpty(PlayerId);

        string IdToken => idTokenProvider != null ? idTokenProvider() : null;

        string PlayerId => playerIdProvider != null ? playerIdProvider() : null;

        /// <summary>
        /// Whether the object running the coroutines is still there to run them. Requests start
        /// from scene teardown now — a run left mid-way is still a run — and
        /// <see cref="MonoBehaviour.StartCoroutine"/> on a component being destroyed throws.
        /// </summary>
        bool HostCanRun => host != null && host.isActiveAndEnabled;

        public void LoadProfile(Action<PlayerProfile, string> onDone)
        {
            if (!IsAvailable || !HostCanRun)
            {
                onDone?.Invoke(null, "Nobody is signed in, or the server cannot be reached.");
                return;
            }

            host.StartCoroutine(Send(
                UnityWebRequest.kHttpVerbGET,
                $"{baseUrl}/database/get/games/{gameId}/users/{PlayerId}",
                null,
                (body, error) => onDone?.Invoke(error == null ? FromJson<PlayerProfile>(body) : null, error)));
        }

        /// <summary>
        /// Sends the run in two steps, because the API keeps the score and the counters apart. The
        /// score goes first: it is the one the player would miss.
        /// </summary>
        public void SubmitRun(RunResult run, PlayerProfile totals, Action<bool, string> onDone)
        {
            if (!IsAvailable || !HostCanRun)
            {
                onDone?.Invoke(false, "Nobody is signed in, or the server cannot be reached.");
                return;
            }

            host.StartCoroutine(SubmitRunRoutine(run, totals, onDone));
        }

        IEnumerator SubmitRunRoutine(RunResult run, PlayerProfile totals, Action<bool, string> onDone)
        {
            string player = PlayerId;
            string scoreFailure = null;
            string counterFailure = null;

            // The endpoint takes the number on its own, not an object around it. Written with the
            // invariant culture because this is a wire format: the server reads it with
            // parseInt(body, 10), which has no opinion about the player's locale.
            yield return Send(
                UnityWebRequest.kHttpVerbPOST,
                $"{baseUrl}/database/set/games/{gameId}/users/{player}/highScore",
                run.score.ToString(CultureInfo.InvariantCulture),
                (_, error) => scoreFailure = error);

            // ==========================================
            // The counters go either way.
            //
            // This used to give up here when the score write failed, and the two are not the same
            // fact: how many runs a player has finished and how long they have played are true
            // whether or not this particular score beat their best or reached the server at all.
            // While a new player's row was silently not being created, the score write answered
            // 404 every time — so this line was never reached, and games_played and total_play_time
            // stayed at zero on the server for good. That is exactly the "the score updates later
            // but the run count and play time never do" this is being fixed for.
            //
            // Nothing is lost by trying both: the run is only taken out of the outbox when both
            // succeeded, and the server takes the larger of the two totals, so a repeat is
            // harmless.
            // ==========================================
            string counters = JsonUtility.ToJson(new ProfileCounters
            {
                gamesPlayed = totals.gamesPlayed,
                totalPlayTime = totals.totalPlayTime,
            });

            yield return Send(
                UnityWebRequest.kHttpVerbPOST,
                $"{baseUrl}/database/patch/games/{gameId}/users/{player}",
                counters,
                (_, error) => counterFailure = error);

            string failure = scoreFailure ?? counterFailure;

            onDone?.Invoke(failure == null, failure);
        }

        public void LoadLeaderboard(Action<LeaderboardEntry[], string> onDone)
        {
            if (string.IsNullOrEmpty(baseUrl) || string.IsNullOrEmpty(gameId) || !HostCanRun)
            {
                onDone?.Invoke(Array.Empty<LeaderboardEntry>(), "No server address is set.");
                return;
            }

            host.StartCoroutine(Send(
                UnityWebRequest.kHttpVerbGET,
                $"{baseUrl}/database/get/games/{gameId}/leaderboard",
                null,
                (body, error) =>
                {
                    if (error != null)
                    {
                        onDone?.Invoke(Array.Empty<LeaderboardEntry>(), error);
                        return;
                    }

                    // The endpoint answers with a bare array, which JsonUtility will not read.
                    LeaderboardPage page = FromJson<LeaderboardPage>($"{{\"entries\":{body}}}");
                    onDone?.Invoke(page != null ? page.entries : Array.Empty<LeaderboardEntry>(), null);
                }));
        }

        IEnumerator Send(string method, string url, string body, Action<string, string> onDone)
        {
            using var request = new UnityWebRequest(url, method)
            {
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = timeoutSeconds,
            };

            if (body != null)
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                request.SetRequestHeader("Content-Type", "application/json");
            }

            string token = IdToken;
            if (!string.IsNullOrEmpty(token)) request.SetRequestHeader("Authorization", $"Bearer {token}");

            // ==========================================
            // Which game's database this is about.
            //
            // Every request here already names the game in its path, and the Worker does not read
            // it from there: Worker.js takes the game from this header, then `?game=`, and failing
            // both from `Object.keys(GAMES)[0]` — the first game in the registry. Neon Katana is
            // that game today, so every profile read and every score write has been landing in the
            // right database by luck rather than by being addressed to it.
            //
            // SignInService has sent this header on /oauth/token and /auth/refresh for a while,
            // with a comment saying exactly this. The data API — the half that carries the scores —
            // never did.
            // ==========================================
            if (!string.IsNullOrEmpty(gameId)) request.SetRequestHeader("X-Game-ID", gameId);

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                // The body is carried into the message on purpose. This API names what it
                // objected to — "forbidden", "user_not_found", "invalid_token" — and each of
                // those is a different thing to go and fix; the status code on its own is not.
                onDone?.Invoke(null, $"{method} {url} failed: {request.responseCode} {request.error} {AnswerFrom(request)}");
                yield break;
            }

            onDone?.Invoke(request.downloadHandler.text, null);
        }

        /// <summary>What the server said, short enough to belong in a log line.</summary>
        static string AnswerFrom(UnityWebRequest request)
        {
            string body = request.downloadHandler != null ? request.downloadHandler.text : null;

            if (string.IsNullOrWhiteSpace(body)) return string.Empty;

            return body.Length <= 300 ? body : body.Substring(0, 300);
        }

        static T FromJson<T>(string json) where T : class
        {
            if (string.IsNullOrWhiteSpace(json)) return null;

            try
            {
                return JsonUtility.FromJson<T>(json);
            }
            catch (Exception failure)
            {
                Debug.LogWarning($"The server sent something that is not a {typeof(T).Name}: {failure.Message}");
                return null;
            }
        }

        /// <summary>
        /// Only the counters, so the patch never carries a field the server's whitelist would
        /// reject — and never overwrites something like the chosen colour with a stale value.
        /// </summary>
        [Serializable]
        class ProfileCounters
        {
            public int gamesPlayed;
            public int totalPlayTime;
        }
    }
}
