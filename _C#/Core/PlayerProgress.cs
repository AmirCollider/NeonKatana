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

        /// <summary>Adds to the running total. Kept, but deliberately never shown to the player.</summary>
        public static void AddPlaySeconds(float seconds)
        {
            if (seconds <= 0f) return;

            PlayerPrefs.SetFloat(PlaySecondsKey, PlaySeconds + seconds);
        }

        public static void CountRun() => PlayerPrefs.SetInt(RunsPlayedKey, RunsPlayed + 1);

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

        /// <summary>Wipes every saved value. Handy for a "reset progress" button while testing.</summary>
        public static void Clear()
        {
            PlayerPrefs.DeleteKey(HighScoreKey);
            PlayerPrefs.DeleteKey(PlaySecondsKey);
            PlayerPrefs.DeleteKey(RunsPlayedKey);
            PlayerPrefs.DeleteKey(LanguageKey);
            Save();
        }
    }
}
