using System.Collections.Generic;
using UnityEngine;

namespace NeonKatana
{
    /// <summary>
    /// The pieces that take a fruit's place the moment it is cut. They are thrown apart, tumble as
    /// they fall, and the whole prefab clears itself away once they are long gone.
    /// The juice left behind on screen is handled separately, by <see cref="JuiceSplash"/>.
    /// </summary>
    public class SlicedFruit : MonoBehaviour
    {
        /// <summary>What a piece is, and therefore which way it should fly.</summary>
        enum PieceRole
        {
            LeftHalf,
            RightHalf,
            Debris,
            Unlabelled,
        }

        [Header("Pieces")]
        [Tooltip("The chunks the fruit falls into. Leave empty to use every child that has a Rigidbody.")]
        [SerializeField] Rigidbody[] pieces;
        [Tooltip("How hard each half is pushed away from the cut.")]
        [SerializeField] Vector2 sidewaysSpeed = new Vector2(2.5f, 3.5f);
        [SerializeField] float upwardSpeed = 2f;

        [Header("Piece tags")]
        [Tooltip("Tag on the half that should fly left. Leave the tags off a prefab to fall back on measuring.")]
        [SerializeField] string leftHalfTag = "LeftHalf";
        [Tooltip("Tag on the half that should fly right.")]
        [SerializeField] string rightHalfTag = "RightHalf";
        [Tooltip("Tag on stems, leaves and other odds and ends, which drift instead of being thrown.")]
        [SerializeField] string debrisTag = "Leaf";
        [Tooltip("Share of a half's speed that debris gets, so a leaf flutters rather than shoots off.")]
        [SerializeField, Range(0f, 1f)] float debrisSpeedShare = 0.4f;

        [Header("Tumble")]
        [Tooltip("Degrees per second the pieces spin as they fall.")]
        [SerializeField] Vector2 tumbleSpeed = new Vector2(300f, 500f);
        [Tooltip("Degrees per second the pieces roll towards the camera.")]
        [SerializeField] float rollSpeed = 150f;

        [Header("Sound")]
        [SerializeField] AudioSource audioSource;
        [SerializeField] AudioClip[] sliceSounds;

        [Header("Clean-up")]
        [Tooltip("Seconds before the pieces are removed. Must outlast the juice splash fade.")]
        [SerializeField, Min(0.5f)] float lifetime = 5f;

        Vector3 tumblePerSecond;

        void Awake()
        {
            if (pieces == null || pieces.Length == 0) pieces = CollectPieces();
            if (audioSource == null) audioSource = GetComponent<AudioSource>();

            tumblePerSecond = new Vector3(
                Random.Range(tumbleSpeed.x, tumbleSpeed.y),
                Random.Range(tumbleSpeed.x, tumbleSpeed.y),
                rollSpeed);
        }

        void Start()
        {
            PlaySliceSound();
            ThrowPiecesApart();
            Destroy(gameObject, lifetime);
        }

        void Update()
        {
            foreach (Rigidbody piece in pieces)
                if (piece != null) piece.transform.Rotate(tumblePerSecond * Time.deltaTime, Space.Self);
        }

        void ThrowPiecesApart()
        {
            Vector3 middle = FindMiddleOfHalves();

            for (int index = 0; index < pieces.Length; index++)
            {
                Rigidbody piece = pieces[index];
                if (piece == null) continue;

                PieceRole role = RoleOf(piece);
                float speed = Random.Range(sidewaysSpeed.x, sidewaysSpeed.y);

                if (role == PieceRole.Debris) speed *= debrisSpeedShare;

                piece.linearVelocity = new Vector3(
                    DirectionFor(role, piece, index, middle) * speed,
                    upwardSpeed,
                    0f);
            }
        }

        float DirectionFor(PieceRole role, Rigidbody piece, int index, Vector3 middle)
        {
            switch (role)
            {
                case PieceRole.LeftHalf: return -1f;
                case PieceRole.RightHalf: return 1f;

                // Debris has no side of its own; sending it either way looks fine.
                case PieceRole.Debris: return Random.value < 0.5f ? -1f : 1f;
            }

            // Nothing was labelled, so fall back on where the piece actually sits.
            float offset = FindCentreOf(piece).x - middle.x;

            return Mathf.Abs(offset) < 0.0001f
                ? (index % 2 == 0 ? -1f : 1f)
                : Mathf.Sign(offset);
        }

        /// <summary>
        /// Reading <c>tag</c> is deliberate: <c>CompareTag</c> throws when the tag has not been
        /// created in the Tag Manager yet, which would break every prefab that is not tagged.
        /// </summary>
        PieceRole RoleOf(Rigidbody piece)
        {
            string pieceTag = piece.tag;

            if (pieceTag == leftHalfTag) return PieceRole.LeftHalf;
            if (pieceTag == rightHalfTag) return PieceRole.RightHalf;
            if (pieceTag == debrisTag) return PieceRole.Debris;

            return PieceRole.Unlabelled;
        }

        /// <summary>
        /// The middle of the two halves — not of every piece. A stem sitting off to one side would
        /// otherwise drag the middle across far enough that both halves land on the same side of it
        /// and fly off together.
        /// </summary>
        Vector3 FindMiddleOfHalves()
        {
            Vector3 total = Vector3.zero;
            int counted = 0;

            foreach (Rigidbody piece in pieces)
            {
                if (piece == null || RoleOf(piece) == PieceRole.Debris) continue;

                total += FindCentreOf(piece);
                counted++;
            }

            return counted == 0 ? transform.position : total / counted;
        }

        /// <summary>
        /// Where a piece actually appears on screen, which is not where its transform sits. An
        /// imported model leaves every sub-mesh on the shared local origin and bakes the offset
        /// into the mesh itself, so asking the transform which side of the cut a piece is on gives
        /// the same answer for all of them.
        /// </summary>
        static Vector3 FindCentreOf(Rigidbody piece)
        {
            Bounds bounds = default;
            bool started = false;

            foreach (Renderer renderer in piece.GetComponentsInChildren<Renderer>())
            {
                // The juice splash is a sprite laid over the cut; it says nothing about the piece.
                if (renderer is SpriteRenderer) continue;

                if (started) bounds.Encapsulate(renderer.bounds);
                else { bounds = renderer.bounds; started = true; }
            }

            return started ? bounds.center : piece.worldCenterOfMass;
        }

        void PlaySliceSound()
        {
            if (audioSource == null || sliceSounds == null || sliceSounds.Length == 0) return;

            audioSource.PlayOneShot(sliceSounds[Random.Range(0, sliceSounds.Length)]);
        }

        Rigidbody[] CollectPieces()
        {
            var found = new List<Rigidbody>();

            foreach (Rigidbody body in GetComponentsInChildren<Rigidbody>())
                if (body.transform != transform) found.Add(body);

            return found.ToArray();
        }
    }
}
