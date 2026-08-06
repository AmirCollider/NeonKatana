using System;
using UnityEngine;

namespace NeonKatana
{
    /// <summary>
    /// A player's record as the server keeps it. The field names are the JSON the API speaks, so
    /// <c>JsonUtility</c> can read and write it without a mapping layer in between — which also
    /// means renaming a field here breaks the wire format.
    /// </summary>
    [Serializable]
    public class PlayerProfile
    {
        public string playerId;
        public string username;
        public string photoURL;

        public int highScore;
        public int gamesPlayed;

        /// <summary>Whole seconds, matching the server's <c>total_play_time</c> column.</summary>
        public int totalPlayTime;

        public string selectedColor = "FFFFFF";

        /// <summary>The parts of a profile this device knows about on its own.</summary>
        public static PlayerProfile FromLocalSave() => new PlayerProfile
        {
            highScore = PlayerProgress.HighScore,
            gamesPlayed = PlayerProgress.RunsPlayed,
            totalPlayTime = WholeSeconds.From(PlayerProgress.PlaySeconds),
        };
    }

    /// <summary>What one finished run is worth reporting.</summary>
    [Serializable]
    public class RunResult
    {
        public int score;
        public int durationSeconds;
        public int livesLost;

        /// <summary>Unix time in seconds, so the server never has to trust a local clock format.</summary>
        public long finishedAtUtc;

        /// <summary>Sent as text rather than a number so adding a cause cannot shift the others.</summary>
        public string cause;

        public static RunResult From(int score, float durationSeconds, int livesLost, GameOverCause cause) =>
            new RunResult
            {
                score = score,
                durationSeconds = WholeSeconds.From(durationSeconds),
                livesLost = livesLost,
                finishedAtUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                cause = cause.ToString(),
            };
    }

    /// <summary>One row of the public leaderboard, as <c>/database/get/games/{id}/leaderboard</c> returns it.</summary>
    [Serializable]
    public class LeaderboardEntry
    {
        public int rank;
        public string username;
        public string displayName;
        public int highScore;
        public string photoURL;
        public string selectedColor;
    }

    /// <summary>JsonUtility cannot read a bare JSON array, so a list arrives wrapped in this.</summary>
    [Serializable]
    public class LeaderboardPage
    {
        public LeaderboardEntry[] entries = Array.Empty<LeaderboardEntry>();
    }

    /// <summary>Turns a float of seconds into the whole-second integer the server columns hold.</summary>
    public static class WholeSeconds
    {
        public static int From(float seconds) =>
            seconds <= 0f ? 0 : (int)Mathf.Min(seconds, int.MaxValue);
    }
}
