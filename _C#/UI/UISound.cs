using UnityEngine;

namespace NeonKatana
{
    /// <summary>
    /// The one speaker the interface talks through.
    ///
    /// <para>
    /// <b>Why not an AudioSource on each button.</b> Half the buttons in this game are the last
    /// thing their scene does: Start Game loads <c>MainGame</c>, Back to Menu loads
    /// <c>MainMenu</c>, and a source living on the button is destroyed with the rest of the scene
    /// while its clip is still playing — the tap is cut off mid-knock, which sounds worse than no
    /// tap at all. One source that outlives the load finishes what it started.
    /// </para>
    /// <para>
    /// It also means a click costs nothing to set up. <see cref="ButtonClickSound"/> carries the
    /// clip and nothing else; there is no AudioSource to add to forty buttons and no volume to
    /// keep in step across two scenes.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public class UISound : MonoBehaviour
    {
        static UISound speaker;
        static bool quitting;

        AudioSource source;

        /// <summary>
        /// Plays <paramref name="clip"/> once, over whatever is already playing. Silently does
        /// nothing without a clip, so a button whose clip was never filled in is not an error at
        /// every press.
        /// </summary>
        public static void Play(AudioClip clip, float volume = 1f)
        {
            if (clip == null || quitting || !Application.isPlaying) return;

            UISound found = Speaker();
            if (found == null) return;

            found.source.PlayOneShot(clip, Mathf.Clamp01(volume));
        }

        static UISound Speaker()
        {
            if (speaker != null) return speaker;

            var host = new GameObject("UI Sound");
            DontDestroyOnLoad(host);

            return host.AddComponent<UISound>();
        }

        /// <summary>
        /// Statics survive Play with domain reloading switched off, and a leftover reference to a
        /// destroyed speaker — or a <c>quitting</c> left true by the last run — would mean silence
        /// until the editor is restarted. Cheaper to clear them than to explain that later.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Forget()
        {
            speaker = null;
            quitting = false;
        }

        void Awake()
        {
            if (speaker != null && speaker != this)
            {
                Destroy(gameObject);
                return;
            }

            speaker = this;

            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;

            // Flat 2D: the interface is not somewhere in the world, so it should not drift to one
            // ear as the camera moves, and it must not fall silent when the game is paused.
            source.spatialBlend = 0f;
            source.ignoreListenerPause = true;
        }

        void OnApplicationQuit() => quitting = true;

        void OnDestroy()
        {
            if (speaker == this) speaker = null;
        }
    }
}
