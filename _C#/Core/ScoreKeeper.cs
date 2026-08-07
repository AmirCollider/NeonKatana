using System;
using UnityEngine;

namespace NeonKatana
{
    /// <summary>
    /// Counts the run's score and files the record away when the run ends. Kept apart from
    /// <see cref="GameManager"/> so scoring rules can change without touching how a run works.
    /// </summary>
    [DisallowMultipleComponent]
    public class ScoreKeeper : MonoBehaviour
    {
        public static ScoreKeeper Instance { get; private set; }

        public int Score { get; private set; }

        /// <summary>True once this run has passed the saved record.</summary>
        public bool BeatTheRecord { get; private set; }

        public int HighScore => Mathf.Max(PlayerProgress.HighScore, Score);

        /// <summary>
        /// The record as it stood when this run began.
        /// <para>
        /// Needed now that the record is written the instant it is beaten rather than at the end
        /// of the run: comparing against the <em>stored</em> record would make
        /// <see cref="BeatTheRecord"/> go false again a frame later, because by then the stored
        /// record is this run's own score.
        /// </para>
        /// </summary>
        int recordAtStart;

        /// <summary>Whether this run has been closed off already, so it is never counted twice.</summary>
        bool filed;

        /// <summary>Raised with the new total every time the score changes.</summary>
        public event Action<int> ScoreChanged;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"A second {nameof(ScoreKeeper)} is in the scene. Keeping the first one.", this);
                enabled = false;
                return;
            }

            Instance = this;
            recordAtStart = PlayerProgress.HighScore;
        }

        void Start() => ScoreChanged?.Invoke(Score);

        void OnDestroy()
        {
            // The last safety net, for a run ending in a way nothing announced: the editor's stop
            // button, a window closed on the way out. Usually a no-op, because the run has already
            // been filed by the time anything is being destroyed.
            //
            // Only a run that scored something. Walking into the game scene and straight back out
            // is not a run, and counting it would put a phantom in the player's total every time
            // somebody opened the game and changed their mind.
            if (Score > 0) FileRun();

            if (Instance == this) Instance = null;
        }

        public void Add(int points)
        {
            if (points == 0) return;

            Score = Mathf.Max(0, Score + points);

            if (Score > recordAtStart)
            {
                BeatTheRecord = true;

                // ==========================================
                // Written here, not at the end of the run.
                //
                // The record used to be filed away by SaveRecord below, which only ever ran when
                // the run ended in a loss. So the one thing a player most wants kept — the score
                // that beat their best — was the thing most easily lost: close the game on a good
                // run, take a phone call, press Exit while ahead, and it was as though it had
                // never happened. Nothing else in this game asks the player to lose before it will
                // remember what they did.
                //
                // The number lands in memory on every point, which is what the labels read. The
                // disk write behind it is throttled — see PlayerProgress.CommitHighScore. Past
                // their old record every fruit sets a new one, and a synchronous file write per
                // fruit is a stutter in the best part of the run.
                // ==========================================
                PlayerProgress.CommitHighScore(Score);
            }

            ScoreChanged?.Invoke(Score);
        }

        /// <summary>
        /// Closes this run's books: the record, the run count, and the write to disk.
        /// <para>
        /// Called by <see cref="GameManager"/> as a run ends, <b>before</b> it tells anybody else,
        /// because what the outbox uploads is read straight back out of this save — so a run
        /// queued before its own numbers were written is a run reported one short.
        /// </para>
        /// <para>
        /// Runs at most once. Losing, walking out and the scene being torn down all arrive here,
        /// and a run counted twice is a run the server is told about twice.
        /// </para>
        /// </summary>
        public void FileRun()
        {
            if (filed) return;
            filed = true;

            PlayerProgress.TrySetHighScore(Score);
            PlayerProgress.CountRun();
            PlayerProgress.Save();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStaticState() => Instance = null;
    }
}
