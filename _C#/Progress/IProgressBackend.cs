using System;

namespace NeonKatana
{
    /// <summary>
    /// The seam between the game and wherever progress is kept. The game only ever talks to this,
    /// so going online is a matter of handing <see cref="ProgressService"/> a different backend —
    /// no gameplay code changes, and an offline build keeps working by using the local one.
    /// </summary>
    public interface IProgressBackend
    {
        /// <summary>False when there is nobody signed in, or no network to reach.</summary>
        bool IsAvailable { get; }

        /// <summary>Reads the stored profile. <paramref name="onDone"/> gets null on failure.</summary>
        void LoadProfile(Action<PlayerProfile, string> onDone);

        /// <summary>
        /// Reports one finished run. Called again later for the same run if it failed, so the
        /// backend must treat a repeat as harmless rather than as a second run.
        /// </summary>
        void SubmitRun(RunResult run, PlayerProfile totals, Action<bool, string> onDone);

        /// <summary>Reads the public leaderboard. Does not need anybody to be signed in.</summary>
        void LoadLeaderboard(Action<LeaderboardEntry[], string> onDone);
    }
}
