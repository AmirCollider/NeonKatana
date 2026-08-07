using System;
using UnityEngine;

namespace NeonKatana
{
    /// <summary>
    /// Everything that outlives a single run: the best score, how long the game has been played
    /// altogether, and the chosen language. All the PlayerPrefs keys live here so nothing else in
    /// the game has to know how saving works.
    /// </summary>
    public static class PlayerProgress
    {
        const string HighScoreKey = "NeonKatana.HighScore";
        const string PlaySecondsKey = "NeonKatana.PlaySeconds";
        const string RunsPlayedKey = "NeonKatana.RunsPlayed";
        const string LanguageKey = "NeonKatana.Language";

        /// <summary>
        /// Which account the totals below belong to. Empty means nobody has claimed them yet.
        /// </summary>
        const string OwnerKey = "NeonKatana.ProgressOwner";

        public static int HighScore => PlayerPrefs.GetInt(HighScoreKey, 0);

        /// <summary>How long the player has spent in the game across every session, in seconds.</summary>
        public static float PlaySeconds => PlayerPrefs.GetFloat(PlaySecondsKey, 0f);

        public static int RunsPlayed => PlayerPrefs.GetInt(RunsPlayedKey, 0);

        /// <summary>Stores the score if it beats the record. Returns true when it was a new best.</summary>
        public static bool TrySetHighScore(int score)
        {
            if (score <= HighScore) return false;

            PlayerPrefs.SetInt(HighScoreKey, score);
            return true;
        }

        /// <summary>When the record was last pushed all the way to disk.</summary>
        static float lastFlushedAt = float.NegativeInfinity;

        /// <summary>
        /// How long a freshly set record may live in memory before it is written down. Four
        /// seconds of a run is a handful of points; four seconds of stutter is the run.
        /// </summary>
        const float FlushEverySeconds = 4f;

        /// <summary>
        /// Stores the score if it beats the record, and writes it to disk — but not on every call.
        /// <para>
        /// The record used to be filed away by <see cref="ScoreKeeper"/> when the run ended, and
        /// only then. A player who beat their best and then closed the game — or walked out through
        /// the pause menu, or was killed by Android reclaiming memory — had beaten it for nothing.
        /// So it is written as it happens instead.
        /// </para>
        /// <para>
        /// <b>Why this is throttled.</b> <see cref="PlayerPrefs"/> is a dictionary in memory and
        /// <see cref="PlayerPrefs.Save"/> is the part that touches the disk — it serialises the
        /// whole store and waits for the write. Once a player is past their old record, every
        /// single fruit sets a new one, so saving on each would mean a synchronous file write
        /// several times a second, on a phone, during the best part of the run. That is a stutter
        /// exactly where it is least welcome.
        /// </para>
        /// <para>
        /// The number is in memory the instant it is scored, which is what everything on screen
        /// reads. Only the disk lags, by at most <see cref="FlushEverySeconds"/> — and every way a
        /// run can end goes through <see cref="Save"/> unconditionally, so the lag can only ever
        /// lose a few points to a crash, never to a player closing the game.
        /// </para>
        /// </summary>
        public static bool CommitHighScore(int score)
        {
            if (!TrySetHighScore(score)) return false;

            if (Time.realtimeSinceStartup - lastFlushedAt < FlushEverySeconds) return true;

            lastFlushedAt = Time.realtimeSinceStartup;
            Save();

            return true;
        }

        /// <summary>Adds to the running total. Kept, but deliberately never shown to the player.</summary>
        public static void AddPlaySeconds(float seconds)
        {
            if (seconds <= 0f) return;

            PlayerPrefs.SetFloat(PlaySecondsKey, PlaySeconds + seconds);
        }

        public static void CountRun() => PlayerPrefs.SetInt(RunsPlayedKey, RunsPlayed + 1);

        /// <summary>
        /// Takes on the server's totals wherever they are ahead of this device's.
        ///
        /// <para>
        /// A lifetime total belongs to the player, not to the phone. Reinstalling the game, or
        /// clearing the save, leaves this device believing the player has played once — and since
        /// what gets sent is the total rather than the difference, that belief used to be written
        /// straight over the real history the moment the first run finished.
        /// </para>
        /// <para>
        /// The server refuses to go backwards now, so nothing is lost either way. This is the
        /// other half: without it the device stays permanently behind, sending totals the server
        /// keeps discarding, and the number the player sees here never agrees with the site.
        /// </para>
        /// <para>Only ever upward — a smaller server value means the device is simply ahead.</para>
        /// </summary>
        public static void AdoptIfHigher(int highScore, int runsPlayed, int playSeconds)
        {
            bool changed = false;

            if (highScore > HighScore) { PlayerPrefs.SetInt(HighScoreKey, highScore); changed = true; }
            if (runsPlayed > RunsPlayed) { PlayerPrefs.SetInt(RunsPlayedKey, runsPlayed); changed = true; }
            if (playSeconds > PlaySeconds) { PlayerPrefs.SetFloat(PlaySecondsKey, playSeconds); changed = true; }

            if (changed) Save();
        }

        // --- Whose totals these are ---

        /// <summary>The account the stored totals belong to, or empty when nobody has claimed them.</summary>
        public static string Owner => PlayerPrefs.GetString(OwnerKey, string.Empty);

        /// <summary>
        /// Hands this device's totals to <paramref name="playerId"/>, wiping them first if they
        /// belong to somebody else. Returns true when they were wiped.
        ///
        /// <para>
        /// The totals here are a device's, and the game only ever had one set of them. So signing
        /// out of one account and into another left the second account holding the first one's
        /// record — and then, because what goes to the server is the running total rather than the
        /// difference, <em>uploaded</em> it: sign in on a friend's phone and you took their high
        /// score home with you, under your name, on the public board. That is the bug this closes,
        /// and it closes it in the only direction that is safe. Totals earned by one person are
        /// never handed to another.
        /// </para>
        /// <para>
        /// Unclaimed totals are adopted rather than wiped. Someone who played before signing in
        /// earned those, and the account they then sign into is theirs — so the score follows them
        /// in. It is only the second, different account that starts from nothing, and it does not
        /// stay at nothing for long: <see cref="AdoptIfHigher"/> fills it back in from that
        /// account's own record as soon as the server answers.
        /// </para>
        /// </summary>
        public static bool ClaimFor(string playerId)
        {
            if (string.IsNullOrWhiteSpace(playerId)) return false;

            string current = Owner;

            if (string.Equals(current, playerId, StringComparison.OrdinalIgnoreCase)) return false;

            bool belongedToSomebodyElse = !string.IsNullOrEmpty(current);

            if (belongedToSomebodyElse) ResetTotals();

            PlayerPrefs.SetString(OwnerKey, playerId);
            Save();

            return belongedToSomebodyElse;
        }

        /// <summary>
        /// Empties the lifetime totals, leaving the language and everything else alone. The server
        /// keeps the real history, so this is a device forgetting rather than a player losing.
        /// </summary>
        public static void ResetTotals()
        {
            PlayerPrefs.SetInt(HighScoreKey, 0);
            PlayerPrefs.SetInt(RunsPlayedKey, 0);
            PlayerPrefs.SetFloat(PlaySecondsKey, 0f);
            Save();
        }

        /// <summary>
        /// The stored language, or <paramref name="fallback"/> when there is none. An unknown
        /// number also falls back, so a save written by a build that had more languages than this
        /// one cannot leave the game with no language at all.
        /// </summary>
        public static GameLanguage GetLanguage(GameLanguage fallback)
        {
            int stored = PlayerPrefs.GetInt(LanguageKey, (int)fallback);

            return Enum.IsDefined(typeof(GameLanguage), stored) ? (GameLanguage)stored : fallback;
        }

        /// <summary>
        /// The language as a BCP-47 tag — what a server or analytics service expects, and stable
        /// even if the enum is renumbered.
        /// </summary>
        public static string LanguageTag(GameLanguage language) => language switch
        {
            GameLanguage.Persian => "fa",
            GameLanguage.Japanese => "ja",
            _ => "en",
        };

        public static void SetLanguage(GameLanguage language)
        {
            PlayerPrefs.SetInt(LanguageKey, (int)language);
            Save();
        }

        /// <summary>Writes to disk. Android can kill the app without warning, so call it early.</summary>
        public static void Save() => PlayerPrefs.Save();

        /// <summary>
        /// Forgets when the record was last written when play mode starts without a domain
        /// reload, so the first record of a session is never held back by the last one's clock.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStaticState() => lastFlushedAt = float.NegativeInfinity;

        /// <summary>Wipes every saved value. Handy for a "reset progress" button while testing.</summary>
        public static void Clear()
        {
            PlayerPrefs.DeleteKey(HighScoreKey);
            PlayerPrefs.DeleteKey(PlaySecondsKey);
            PlayerPrefs.DeleteKey(RunsPlayedKey);
            PlayerPrefs.DeleteKey(LanguageKey);
            PlayerPrefs.DeleteKey(OwnerKey);
            Save();
        }
    }
}
