using UnityEngine;
using UnityEngine.UI;

namespace NeonKatana
{
    /// <summary>
    /// Makes a button do something without anybody wiring its OnClick by hand. Drop this on the
    /// button, pick what it should do from the list, and it hooks itself up when the scene loads.
    /// <para>
    /// The saved value is a name from an enum, not a reference to an object and a method, so it
    /// survives renaming scripts, rebuilding prefabs and copying buttons between scenes.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public class UIActionButton : MonoBehaviour
    {
        [SerializeField] GameAction action = GameAction.StartGame;

        [Tooltip("Only used by Open Website.")]
        [SerializeField] string url = "https://amircollider.com";

        Button button;

        void Awake()
        {
            button = GetComponent<Button>();
            button.onClick.AddListener(Press);
        }

        void OnDestroy()
        {
            if (button != null) button.onClick.RemoveListener(Press);
        }

        /// <summary>Does what the button says. Public so anything else can trigger it too.</summary>
        public void Press() => GameActions.Run(action, url);
    }
}
