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
        [Tooltip("One fruit hangs at each of these, chosen at random from the list above.")]
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

        void Start()
        {
            if (fruitPrefabs == null || fruitPrefabs.Length == 0)
            {
                Debug.LogWarning($"{nameof(MenuFruitShowcase)} has no fruit to show.", this);
                return;
            }

            for (int slot = 0; slot < slots.Length; slot++) Hang(slot);
        }

        void Update()
        {
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

            Vector3 home = new Vector3(slots[slot].x, slots[slot].y, depth);
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
        /// <summary>Shows where the fruit will hang, so the places can be moved by eye.</summary>
        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.8f);

            foreach (Vector2 slot in slots) Gizmos.DrawWireSphere(new Vector3(slot.x, slot.y, depth), 0.6f);
        }
#endif
    }
}
