using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NeonKatana
{
    /// <summary>
    /// The fruit that hangs in the air behind the menu, turning and swaying, waiting to be cut.
    /// Gravity is switched off so nothing falls; cutting one drops its halves the normal way,
    /// because the sliced prefab is untouched and still weighs something.
    /// <para>
    /// The places are fixed and the fruit in each is picked at random, so the menu is laid out the
    /// same every time but never shows the same arrangement twice.
    /// </para>
    /// </summary>
    public class MenuFruitShowcase : MonoBehaviour
    {
        [Header("What to show")]
        [SerializeField] Fruit[] fruitPrefabs;

        [Header("Where")]
        [Tooltip(
            "Places as a fraction of the screen: (0,0) is the bottom-left corner and (1,1) the " +
            "top-right, whatever shape the screen is. Leave this on.")]
        [SerializeField] bool placeByScreen = true;

        [Tooltip("One fruit hangs at each of these. 0 to 1 across the screen, 0 to 1 up it.")]
        [SerializeField] Vector2[] screenSlots =
        {
            new Vector2(0.08f, 0.72f),
            new Vector2(0.20f, 0.42f),
            new Vector2(0.83f, 0.32f),
            new Vector2(0.93f, 0.52f),
            new Vector2(0.05f, 0.22f),
        };

        [Tooltip(
            "The old places, in world units. Only used with 'Place By Screen' off. Use the " +
            "component's context menu to convert them to screen places without losing the layout.")]
        [SerializeField] Vector2[] slots =
        {
            new Vector2(-7.75f, 2.8f),
            new Vector2(-5.25f, -1.35f),
            new Vector2(6.65f, -2.5f),
            new Vector2(8.7f, 0.25f),
            new Vector2(-8.85f, -3.5f),
        };

        [Tooltip("How far in front of the camera the fruit sits.")]
        [SerializeField] float depth;

        [Tooltip("The camera the places are measured against. Falls back to the main camera.")]
        [SerializeField] Camera viewCamera;

        [Header("Sway")]
        [Tooltip("How far each fruit drifts up and down from its place.")]
        [SerializeField] float bobDistance = 0.45f;
        [Tooltip("How far each fruit drifts side to side from its place.")]
        [SerializeField] float swayDistance = 0.25f;
        [Tooltip("Full up-and-down trips per second.")]
        [SerializeField] Vector2 bobSpeed = new Vector2(0.25f, 0.45f);

        [Header("After a cut")]
        [Tooltip("Seconds before a fresh fruit takes the empty place. 0 leaves the gap.")]
        [SerializeField, Min(0f)] float replaceAfter = 2.5f;

        /// <summary>One hanging fruit, and the drift that makes it look alive.</summary>
        class Hanging
        {
            public Fruit fruit;
            public int slot;
            public Vector3 home;
            public float phase;
            public float speed;
        }

        readonly List<Hanging> hanging = new List<Hanging>();

        /// <summary>The screen the places were last worked out for; they are redone when it changes.</summary>
        Vector2Int measuredFor;

        void Start()
        {
            if (fruitPrefabs == null || fruitPrefabs.Length == 0)
            {
                Debug.LogWarning($"{nameof(MenuFruitShowcase)} has no fruit to show.", this);
                return;
            }

            if (viewCamera == null) viewCamera = Camera.main;

            measuredFor = new Vector2Int(Screen.width, Screen.height);

            for (int slot = 0; slot < SlotCount; slot++) Hang(slot);
        }

        void Update()
        {
            NoticeTheScreenChanged();

            for (int index = hanging.Count - 1; index >= 0; index--)
            {
                Hanging entry = hanging[index];

                // The fruit destroys itself when cut, so a missing one means somebody got it.
                if (entry.fruit == null)
                {
                    hanging.RemoveAt(index);
                    if (replaceAfter > 0f) StartCoroutine(HangAgainLater(entry.slot));
                    continue;
                }

                Drift(entry);
            }
        }

        /// <summary>
        /// A screen place is only a world place once the screen is known, so a rotation or a
        /// resize makes every one of them out of date.
        /// </summary>
        void NoticeTheScreenChanged()
        {
            if (!placeByScreen) return;
            if (Screen.width == measuredFor.x && Screen.height == measuredFor.y) return;

            measuredFor = new Vector2Int(Screen.width, Screen.height);

            foreach (Hanging entry in hanging) entry.home = HomeOf(entry.slot);
        }

        int SlotCount => placeByScreen
            ? (screenSlots != null ? screenSlots.Length : 0)
            : (slots != null ? slots.Length : 0);

        /// <summary>
        /// Where a slot is, in the world.
        /// <para>
        /// The world places this used to hold were picked by eye at one aspect ratio, and the
        /// camera shows a different slice of the world on every other one — so on a phone the
        /// fruit at x = 8.7 was simply past the right-hand edge. A fraction of the screen is the
        /// same fraction of every screen.
        /// </para>
        /// </summary>
        Vector3 HomeOf(int slot)
        {
            if (!placeByScreen) return new Vector3(slots[slot].x, slots[slot].y, depth);

            if (viewCamera == null) viewCamera = Camera.main;

            if (viewCamera == null)
            {
                Debug.LogWarning($"{nameof(MenuFruitShowcase)} has no camera, so it cannot place fruit by screen.", this);
                return new Vector3(0f, 0f, depth);
            }

            Vector2 place = screenSlots[slot];

            // The distance is measured from the camera, so the fruit lands on the plane the menu
            // was designed around however far back the camera happens to be.
            float distance = Mathf.Abs(depth - viewCamera.transform.position.z);

            Vector3 world = viewCamera.ViewportToWorldPoint(new Vector3(place.x, place.y, distance));

            return new Vector3(world.x, world.y, depth);
        }

        void Drift(Hanging entry)
        {
            float time = Time.time * entry.speed + entry.phase;

            entry.fruit.transform.position = entry.home + new Vector3(
                Mathf.Sin(time * 0.7f) * swayDistance,
                Mathf.Sin(time) * bobDistance,
                0f);
        }

        void Hang(int slot)
        {
            Fruit prefab = fruitPrefabs[Random.Range(0, fruitPrefabs.Length)];
            if (prefab == null) return;

            Vector3 home = HomeOf(slot);
            Fruit fruit = Instantiate(prefab, home, Random.rotation);

            // Nothing falls in the menu: no gravity, and no leftover speed from the prefab.
            if (fruit.TryGetComponent(out Rigidbody body))
            {
                body.useGravity = false;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }

            hanging.Add(new Hanging
            {
                fruit = fruit,
                slot = slot,
                home = home,
                phase = Random.Range(0f, Mathf.PI * 2f),
                speed = Random.Range(bobSpeed.x, bobSpeed.y) * Mathf.PI * 2f,
            });
        }

        IEnumerator HangAgainLater(int slot)
        {
            yield return new WaitForSeconds(replaceAfter);

            Hang(slot);
        }

#if UNITY_EDITOR
        /// <summary>
        /// Turns the old world places into screen places, using the camera as it is set up now.
        /// Run this once and the layout that was tuned by eye is kept — as a fraction of the
        /// screen this time, so it survives leaving this one aspect ratio.
        /// </summary>
        [ContextMenu("Convert World Slots To Screen Slots")]
        void ConvertWorldSlotsToScreenSlots()
        {
            Camera camera = viewCamera != null ? viewCamera : Camera.main;

            if (camera == null)
            {
                Debug.LogWarning("No camera to measure the places against.", this);
                return;
            }

            if (slots == null || slots.Length == 0)
            {
                Debug.LogWarning("There are no world places left to convert.", this);
                return;
            }

            var converted = new Vector2[slots.Length];

            for (int slot = 0; slot < slots.Length; slot++)
            {
                Vector3 viewport = camera.WorldToViewportPoint(new Vector3(slots[slot].x, slots[slot].y, depth));
                converted[slot] = new Vector2(viewport.x, viewport.y);
            }

            UnityEditor.Undo.RecordObject(this, "Convert Slots");

            screenSlots = converted;
            placeByScreen = true;

            UnityEditor.EditorUtility.SetDirty(this);

            Debug.Log($"Converted {converted.Length} place(s) to fractions of the screen.", this);
        }

        /// <summary>Shows where the fruit will hang, so the places can be moved by eye.</summary>
        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.8f);

            for (int slot = 0; slot < SlotCount; slot++) Gizmos.DrawWireSphere(HomeOf(slot), 0.6f);
        }
#endif
    }
}
