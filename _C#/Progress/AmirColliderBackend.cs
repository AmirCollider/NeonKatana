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

        public bool IsAvailable =>
            !string.IsNullOrEmpty(baseUrl) &&
            !string.IsNullOrEmpty(gameId) &&
            !string.IsNullOrEmpty(IdToken) &&
            !string.IsNullOrEmpty(PlayerId) &&
            Application.internetReachability != NetworkReachability.NotReachable;

        string IdToken => idTokenProvider != null ? idTokenProvider() : null;

        string PlayerId => playerIdProvider != null ? playerIdProvider() : null;

        public void LoadProfile(Action<PlayerProfile, string> onDone)
        {
            if (!IsAvailable)
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
            if (!IsAvailable)
            {
                onDone?.Invoke(false, "Nobody is signed in, or the server cannot be reached.");
                return;
            }

            host.StartCoroutine(SubmitRunRoutine(run, totals, onDone));
        }

        IEnumerator SubmitRunRoutine(RunResult run, PlayerProfile totals, Action<bool, string> onDone)
        {
            string player = PlayerId;
            string failure = null;

            // The endpoint takes the number on its own, not an object around it. Written with the
            // invariant culture because this is a wire format: the server reads it with
            // parseInt(body, 10), which has no opinion about the player's locale.
            yield return Send(
                UnityWebRequest.kHttpVerbPOST,
                $"{baseUrl}/database/set/games/{gameId}/users/{player}/highScore",
                run.score.ToString(CultureInfo.InvariantCulture),
                (_, error) => failure = error);

            if (failure != null)
            {
                onDone?.Invoke(false, failure);
                yield break;
            }

            string counters = JsonUtility.ToJson(new ProfileCounters
            {
                gamesPlayed = totals.gamesPlayed,
                totalPlayTime = totals.totalPlayTime,
            });

            yield return Send(
                UnityWebRequest.kHttpVerbPOST,
                $"{baseUrl}/database/patch/games/{gameId}/users/{player}",
                counters,
                (_, error) => failure = error);

            onDone?.Invoke(failure == null, failure);
        }

        public void LoadLeaderboard(Action<LeaderboardEntry[], string> onDone)
        {
            if (string.IsNullOrEmpty(baseUrl) || string.IsNullOrEmpty(gameId))
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
