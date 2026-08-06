using System;

namespace NeonKatana
{
    /// <summary>
    /// Progress kept on the device, through <see cref="PlayerProgress"/>. This is what the game
    /// runs on until a server is wired up, and it stays the fallback afterwards — a player with no
    /// connection still keeps their record.
    /// </summary>
    public class LocalProgressBackend : IProgressBackend
    {
        public bool IsAvailable => true;

        public void LoadProfile(Action<PlayerProfile, string> onDone)
        {
            onDone?.Invoke(PlayerProfile.FromLocalSave(), null);
        }

        public void SubmitRun(RunResult run, PlayerProfile totals, Action<bool, string> onDone)
        {
            // The score and the playtime are already written as the run ends; there is nothing
            // further to store locally. Kept as a no-op so the two backends stay interchangeable.
            onDone?.Invoke(true, null);
        }

        public void LoadLeaderboard(Action<LeaderboardEntry[], string> onDone)
        {
            onDone?.Invoke(Array.Empty<LeaderboardEntry>(), "There is no leaderboard while the game is offline.");
        }
    }
}
