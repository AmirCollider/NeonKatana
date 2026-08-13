using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace NeonKatana.EditorTools
{
    /// <summary>
    /// Re-anchors UI elements to the edge of the screen they were meant to sit against.
    ///
    /// <para>
    /// <b>The problem this exists for.</b> Every canvas in this project is Scale With Screen Size,
    /// reference 3120×1440, match 0.5. That does not fix the canvas at 3120×1440 — it picks a scale
    /// factor and the canvas then comes out however big the screen makes it. At the 2712×1220 the
    /// Game view was set to, the canvas works out at 3160.26 × 1421.65 units, so its top-right
    /// corner is at (1580.13, 710.83).
    /// </para>
    /// <para>
    /// The <c>Lives</c> row is at (1580.1323, 710.8265). The pause button, the flag and the store
    /// button are all placed the same way — to the exact unit, against corners that only exist at
    /// that one resolution — while their anchors were left in the middle of the canvas. So the
    /// offsets are measured from the centre, the centre-to-corner distance changes with every
    /// screen, and on a phone everything lands somewhere else or off the edge entirely. It is not
    /// letterboxing and not a notch; it is arithmetic.
    /// </para>
    /// <para>
    /// Anchoring an element to the corner it belongs to measures its offset from <i>that corner</i>
    /// instead, and the corner is wherever the screen puts it. Sixteen pixels in from the top right
    /// stays sixteen pixels in from the top right, on every device.
    /// </para>
    /// </summary>
    public class UIAnchorFixer : EditorWindow
    {
        /// <summary>
        /// How close to an edge an element has to be, as a fraction of the parent, before it is
        /// treated as belonging to that edge rather than to the middle.
        /// <para>
        /// Deliberately not a third. A menu column at the bottom of the screen is still a column
        /// laid out from the centre, and anchoring its last button to the bottom edge would pull
        /// that button away from the two above it on any screen of a different shape. Only things
        /// genuinely pinned against an edge should stop being measured from the centre.
        /// </para>
        /// </summary>
        float edgeBand = 0.12f;

        bool wholeScene;
        bool includeInactive = true;

        Vector2 scroll;
        readonly List<Candidate> found = new List<Candidate>();

        struct Candidate
        {
            public RectTransform element;
            public Vector2 from;
            public Vector2 to;
        }

        [MenuItem("Neon Katana/UI/Fix Anchors…", false, 100)]
        static void Open() => GetWindow<UIAnchorFixer>(true, "Fix Anchors", true).minSize = new Vector2(460f, 320f);

        void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Elements placed against a screen corner but anchored to the middle of the canvas " +
                "only land in that corner at the one resolution they were dragged into place at. " +
                "This re-anchors them to the corner or edge they belong to, without moving them.",
                MessageType.Info);

            EditorGUILayout.Space();

            wholeScene = EditorGUILayout.Toggle(
                new GUIContent("Whole scene", "Off: only what is selected in the Hierarchy."),
                wholeScene);

            includeInactive = EditorGUILayout.Toggle(
                new GUIContent("Include switched-off", "Panels that start hidden need this."),
                includeInactive);

            edgeBand = EditorGUILayout.Slider(
                new GUIContent("Edge band", "How near an edge counts as belonging to it."),
                edgeBand, 0.02f, 0.33f);

            EditorGUILayout.Space();

            if (GUILayout.Button("Find what would change", GUILayout.Height(24f))) Scan();

            if (found.Count == 0)
            {
                EditorGUILayout.LabelField("Nothing found yet.", EditorStyles.miniLabel);
                return;
            }

            EditorGUILayout.LabelField($"{found.Count} element(s) would be re-anchored:", EditorStyles.boldLabel);

            scroll = EditorGUILayout.BeginScrollView(scroll);

            foreach (Candidate candidate in found)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.ObjectField(candidate.element, typeof(RectTransform), true);
                EditorGUILayout.LabelField($"{Name(candidate.from)} → {Name(candidate.to)}", GUILayout.Width(150f));
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.Space();

            GUI.backgroundColor = new Color(0.6f, 0.9f, 0.6f);

            if (GUILayout.Button("Apply (undoable)", GUILayout.Height(28f))) Apply();

            GUI.backgroundColor = Color.white;
        }

        void Scan()
        {
            found.Clear();

            foreach (RectTransform element in Targets())
            {
                if (!ShouldMove(element, out Vector2 wanted)) continue;

                found.Add(new Candidate
                {
                    element = element,
                    from = element.anchorMin,
                    to = wanted,
                });
            }

            found.Sort((a, b) => string.CompareOrdinal(Path(a.element), Path(b.element)));
        }

        void Apply()
        {
            var report = new StringBuilder();

            foreach (Candidate candidate in found)
            {
                if (candidate.element == null) continue;

                Undo.RecordObject(candidate.element, "Fix Anchors");
                Reanchor(candidate.element, candidate.to);
                EditorUtility.SetDirty(candidate.element);

                report.AppendLine($"  {Path(candidate.element)} → {Name(candidate.to)}");
            }

            Debug.Log($"Re-anchored {found.Count} element(s):\n{report}");

            found.Clear();
        }

        IEnumerable<RectTransform> Targets()
        {
            if (!wholeScene)
            {
                foreach (GameObject selected in Selection.gameObjects)
                {
                    foreach (RectTransform element in selected.GetComponentsInChildren<RectTransform>(includeInactive))
                        yield return element;
                }

                yield break;
            }

            foreach (Canvas canvas in Resources.FindObjectsOfTypeAll<Canvas>())
            {
                // Prefab previews and assets are not what anybody has open.
                if (!canvas.gameObject.scene.IsValid()) continue;

                foreach (RectTransform element in canvas.GetComponentsInChildren<RectTransform>(includeInactive))
                    yield return element;
            }
        }

        /// <summary>
        /// Whether this element is sitting against an edge while anchored somewhere else, and which
        /// anchor it should have instead.
        /// </summary>
        bool ShouldMove(RectTransform element, out Vector2 wanted)
        {
            wanted = Vector2.zero;

            if (element == null) return false;

            RectTransform parent = element.parent as RectTransform;
            if (parent == null) return false;

            // A canvas root's own rect is the canvas's business, and a stretched element is
            // already resolution-independent — re-anchoring it to a point would break it.
            if (element.GetComponent<Canvas>() != null) return false;
            if (element.anchorMin != element.anchorMax) return false;

            Rect area = parent.rect;
            if (area.width <= 0f || area.height <= 0f) return false;

            Bounds(element, parent, out Vector2 min, out Vector2 max);

            Vector2 middle = (min + max) * 0.5f;

            float x = (middle.x - area.xMin) / area.width;
            float y = (middle.y - area.yMin) / area.height;

            wanted = new Vector2(Snap(x), Snap(y));

            return wanted != element.anchorMin;
        }

        float Snap(float normalized)
        {
            if (normalized <= edgeBand) return 0f;
            if (normalized >= 1f - edgeBand) return 1f;

            return 0.5f;
        }

        /// <summary>The element's rect in its parent's own coordinates.</summary>
        static void Bounds(RectTransform element, RectTransform parent, out Vector2 min, out Vector2 max)
        {
            Rect area = parent.rect;

            var atMin = new Vector2(
                area.xMin + element.anchorMin.x * area.width,
                area.yMin + element.anchorMin.y * area.height);

            var atMax = new Vector2(
                area.xMin + element.anchorMax.x * area.width,
                area.yMin + element.anchorMax.y * area.height);

            min = atMin + element.offsetMin;
            max = atMax + element.offsetMax;
        }

        /// <summary>
        /// Moves the anchor without moving the element. The rect's two corners are worked out
        /// first and put back afterwards, so the size and the pivot come through untouched.
        /// </summary>
        public static void Reanchor(RectTransform element, Vector2 anchor)
        {
            if (element == null) return;

            RectTransform parent = element.parent as RectTransform;
            if (parent == null) return;

            Bounds(element, parent, out Vector2 min, out Vector2 max);

            Rect area = parent.rect;

            var at = new Vector2(
                area.xMin + anchor.x * area.width,
                area.yMin + anchor.y * area.height);

            element.anchorMin = anchor;
            element.anchorMax = anchor;

            element.offsetMin = min - at;
            element.offsetMax = max - at;
        }

        static string Name(Vector2 anchor)
        {
            string across = anchor.x == 0f ? "left" : anchor.x == 1f ? "right" : "centre";
            string up = anchor.y == 0f ? "bottom" : anchor.y == 1f ? "top" : "middle";

            return $"{up}-{across}";
        }

        static string Path(RectTransform element)
        {
            if (element == null) return "(gone)";

            var built = new StringBuilder(element.name);

            for (Transform above = element.parent; above != null; above = above.parent)
                built.Insert(0, above.name + "/");

            return built.ToString();
        }
    }
}
