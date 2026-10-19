using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GDEV50024.Week01
{
    /// <summary>
    /// Week 01 baseline: a small, strict 2D platformer controller.
    ///
    /// The complete rule set:
    ///   MOVE - While Left or Right is held, horizontal speed is Move Speed.
    ///          Releasing stops the character at once.
    ///   FALL - Gravity pulls down every physics step, up to Max Fall Speed.
    ///   JUMP - A Jump press is judged once, on the frame it arrives:
    ///            standing on the ground at that moment -> jump with a fixed strength;
    ///            anything else                         -> nothing happens, and the press is discarded.
    ///          Holding Jump never jumps again. Only a new press is a new request.
    ///
    /// "Standing on the ground" means: a solid surface directly under the feet,
    /// and the body is neither rising nor falling.
    ///
    /// Solid collisions (floors, walls, ceilings) are handled by Unity's 2D physics
    /// through the Rigidbody2D and BoxCollider2D on this GameObject.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
    public class StrictPlatformerController : MonoBehaviour
    {
        [Header("Input (New Input System)")]
        [SerializeField] InputActionReference moveAction;
        [SerializeField] InputActionReference jumpAction;

        [Header("Movement")]
        [Tooltip("Horizontal speed (units per second) while Left or Right is held.")]
        [SerializeField, Min(0f)] float moveSpeed = 7f;

        [Header("Jump")]
        [Tooltip("Peak height of every jump, in units (approximate: physics runs in small fixed steps).")]
        [SerializeField, Min(0.1f)] float jumpHeight = 2.5f;
        [Tooltip("Downward acceleration, in units per second per second.")]
        [SerializeField, Min(0.1f)] float gravity = 30f;
        [Tooltip("Fastest possible falling speed, in units per second.")]
        [SerializeField, Min(0.1f)] float maxFallSpeed = 20f;

        [Header("Ground Detection")]
        [Tooltip("Which layers count as solid ground.")]
        [SerializeField] LayerMask groundLayers = ~0;
        [Tooltip("How far below the feet to look for ground, in units. It only needs to cover " +
                 "the tiny gap the physics engine keeps between touching colliders.")]
        [SerializeField, Range(0.005f, 0.1f)] float groundCheckDepth = 0.04f;

        [Header("Visuals")]
        [Tooltip("Optional. Moved to the front side of the body to show which way the character faces.")]
        [SerializeField] Transform eye;

        [Header("Debug")]
        [Tooltip("Print every Jump press, and whether it was accepted, to the Console.")]
        [SerializeField] bool logJumpPresses;

        // Vertical speeds smaller than this count as "not moving up or down".
        const float RestingSpeedTolerance = 0.05f;

        Rigidbody2D body;
        BoxCollider2D box;
        ContactFilter2D groundFilter;
        readonly List<Collider2D> overlaps = new List<Collider2D>();

        float moveInput;
        int facing = 1;
        Vector2 eyeOffset;

        /// <summary>Upward speed given by every jump. Derived from Jump Height and Gravity.</summary>
        public float JumpSpeed => Mathf.Sqrt(2f * gravity * jumpHeight);

        /// <summary>How many Jump presses became jumps.</summary>
        public int JumpsPerformed { get; private set; }

        /// <summary>How many Jump presses were discarded because the character was not standing on the ground.</summary>
        public int JumpPressesIgnored { get; private set; }

        void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            box = GetComponent<BoxCollider2D>();

            // This script applies gravity itself (see FixedUpdate), so Unity's built-in gravity is off.
            body.gravityScale = 0f;
            body.freezeRotation = true;

            groundFilter = new ContactFilter2D { useTriggers = false };
            groundFilter.SetLayerMask(groundLayers);

            if (eye != null)
                eyeOffset = new Vector2(Mathf.Abs(eye.localPosition.x), eye.localPosition.y);
        }

        void OnEnable()
        {
            if (moveAction == null || jumpAction == null)
            {
                Debug.LogError("Assign the Move and Jump actions on the StrictPlatformerController.", this);
                enabled = false;
                return;
            }
            moveAction.action.Enable();
            jumpAction.action.Enable();
        }

        void OnDisable()
        {
            if (moveAction != null) moveAction.action.Disable();
            if (jumpAction != null) jumpAction.action.Disable();
        }

        // Update runs once per rendered frame. All input is read here.
        void Update()
        {
            moveInput = moveAction.action.ReadValue<float>();

            // WasPressedThisFrame is true only on the frame the key goes down,
            // so holding Jump can never produce a second jump.
            if (jumpAction.action.WasPressedThisFrame())
                OnJumpPressed();

            if (moveInput > 0f) facing = 1;
            else if (moveInput < 0f) facing = -1;

            if (eye != null)
                eye.localPosition = new Vector3(eyeOffset.x * facing, eyeOffset.y, 0f);
        }

        void OnJumpPressed()
        {
            // The press is judged right now, against the current physics state.
            //
            // A common pattern is to set "jumpRequested = true" here and let FixedUpdate use it
            // later. But if the character lands in between, that stored flag quietly becomes a
            // jump buffer. Here the decision is made immediately, and only its result
            // (a new upward speed) is handed to the physics engine. Nothing is kept for later.
            if (IsGrounded())
            {
                Vector2 velocity = body.linearVelocity;
                velocity.y = JumpSpeed;
                body.linearVelocity = velocity;

                JumpsPerformed++;
                if (logJumpPresses) Debug.Log("Jump press: accepted (standing on the ground).", this);
            }
            else
            {
                JumpPressesIgnored++;
                if (logJumpPresses) Debug.Log("Jump press: ignored (not standing on the ground).", this);
            }
        }

        // FixedUpdate runs once per physics step (50 times per second by default).
        void FixedUpdate()
        {
            // Start from the velocity the physics engine left us with. If we just hit a
            // floor, wall or ceiling, the engine has already removed that part of our speed,
            // so we never keep pushing into a ceiling after bumping it.
            Vector2 velocity = body.linearVelocity;

            velocity.x = moveInput * moveSpeed;                   // no acceleration, no sliding
            velocity.y -= gravity * Time.fixedDeltaTime;          // gravity, every step
            velocity.y = Mathf.Max(velocity.y, -maxFallSpeed);    // limit falling speed

            body.linearVelocity = velocity;
        }

        /// <summary>
        /// True when the character is standing on the ground right now.
        /// </summary>
        public bool IsGrounded()
        {
            // 1. Rising (for example, just after a jump) or falling (not landed yet) is never standing.
            if (Mathf.Abs(body.linearVelocity.y) > RestingSpeedTolerance)
                return false;

            // 2. Look for solid colliders in a thin strip just below the feet.
            Vector2 centre = body.position + box.offset;
            float feetY = centre.y - box.size.y * 0.5f;
            Vector2 stripCentre = new Vector2(centre.x, feetY - groundCheckDepth * 0.5f);
            Vector2 stripSize = new Vector2(box.size.x, groundCheckDepth);

            int count = Physics2D.OverlapBox(stripCentre, stripSize, 0f, groundFilter, overlaps);
            for (int i = 0; i < count; i++)
            {
                Collider2D other = overlaps[i];
                if (other.attachedRigidbody == body)
                    continue; // that is our own collider

                // 3. It only counts as ground if its top surface is under our feet.
                //    A wall beside us reaches higher than our feet, so it never counts.
                if (other.bounds.max.y <= feetY + groundCheckDepth)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Moves the character to a position and clears its motion. Used by the Restart control.
        /// </summary>
        public void ResetTo(Vector2 position)
        {
            body.position = position;
            transform.position = position;
            body.linearVelocity = Vector2.zero;
            facing = 1;
        }

        // Shows the ground-check strip in the Scene view when the character is selected.
        void OnDrawGizmosSelected()
        {
            BoxCollider2D collider = GetComponent<BoxCollider2D>();
            if (collider == null) return;

            Vector2 centre = (Vector2)transform.position + collider.offset;
            float feetY = centre.y - collider.size.y * 0.5f;
            bool grounded = Application.isPlaying && body != null && IsGrounded();

            Gizmos.color = grounded ? Color.green : Color.yellow;
            Gizmos.DrawWireCube(new Vector3(centre.x, feetY - groundCheckDepth * 0.5f, 0f),
                                new Vector3(collider.size.x, groundCheckDepth, 0f));
        }
    }
}
