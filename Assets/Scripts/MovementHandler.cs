using System;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
public class MovementHandler : MonoBehaviour
{


    [Header("Movement")]
    [SerializeField, Min(0f)] private float _moveSpeed = 7f;
    [SerializeField, Min(0f)] private float _jumpSpeed = 12f;
    [SerializeField, Min(0f)] private float _gravity = 30f;
    [SerializeField, Min(0f)] private float _maxFallSpeed = 20f;

    [Header("Ground Detection")]
    [SerializeField] private LayerMask _groundLayers = 1;
    [SerializeField, Range(0.1f, 1f)] private float _minGroundNormalY = 0.7f;
    private const float _risingSpeedThreshold = 0.1f;
    private Rigidbody2D _rb;
    private ContactFilter2D _groundFilter;
    private readonly List<ContactPoint2D> contacts = new List<ContactPoint2D>();
    private float _horizontalInput;
    private bool _jumpPressedSinceLastStep;

    [Header("Anti-Gravity Apex")]
    [SerializeField, Min(0.1f)] private float _apexSpeedThreshold = 2f;
    [SerializeField, Range(0.1f, 1f)] private float _apexGravityMultiplier = 0.35f;
    private bool _apexJumpActive;

    [Header("Speed Apex")]
    //  [SerializeField, Min(0.1f)] private float _apexSpeedThreshold = 2f;
    [SerializeField, Range(1f, 1.5f)] private float _apexSpeedMultiplier = 1.25f;


    //  private bool _apexJumpActive

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _rb.gravityScale = 0f;
        _rb.freezeRotation = true;

        _groundFilter = new ContactFilter2D() { useTriggers = false };
        _groundFilter.SetLayerMask(_groundLayers);
    }
    //private void OnEnable()
    //{
    //    InputHandler.JumpPerformed += Jump;
    //    InputHandler.MoveLeftPerformed += MoveLeft;
    //    InputHandler.MoveRightPerformed += MoveRight;
    //}

    //private void OnDisable()
    //{
    //    InputHandler.JumpPerformed -= Jump;
    //    InputHandler.MoveLeftPerformed -= MoveLeft;
    //    InputHandler.MoveRightPerformed -= MoveRight;
    //}
    void Start()
    {

    }


    // Update is called once per frame
    void FixedUpdate()
    {
        //  Takuh Inputuh
        bool freshPress = _jumpPressedSinceLastStep;
        _jumpPressedSinceLastStep = false;

        bool isGrounded = CheckGrounded();

        //  Normal Horizontal Movement
        Vector2 velocity = _rb.linearVelocity;
        velocity.x = _horizontalInput * _moveSpeed;

        float gravityMultiplier = GetApexGravityMultiplier(isGrounded, velocity.y); 
        velocity.y = Mathf.Max(velocity.y - _gravity * gravityMultiplier * Time.fixedDeltaTime, -_maxFallSpeed);

        if (freshPress && isGrounded) {
            velocity.y = _jumpSpeed;
            _apexJumpActive = true;
            isGrounded = false;
        }

        _rb.linearVelocity = velocity;
        //  Jump Buffering


        //  Coyote Time


        //  Anti-Gravity Apex


        //  Speed Apex


        //  Sticky Feet on Land


        //  Corner Clip on Jumps


        //  Hold Crouch to Stay on Ledge


        //  Variable Jump Strength
    }

    //  Basic movement
    //  ---
    public void OnJump(InputAction.CallbackContext context) {
        Debug.Log(context.phase);
        if (context.performed) {
            Debug.Log("Jump");
            _jumpPressedSinceLastStep = true;
        }
    }

    public void OnMovement(InputAction.CallbackContext context) {
        _horizontalInput = context.ReadValue<float>();
    }
    //  ---

    private float GetApexGravityMultiplier(bool isGrounded, float verticalSpeed) {
        if (isGrounded || HasCeilingContact() || verticalSpeed < -_apexSpeedThreshold) {
            _apexJumpActive = false;
            return 1f;
        }
        if (!_apexJumpActive) return 1f;

        bool inApexBand = Mathf.Abs(verticalSpeed) <= _apexSpeedThreshold;
        return inApexBand ? _apexGravityMultiplier : 1f;
    }

    //  Platform collision
    //  ---
    private bool HasCeilingContact() {
        foreach (ContactPoint2D contact in contacts) {
            if (contact.normal.y <= -_minGroundNormalY) return true;
        }
        return false;
    }

    private bool CheckGrounded() {
        if (_rb.linearVelocity.y > _risingSpeedThreshold) return false;

        _rb.GetContacts(_groundFilter, contacts);

        foreach (ContactPoint2D contact in contacts) {
            if (contact.normal.y >= _minGroundNormalY) return true;
        }
        return false;
    }
    //  ---

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus) ClearInput();
    }

    private void ClearInput() {
        _apexJumpActive = false;
        _horizontalInput = 0f;
        _jumpPressedSinceLastStep = false;
    }
}
