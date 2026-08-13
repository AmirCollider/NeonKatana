using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NeonKatana.EditorTools
{
    /// <summary>
    /// Finds every button in the scenes that are open and gives each one its click sound.
    ///
    /// <para>
    /// <b>Why a tool and not a hand.</b> There are buttons in two scenes, several of them inside
    /// panels that start switched off — the pause menu, the lose screen, the account screen — so
    /// they cannot all be seen in the Hierarchy at once, let alone selected. Doing it by hand means
    /// finding forty objects, adding the same component forty times and dragging the same clip into
    /// forty fields, and the one that gets missed is always the one nobody presses until release.
    /// </para>
    /// <para>
    /// Nothing here is decided quietly. It scans first and lists exactly what it would touch, and
    /// applying is a single undo step, so a scan on the wrong scene costs one Ctrl+Z.
    /// </para>
    /// </summary>
    public class ButtonClickSoundInstaller : EditorWindow
    {
        /// <summary>Where the sound lives, and what it is called if it has been moved.</summary>
        const string ClipPath = "Assets/Audio/SFX/WoodTabSoundEffect.wav";
        const string ClipName = "WoodTabSoundEffect";

        AudioClip clip;
        float volume = 1f;

        bool wholeScene = true;
        bool includeInactive = true;

        Vector2 scroll;
        readonly List<Candidate> found = new List<Candidate>();

        /// <summary>What applying would do to one button.</summary>
        enum Work
        {
            /// <summary>No <see cref="ButtonClickSound"/> on it yet.</summary>
            Add,

            /// <summary>Has one, but pointed at the wrong clip or at nothing.</summary>
            Refill,

            /// <summary>Already exactly right; left alone.</summary>
            Done,
        }

        struct Candidate
        {
            public Button button;
            public ButtonClickSound sound;
            public Work work;
        }

        [MenuItem("Neon Katana/UI/Button Click Sound…", false, 101)]
        static void Open() =>
            GetWindow<ButtonClickSoundInstaller>(true, "Button Click Sound", true).minSize = new Vector2(460f, 340f);

        /// <summary>
        /// The same job without the window, for when it is obvious what is wanted: every button in
        /// every open scene, switched-off panels included.
        /// </summary>
        [MenuItem("Neon Katana/UI/Add Click Sound To Every Button", false, 102)]
        static void RunOnEverything()
        {
            var window = CreateInstance<ButtonClickSoundInstaller>();

            window.clip = FindClip();

            if (window.clip == null)
            {
                Debug.LogError(
                    $"Could not find {ClipName}. Expected it at {ClipPath} — import it, or use " +
                    "Neon Katana ▸ UI ▸ Button Click Sound… and pick it by hand.");

                DestroyImmediate(window);
                return;
            }

            window.Scan();
            window.Apply();

            DestroyImmediate(window);
        }

        void OnEnable()
        {
            if (clip == null) clip = FindClip();
        }

        void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Adds ButtonClickSound to every Button it finds, so each click plays the wooden " +
                "tap once. Buttons that already have it are left alone. Scenes are not saved for " +
                "you — save them yourself once you are happy.",
                MessageType.Info);

            EditorGUILayout.Space();

            clip = (AudioClip)EditorGUILayout.ObjectField(
                new GUIContent("Sound", "The clip every button plays when clicked."),
                clip, typeof(AudioClip), false);

            if (clip == null)
            {
                EditorGUILayout.HelpBox($"{ClipName} was not found at {ClipPath}. Drag it in above.", MessageType.Warning);
            }

            volume = EditorGUILayout.Slider(
                new GUIContent("Volume", "How loud, against the rest of the game."), volume, 0f, 1f);

            wholeScene = EditorGUILayout.Toggle(
                new GUIContent("Every open scene", "Off: only what is selected in the Hierarchy, and its children."),
                wholeScene);

            includeInactive = EditorGUILayout.Toggle(
                new GUIContent("Include switched-off", "The pause menu, the lose screen and the account screen need this."),
                includeInactive);

            EditorGUILayout.Space();

            using (new EditorGUI.DisabledScope(clip == null))
            {
                if (GUILayout.Button("Find the buttons", GUILayout.Height(24f))) Scan();
            }

            if (found.Count == 0)
            {
                EditorGUILayout.LabelField("Nothing found yet.", EditorStyles.miniLabel);
                return;
            }

            int todo = 0;
            foreach (Candidate candidate in found)
                if (candidate.work != Work.Done) todo++;

            EditorGUILayout.LabelField(
                $"{found.Count} button(s) found · {todo} to change · {found.Count - todo} already done",
                EditorStyles.boldLabel);

            scroll = EditorGUILayout.BeginScrollView(scroll);

            foreach (Candidate candidate in found)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.ObjectField(candidate.button, typeof(Button), true);
                EditorGUILayout.LabelField(Says(candidate.work), GUILayout.Width(120f));
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.Space();

            using (new EditorGUI.DisabledScope(todo == 0))
            {
                GUI.backgroundColor = new Color(0.6f, 0.9f, 0.6f);

                if (GUILayout.Button("Apply (undoable)", GUILayout.Height(28f))) Apply();

                GUI.backgroundColor = Color.white;
            }

            if (GUILayout.Button("Take the sound off every button again")) Strip();
        }

        void Scan()
        {
            found.Clear();

            var seen = new HashSet<Button>();

            foreach (Button button in Buttons())
            {
                if (button == null || !seen.Add(button)) continue;

                var sound = button.GetComponent<ButtonClickSound>();

                found.Add(new Candidate
                {
                    button = button,
                    sound = sound,
                    work = sound == null ? Work.Add : sound.Clip == clip ? Work.Done : Work.Refill,
                });
            }

            found.Sort((a, b) => string.CompareOrdinal(Path(a.button), Path(b.button)));

            if (found.Count == 0) Debug.Log("No buttons found. Is the scene you meant open, and is anything selected?");
        }

        void Apply()
        {
            if (clip == null) return;

            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Add Button Click Sound");

            var report = new StringBuilder();
            int changed = 0;

            foreach (Candidate candidate in found)
            {
                if (candidate.button == null || candidate.work == Work.Done) continue;

                ButtonClickSound sound = candidate.sound != null
                    ? candidate.sound
                    : Undo.AddComponent<ButtonClickSound>(candidate.button.gameObject);

                // Through SerializedObject rather than the fields: it records the undo, respects
                // prefab overrides and marks the object dirty, none of which a direct assignment
                // to a private field could do anyway.
                var editing = new SerializedObject(sound);
                editing.FindProperty("clip").objectReferenceValue = clip;
                editing.FindProperty("volume").floatValue = volume;
                editing.ApplyModifiedProperties();

                MarkDirty(sound);

                report.AppendLine($"  {Says(candidate.work)}  {Path(candidate.button)}");
                changed++;
            }

            Undo.CollapseUndoOperations(group);

            Debug.Log(changed == 0
                ? "Every button already had its click sound."
                : $"Click sound on {changed} button(s):\n{report}Save the scene to keep it.");

            Scan();
        }

        /// <summary>Undo for people who did not press Ctrl+Z in time.</summary>
        void Strip()
        {
            if (!EditorUtility.DisplayDialog(
                    "Take the click sound off?",
                    "Removes ButtonClickSound from every button in range. Undoable.",
                    "Remove", "Keep it"))
            {
                return;
            }

            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Remove Button Click Sound");

            int removed = 0;

            foreach (Button button in Buttons())
            {
                if (button == null) continue;

                var sound = button.GetComponent<ButtonClickSound>();
                if (sound == null) continue;

                Scene scene = button.gameObject.scene;

                Undo.DestroyObjectImmediate(sound);

                if (scene.IsValid()) EditorSceneManager.MarkSceneDirty(scene);

                removed++;
            }

            Undo.CollapseUndoOperations(group);

            Debug.Log($"Took the click sound off {removed} button(s).");

            Scan();
        }

        /// <summary>
        /// Every button in range: the selection, or the roots of every open scene — plus the prefab
        /// being edited, if the window was opened from inside one.
        /// </summary>
        IEnumerable<Button> Buttons()
        {
            if (!wholeScene)
            {
                foreach (GameObject selected in Selection.gameObjects)
                {
                    foreach (Button button in selected.GetComponentsInChildren<Button>(includeInactive))
                        yield return button;
                }

                yield break;
            }

            var stage = PrefabStageUtility.GetCurrentPrefabStage();

            if (stage != null)
            {
                foreach (Button button in stage.prefabContentsRoot.GetComponentsInChildren<Button>(includeInactive))
                    yield return button;

                yield break;
            }

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;

                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    foreach (Button button in root.GetComponentsInChildren<Button>(includeInactive))
                        yield return button;
                }
            }
        }

        static void MarkDirty(Component component)
        {
            if (component == null) return;

            EditorUtility.SetDirty(component);

            Scene scene = component.gameObject.scene;
            if (scene.IsValid()) EditorSceneManager.MarkSceneDirty(scene);
        }

        /// <summary>
        /// The clip, by path first and by name second, so moving it out of <c>Audio/SFX</c> makes
        /// the tool look for it rather than quietly do nothing.
        /// </summary>
        static AudioClip FindClip()
        {
            var atPath = AssetDatabase.LoadAssetAtPath<AudioClip>(ClipPath);
            if (atPath != null) return atPath;

            foreach (string guid in AssetDatabase.FindAssets($"{ClipName} t:AudioClip"))
            {
                var elsewhere = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guid));

                if (elsewhere != null && elsewhere.name == ClipName) return elsewhere;
            }

            return null;
        }

        static string Says(Work work) => work switch
        {
            Work.Add => "add sound",
            Work.Refill => "set the clip",
            _ => "already done",
        };

        static string Path(Button button)
        {
            if (button == null) return "(gone)";

            var built = new StringBuilder(button.name);

            for (Transform above = button.transform.parent; above != null; above = above.parent)
                built.Insert(0, above.name + "/");

            return built.ToString();
        }
    }
}
