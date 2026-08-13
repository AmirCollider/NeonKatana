using UnityEngine;
using UnityEngine.UI;

namespace NeonKatana
{
    /// <summary>
    /// Gives a button its knock: one <c>WoodTabSoundEffect</c> per click, and only when the click
    /// actually counts.
    ///
    /// <para>
    /// It listens to <see cref="Button.onClick"/> rather than to the pointer, which is the whole
    /// point of it being a separate component from <see cref="ButtonPress"/>. A press that starts
    /// on a button and is dragged off before release is not a click, a button that has been made
    /// non-interactable does not click at all, and neither should make a sound — <c>onClick</c>
    /// already knows all of that, and the pointer handlers do not.
    /// </para>
    /// <para>
    /// The clip is a field rather than something looked up by name so that a button can be given a
    /// different sound later without touching any code. <c>Neon Katana ▸ UI ▸ Button Click Sound…</c>
    /// is what fills it in across a whole scene.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public class ButtonClickSound : MonoBehaviour
    {
        [Tooltip("Played once per click. Usually Audio/SFX/WoodTabSoundEffect.")]
        [SerializeField] AudioClip clip;

        [Tooltip("How loud, against the rest of the game.")]
        [SerializeField, Range(0f, 1f)] float volume = 1f;

        Button button;

        /// <summary>What this button plays. Read-only: the editor tool sets the field itself.</summary>
        public AudioClip Clip => clip;

        void Awake()
        {
            button = GetComponent<Button>();
            button.onClick.AddListener(Play);
        }

        void OnDestroy()
        {
            if (button != null) button.onClick.RemoveListener(Play);
        }

        /// <summary>
        /// Knocks once. Public so a button that is triggered from code — or one wired to something
        /// other than <see cref="Button"/> — can still sound like it was pressed.
        /// </summary>
        public void Play() => UISound.Play(clip, volume);
    }
}
