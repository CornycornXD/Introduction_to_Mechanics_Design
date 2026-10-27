using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GDEV50024.Week01
{
    /// <summary>
    /// 2D character movement with jump buffering and coyote time.
    /// Assign Move and Jump actions from the New Input System.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
    public class CharacterMovement : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField] private InputActionReference moveAction;
        [SerializeField] private InputActionReference jumpAction;

        [Header("Movement")]
        [SerializeField, Min(0f)] private float moveSpeed = 7f;
        [SerializeField, Min(0.1f)] private float jumpHeight = 2.5f;
        [SerializeField, Min(0.1f)] private float gravity = 30f;
        [SerializeField, Min(0.1f)] private float maxFallSpeed = 20f;

        [Header("Jump Timing")]
        [SerializeField, Min(0f)] private float jumpBufferTime = 0.15f;
        [SerializeField, Min(0f)] private float coyoteTime = 0.12f;

        [Header("Ground Detection")]
        [SerializeField] private LayerMask groundLayers = ~0;
        [SerializeField, Range(0.005f, 0.1f)] private float groundCheckDepth = 0.04f;

        private const float RestingSpeedTolerance = 0.05f;

        private Rigidbody2D body;
        private BoxCollider2D box;
        private ContactFilter2D groundFilter;
        private readonly List<Collider2D> overlaps = new List<Collider2D>();
        private float moveInput;
        private float jumpBufferRemaining;
        private float coyoteRemaining;

        private float JumpSpeed => Mathf.Sqrt(2f * gravity * jumpHeight);

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            box = GetComponent<BoxCollider2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;

            groundFilter = new ContactFilter2D { useTriggers = false };
            groundFilter.SetLayerMask(groundLayers);
        }

        private void OnEnable()
        {
            if (moveAction == null || jumpAction == null)
            {
                Debug.LogError("Assign Move and Jump actions on CharacterMovement.", this);
                enabled = false;
                return;
            }

            moveAction.action.Enable();
            jumpAction.action.Enable();
        }

        private void OnDisable()
        {
            if (moveAction != null) moveAction.action.Disable();
            if (jumpAction != null) jumpAction.action.Disable();
        }

        private void Update()
        {
            moveInput = moveAction.action.ReadValue<float>();
            if (jumpAction.action.WasPressedThisFrame())
                jumpBufferRemaining = jumpBufferTime;
            else
                jumpBufferRemaining = Mathf.Max(0f, jumpBufferRemaining - Time.deltaTime);
        }

        private void FixedUpdate()
        {
            bool grounded = IsGrounded();
            if (grounded)
                coyoteRemaining = coyoteTime;
            else
                coyoteRemaining = Mathf.Max(0f, coyoteRemaining - Time.fixedDeltaTime);

            Vector2 velocity = body.linearVelocity;
            velocity.x = moveInput * moveSpeed;
            velocity.y = Mathf.Max(velocity.y - gravity * Time.fixedDeltaTime, -maxFallSpeed);

            if (jumpBufferRemaining > 0f && coyoteRemaining > 0f)
            {
                velocity.y = JumpSpeed;
                jumpBufferRemaining = 0f;
                coyoteRemaining = 0f;
            }

            body.linearVelocity = velocity;
        }

        private bool IsGrounded()
        {
            if (body.linearVelocity.y > RestingSpeedTolerance)
                return false;

            Vector2 centre = body.position + box.offset;
            float feetY = centre.y - box.size.y * 0.5f;
            Vector2 stripCentre = new Vector2(centre.x, feetY - groundCheckDepth * 0.5f);
            Vector2 stripSize = new Vector2(box.size.x, groundCheckDepth);

            int count = Physics2D.OverlapBox(stripCentre, stripSize, 0f, groundFilter, overlaps);
            for (int i = 0; i < count; i++)
            {
                Collider2D other = overlaps[i];
                if (other.attachedRigidbody == body)
                    continue;

                if (other.bounds.max.y <= feetY + groundCheckDepth)
                    return true;
            }

            return false;
        }
    }
}
