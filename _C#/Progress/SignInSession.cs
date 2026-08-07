using UnityEngine;

namespace NeonKatana
{
    /// <summary>
    /// The signed-in player, held somewhere a scene change cannot reach.
    /// <para>
    /// <see cref="SignInService"/> lives on the menu's <c>MenuServices</c> object and is destroyed
    /// with it the moment the game scene loads. Everything it knew went with it — including the
    /// id_token, which is deliberately never written to disk — so coming back to the menu meant a
    /// fresh component with nothing in it, and a menu that drew itself signed out while a
    /// <c>/auth/refresh</c> round trip to Google decided whether the player was still welcome.
    /// That round trip is why the menu came back saying "sign in to save your score" after every
    /// run, and why the avatar had nothing to load: one slow or refused request and the player was
    /// signed out until they restarted the app.
    /// </para>
    /// <para>
    /// A static holds its value across <see cref="UnityEngine.SceneManagement.SceneManager.LoadScene"/>
    /// and is emptied when play mode starts, which is exactly the lifetime a session wants: the
    /// token stays in memory, never touches the disk, and survives every scene the player walks
    /// through. Refreshing is then something that happens quietly before the token expires rather
    /// than something the menu has to wait for.
    /// </para>
    /// <para>
    /// It is also how the <b>game</b> scene reaches the player at all. There is no
    /// <see cref="SignInService"/> in <c>MainGame</c>, so the <see cref="ProgressService"/> sitting
    /// on <c>LevelManager</c> had no token and no player id, and every finished run went into the
    /// outbox instead of to the server — waiting for a menu that had itself come back signed out.
    /// </para>
    /// </summary>
    public static class SignInSession
    {
        /// <summary>
        /// The Google id_token as it was issued. Memory only — never written to disk, on purpose.
        /// <para>
        /// This is the raw value and may already have expired; <see cref="UsableIdToken"/> is what
        /// callers should send.
        /// </para>
        /// </summary>
        public static string IdToken { get; private set; }

        /// <summary>
        /// The token to actually put on a request, or null once it has expired.
        /// <para>
        /// The difference matters most in the game scene, which has no <see cref="SignInService"/>
        /// in it and therefore nothing renewing anything. A token that ran out during a long run
        /// was still handed to the progress service, which had no way of telling and sent it — one
        /// guaranteed round trip to a <c>401</c> at the exact moment a run was trying to save.
        /// Nothing was lost, because the run stays in the outbox and goes out from the menu once
        /// the session is renewed, but the request was never worth making.
        /// </para>
        /// </summary>
        public static string UsableIdToken =>
            !string.IsNullOrEmpty(PlayerId) &&
            !string.IsNullOrEmpty(IdToken) &&
            Time.realtimeSinceStartup < ExpiresAt - SendMarginSeconds
                ? IdToken
                : null;

        /// <summary>
        /// How much of a token's remaining life is not worth spending. A request that leaves with
        /// a few seconds on the clock can still arrive expired, and the server is the one deciding.
        /// Kept off <see cref="IsSignedIn"/>, which answers a question about the player rather than
        /// about a request, and should not report somebody signed out a minute early.
        /// </summary>
        const float SendMarginSeconds = 30f;

        /// <summary>
        /// <see cref="Time.realtimeSinceStartup"/> at which <see cref="IdToken"/> stops being
        /// worth sending. Realtime rather than the wall clock: a device whose clock the player
        /// moves should not decide a valid token is dead, or a dead one alive.
        /// </summary>
        public static float ExpiresAt { get; private set; }

        /// <summary>The id the server keeps this player's row under.</summary>
        public static string PlayerId { get; private set; }

        public static string Email { get; private set; }

        /// <summary>Google's own id for this person.</summary>
        public static string GoogleSubject { get; private set; }

        /// <summary>The name on the Google account, as it arrived.</summary>
        public static string GoogleName { get; private set; }

        /// <summary>The account name cut to the length a name may be, or empty.</summary>
        public static string UserName { get; private set; }

        /// <summary>The picture named on the token.</summary>
        public static string PictureUrl { get; private set; }

        /// <summary>The refresh token, mirrored here so a scene change does not lose it either.</summary>
        public static string RefreshToken { get; private set; }

        /// <summary>True when there is a token worth sending right now.</summary>
        public static bool IsSignedIn =>
            !string.IsNullOrEmpty(PlayerId) &&
            !string.IsNullOrEmpty(IdToken) &&
            Time.realtimeSinceStartup < ExpiresAt;

        /// <summary>Seconds until the token expires, floored at zero.</summary>
        public static float SecondsLeft => Mathf.Max(0f, ExpiresAt - Time.realtimeSinceStartup);

        public static void Remember(
            string idToken,
            int expiresInSeconds,
            string playerId,
            string email,
            string googleSubject,
            string googleName,
            string userName,
            string pictureUrl)
        {
            IdToken = idToken;

            // A server answering with a nonsense lifetime gets Google's own default rather than a
            // token that is already expired, or one good until the heat death of the universe.
            float lifetime = expiresInSeconds > 0 ? Mathf.Min(expiresInSeconds, 86400) : 3600f;
            ExpiresAt = Time.realtimeSinceStartup + lifetime;

            PlayerId = playerId;
            Email = email;
            GoogleSubject = googleSubject;
            GoogleName = googleName;
            UserName = userName;
            PictureUrl = pictureUrl;
        }

        public static void RememberRefreshToken(string refreshToken)
        {
            if (!string.IsNullOrEmpty(refreshToken)) RefreshToken = refreshToken;
        }

        public static void Clear()
        {
            IdToken = null;
            ExpiresAt = 0f;
            PlayerId = null;
            Email = null;
            GoogleSubject = null;
            GoogleName = null;
            UserName = null;
            PictureUrl = null;
            RefreshToken = null;
        }

        /// <summary>Empties the session when play mode starts without a domain reload.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStaticState() => Clear();
    }
}
