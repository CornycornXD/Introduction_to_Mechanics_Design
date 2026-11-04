using System;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using Unity.VisualScripting;

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

    //  Used by 'Corner Clip on Jumps', 'Bumped Head Corrections'
    [Header("Spatial Queries")]
    [SerializeField] private LayerMask _obstacleLayers = 1;
    [SerializeField, Min(0.001f)] private float _querySkin = 0.01f;
    private BoxCollider2D _bodyCollider;
    private ContactFilter2D _solidFilter;
    private readonly RaycastHit2D[] _castResults = new RaycastHit2D[16];
    private readonly Collider2D[] _overlapResults = new Collider2D[16];

    //  Mechanics group 1
    [Header("Anti-Gravity Apex")]
    [SerializeField, Min(0.1f)] private float _apexSpeedThreshold = 2f;
    private bool _apexJumpActive;
    [SerializeField, Range(0.1f, 1f)] private float _apexGravityMultiplier = 0.35f;

    [Header("Speed Apex")]
    [SerializeField, Range(1f, 1.5f)] private float _apexSpeedMultiplier = 1.25f;


    //  Mechanics group 2
    [Header("Coyote Time")]
    [SerializeField, Min(0f)] private double _coyoteDuration = 0.12d;
    private double _lastGroundedAt;
    private bool _coyoteAvailable;

    [Header("Jump Buffering")]
    [SerializeField, Min(0f)] private double _jumpBufferDuration = 0.12d;
    private bool _hasJumpRequest;
    private double _jumpPressedAt;

    //  Mechanics group 3
    [Header("Corner Clip on Jumps")]
    [SerializeField, Min(0.01f)] private float _maxFootLift = 0.18f;
    private bool _cornerCorrected;

    [Header("Bumped Head Correction")]
    [SerializeField, Min(0.01f)] private float _maxHeadCorrection = 0.16f;
    [SerializeField, Min(0.005f)] private float _headSearchStep = 0.02f;
    private bool _headCorrected;

    [Header("Relaxed Semi-Solids")]
    [SerializeField] private LayerMask _semiSolidLayers;
    [SerializeField, Min(0.01f)] private float _maxSemLift = 0.12f;
    [SerializeField, Min(0.1f)] private float _nearApexRiseSpeed = 2f;
    private bool _jumpHeld;
    private bool _downHeld;
    private bool _semiJumpActive;
    private bool _semiAssistUsed;
    private ContactFilter2D _semiFilter;
    private readonly Collider2D[] _semiCandidates = new Collider2D[16];
    private readonly List<Collider2D> _droppedPlatforms = new List<Collider2D>();

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _rb.gravityScale = 0f;
        _rb.freezeRotation = true;
        _bodyCollider = GetComponent<BoxCollider2D>();
        _solidFilter = new ContactFilter2D { useTriggers = false };
        _solidFilter.SetLayerMask(_obstacleLayers);

        _semiFilter = new ContactFilter2D { useTriggers = false };
        _semiFilter.SetLayerMask(_semiSolidLayers);

        _groundFilter = new ContactFilter2D() { useTriggers = false };
        _groundFilter.SetLayerMask(_groundLayers | _semiSolidLayers);
    }

    void Start()
    {

    }


    // Update is called once per frame
    void FixedUpdate()
    {
        //  Takuh Inputuh
        bool freshPress = _jumpPressedSinceLastStep;
        _jumpPressedSinceLastStep = false;

        double now = Time.fixedTimeAsDouble;

        if (freshPress) RecordJumpPress(now);

        bool isGrounded = CheckGrounded();

        UpdateCoyoteTime(now, isGrounded);

        //  Normal Horizontal Movement
        Vector2 velocity = _rb.linearVelocity;
        float speedMultiplier = GetApexSpeedMultiplier(isGrounded, velocity.y);
        velocity.x = _horizontalInput * _moveSpeed * speedMultiplier;

        float gravityMultiplier = GetApexGravityMultiplier(isGrounded, velocity.y); 
        velocity.y = Mathf.Max(velocity.y - _gravity * gravityMultiplier * Time.fixedDeltaTime, -_maxFallSpeed);

        if (TryConsumeCoyoteJump(freshPress, isGrounded) || TryConsumeBufferedJump(now, isGrounded)) {
            velocity.y = _jumpSpeed;

            _apexJumpActive = true;

            _cornerCorrected = false;
            _headCorrected = false;

            isGrounded = false;
        }

        ApplyCornerClip(velocity, isGrounded);
        ApplyHeadCorrection(velocity, isGrounded);

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

    //  Apex Gravity
    private float GetApexGravityMultiplier(bool isGrounded, float verticalSpeed) {
        if (isGrounded || HasCeilingContact() || verticalSpeed < -_apexSpeedThreshold) {
            _apexJumpActive = false;
            return 1f;
        }
        if (!_apexJumpActive) return 1f;

        bool inApexBand = Mathf.Abs(verticalSpeed) <= _apexSpeedThreshold;
        return inApexBand ? _apexGravityMultiplier : 1f;
    }
    //  ---

    //  Apex Speed
    private float GetApexSpeedMultiplier(bool isGrounded, float verticalSpeed) {
        if (isGrounded || HasCeilingContact() || verticalSpeed < -_apexSpeedThreshold)
        {
            _apexJumpActive = false;
            return 1f;
        }
        if (!_apexJumpActive) return 1f;

        bool inApexBand = Mathf.Abs(verticalSpeed) <= _apexSpeedThreshold;
        return inApexBand ? _apexSpeedMultiplier : 1f;
    }
    //  ---

    //  Coyote time
    private void UpdateCoyoteTime(double now, bool isGrounded) {
        if (isGrounded) { 
            _lastGroundedAt = now;
            _coyoteAvailable = true;
        }
        else if (now > _lastGroundedAt + _coyoteDuration) {
            _coyoteAvailable = false;
        }
    }

    private bool TryConsumeCoyoteJump(bool freshPress, bool isGrounded) {
        if (!freshPress) return false;
        if (!isGrounded && !_coyoteAvailable) return false;

        _coyoteAvailable = false;
        return true;
    }
    //  ---

    //  Jump Buffering
    private void RecordJumpPress(double now)
    {
        _hasJumpRequest = true;
        _jumpPressedAt = now;
    }

    private bool TryConsumeBufferedJump(double now, bool isGrounded) {
        if (!_hasJumpRequest) return false;

        double requestAge = now - _jumpPressedAt;

        if (requestAge > _jumpBufferDuration) {
            _hasJumpRequest = false;
            return false;
        }

        if (!isGrounded) return false;

        _hasJumpRequest = false;
        return true;
    }
    //  ---

  
    //  Used by 'Corner Clip on Jumps', 'Bumped Head Corrections'
    private bool IsSweepClear(Vector2 offset, Vector2 delta) { 
        Bounds bodyBounds = _bodyCollider.bounds;
        
        Vector2 size = (Vector2)bodyBounds.size - Vector2.one * (2f * _querySkin);
        Vector2 center = (Vector2)bodyBounds.center + offset;

        int overlaps = Physics2D.OverlapBox(center + delta, size, 0f, _solidFilter, _overlapResults);
        if (overlaps == _overlapResults.Length) return false;
        for (int i = 0; i < overlaps; i++) if (_overlapResults[i] != _bodyCollider) return false;

        if (delta.sqrMagnitude == 0f) return true;

        int count = Physics2D.BoxCast(center, size, 0f, delta.normalized, _solidFilter, _castResults, delta.magnitude);
        if (count == _castResults.Length) return false;
        for (int i = 0; i < count; i++) if (_castResults[i].collider != _bodyCollider) return false;
        return true;
    }

    private bool ProbeSolid(Vector2 origin, Vector2 direction, float distance, out RaycastHit2D hit)
    {
        int count = Physics2D.Raycast(origin, direction, _solidFilter, _castResults, distance);
        if (count == _castResults.Length)
        {
            hit = default;
            return true;
        }
        for (int i = 0; i < count; i++)
        {
            if (_castResults[i].collider == _bodyCollider) continue;
            hit = _castResults[i];
            return true;
        }
        hit = default;
        return false;
    }
    //  ---

    //  Corner Clip on Jumps

    private void ApplyCornerClip(Vector2 velocity, bool isGrounded) {
        if (isGrounded) {
            _cornerCorrected = false;
            return;
        }
        if (_cornerCorrected || velocity.y <= 0 || Mathf.Abs(_horizontalInput) < 0.05f || _horizontalInput * velocity.x <= 0) return;

        Bounds bodyBounds = _bodyCollider.bounds;
        float direction = Mathf.Sign(velocity.x);
        Vector2 side = new Vector2(bodyBounds.center.x + direction * (bodyBounds.extents.x - _querySkin), bodyBounds.min.y + _querySkin);
        float travel = Mathf.Abs(velocity.x) * Time.fixedDeltaTime + 2f * _querySkin;
        if (!ProbeSolid(side, Vector2.right * direction, travel, out RaycastHit2D hit) || hit.collider == null) return;

        float lift = hit.collider.bounds.max.y - bodyBounds.min.y + 2f * _querySkin;
        if (lift <= 0 || lift > _maxFootLift) return;

        Vector2 up = Vector2.up * lift;
        Vector2 forward = Vector2.right * (direction * travel);
        if (!IsSweepClear(Vector2.zero, up) || !IsSweepClear(up, forward)) return;

        _rb.position += up;
        _cornerCorrected = true;
    }
    //  ---

    //  Bumped Head Correction
    private void ApplyHeadCorrection(Vector2 velocity, bool isGrounded) {
        if (isGrounded) {
            _headCorrected = false;
            return;
        }
        if (_headCorrected || velocity.y <= 0) return;

        Bounds bodyBounds = _bodyCollider.bounds;
        float rise = velocity.y * Time.fixedDeltaTime + _querySkin;
        Vector2 top = new Vector2(bodyBounds.center.x, bodyBounds.max.y - _querySkin);
        float edge = bodyBounds.extents.x - 2f * _querySkin;
        bool middle = ProbeSolid(top, Vector2.up, rise, out _);
        bool left = ProbeSolid(top + Vector2.left * edge, Vector2.up, rise, out _);
        bool right = ProbeSolid(top + Vector2.right * edge, Vector2.up, rise, out _);
        if (middle || left == right) return;

        float direction = left ? -1 : 1;
        float step = MathF.Max(0.005f, _headSearchStep);
        for (float distance = step; distance <= _maxHeadCorrection; distance += step) {
            Vector2 shift = Vector2.right * (direction * distance);
            if (!IsSweepClear(Vector2.zero, shift) || !IsSweepClear(shift, Vector2.up * rise)) continue;

            _rb.position += shift;
            _headCorrected = true;
        }
    }
    //  ---
    //  Platform Collision
    private bool CheckGrounded() {
        if (_rb.linearVelocity.y > _risingSpeedThreshold) return false;

        _rb.GetContacts(_groundFilter, contacts);

        foreach (ContactPoint2D contact in contacts) {
            if (contact.normal.y >= _minGroundNormalY) return true;
        }
        return false;
    }

    private bool HasCeilingContact()
    {
        foreach (ContactPoint2D contact in contacts)
        {
            if (contact.normal.y <= -_minGroundNormalY) return true;
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

        _cornerCorrected = true;
        _headCorrected = true;

        _horizontalInput = 0f;
        _jumpPressedSinceLastStep = false;

        _coyoteAvailable = false;
        _lastGroundedAt = 0d;

        _hasJumpRequest = false;
        _jumpPressedAt = 0d;
    }
}
