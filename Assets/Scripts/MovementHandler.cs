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
    //  Acceleration - Sticky Feet on Land
    [SerializeField, Min(0f)] private float _groundAcceleration = 25f;
    [SerializeField, Min(0f)] private float _airAcceleration = 12f;

    [Header("Ground Detection")]
    [SerializeField] private LayerMask _groundLayers = 1;
    [SerializeField, Range(0.1f, 1f)] private float _minGroundNormalY = 0.7f;
    private const float _risingSpeedThreshold = 0.1f;
    private Rigidbody2D _rb;
    private ContactFilter2D _groundFilter;
    private readonly List<ContactPoint2D> _contacts = new List<ContactPoint2D>();
    private float _horizontalInput;
    private bool _jumpPressedSinceLastStep;

    //  Used by 'Corner Clip on Jumps', 'Bumped Head Corrections', and 'Relaxed Semi-solids'
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
    private bool _jumpHeld; //  Shares with Variable Jump Strength
    private bool _downHeld;
    private bool _semiJumpActive;
    private bool _semiAssistUsed;
    private ContactFilter2D _semiFilter;
    private readonly Collider2D[] _semiCandidates = new Collider2D[16];
    private readonly List<Collider2D> _droppedPlatforms = new List<Collider2D>();

    //  Other individual mechanics
    [Header("Variable Jump Strength")]
    [SerializeField, Range(0.1f, 1f)] private float _jumpCutMultiplier = 0.45f;
    private bool _canCutJump;

    [Header("Sticky Feet on Land")]
    [SerializeField, Min(0f)] private float _landingBrake = 400f;
    private bool _hasGroundHistory;
    private bool _wasGrounded;

    [Header("Ledge Retention")]
    [SerializeField, Min(0.001f)] private float _supportSkin = 0.02f;
    [SerializeField, Min(0.01f)] private float _supportProbeDepth = 0.1f;
    private bool _safetyHeld;
    private readonly RaycastHit2D[] _supportHits = new RaycastHit2D[8];

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

        RestoreClearedPlatforms();

        double now = Time.fixedTimeAsDouble;

        if (freshPress) RecordJumpPress(now);

        bool isGrounded = CheckGrounded();

        UpdateCoyoteTime(now, isGrounded);

        //  Normal Horizontal Movement
        Vector2 velocity = _rb.linearVelocity;

        bool landedThisStep = _hasGroundHistory && isGrounded && !_wasGrounded;

        float acceleration = isGrounded ? _groundAcceleration : _airAcceleration;

        float speedMultiplier = GetApexSpeedMultiplier(isGrounded, velocity.y);
        velocity.x = Mathf.MoveTowards(velocity.x, _horizontalInput * _moveSpeed * speedMultiplier, acceleration * Time.fixedDeltaTime);

        float gravityMultiplier = GetApexGravityMultiplier(isGrounded, velocity.y);
        velocity.y = Mathf.Max(velocity.y - _gravity * gravityMultiplier * Time.fixedDeltaTime, -_maxFallSpeed);

        if (_downHeld && freshPress && isGrounded && TryBeginDropThrough()) {
            freshPress = false;
            isGrounded = false;
            velocity.y = Mathf.Min(velocity.y, -1f);
        }

        if (TryConsumeCoyoteJump(freshPress, isGrounded) || TryConsumeBufferedJump(now, isGrounded)) {
            velocity.y = _jumpSpeed;

            _apexJumpActive = true;

            _cornerCorrected = false;
            _headCorrected = false;

            _semiJumpActive = true;
            _semiAssistUsed = false;

            _canCutJump = true;

            isGrounded = false;
        }

        //  im a bit skeptical about this part
        if (isGrounded) ApplyLandingBrake(ref velocity, landedThisStep);
        _wasGrounded = isGrounded;
        _hasGroundHistory = true;
        //  ---

        ApplyCornerClip(velocity, isGrounded);

        ApplyHeadCorrection(velocity, isGrounded);

        ApplySemiSolidLift(velocity, isGrounded);

        ApplyJumpCut(ref velocity, isGrounded);

        RetainSupportedFoot(ref velocity, isGrounded);

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
        if (context.performed) {
            _jumpPressedSinceLastStep = true;
            _jumpHeld = true;
        }
        else if (context.canceled)
        {
            _jumpHeld = false;
        }
    }

    public void OnDown(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            _downHeld = true;
        } 
        else if (context.canceled) {
            _downHeld = false;        
        }
    }

    public void OnCrouch(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            _safetyHeld = true;
        }
        else if (context.canceled)
        {
            _safetyHeld = false;
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


    //  Used by 'Corner Clip on Jumps', 'Bumped Head Corrections', and 'Relaxed Semi-Solids'
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

    //  Relaxed Semi-Solids
    private void ApplySemiSolidLift(Vector2 velocity, bool isGrounded) {
        if (isGrounded) {
            _semiJumpActive = false;
            _semiAssistUsed = false;
            return;
        }
        if (velocity.y < 0f) _semiJumpActive = false;
        if (!_semiJumpActive || _semiAssistUsed || !_jumpHeld || velocity.y < 0 || velocity.y > _nearApexRiseSpeed || _droppedPlatforms.Count > 0) return;

        Bounds bodyBounds = _bodyCollider.bounds;
        Vector2 search = new Vector2(bodyBounds.center.x, bodyBounds.min.y + _maxSemLift * 0.5f);
        Vector2 size = new Vector2(bodyBounds.size.x * 0.8f, _maxSemLift + 2f * _querySkin);
        int count = Physics2D.OverlapBox(search, size, 0f, _semiFilter, _semiCandidates);
        if (count == _semiCandidates.Length) return;
        float bestLift = float.PositiveInfinity;
        for (int i = 0; i < count; i++) {
            Collider2D platform = _semiCandidates[i];
            PlatformEffector2D effector = platform.GetComponent<PlatformEffector2D>();
            if (!platform.usedByEffector || effector == null || !effector.useOneWay) continue;
            float lift = platform.bounds.max.y - bodyBounds.min.y + _querySkin;
            if (lift <= 0 || lift > _maxSemLift || lift >= bestLift) continue;
            if (!IsSweepClear(Vector2.zero, Vector2.up * lift)) continue;
            bestLift = lift;
        }
        if (float.IsPositiveInfinity(bestLift)) return;

        _rb.position += Vector2.up * bestLift;
        _semiAssistUsed = true;
    }

    private bool TryBeginDropThrough() {
        bool found = false;
        foreach (ContactPoint2D contact in _contacts) {
            if (contact.normal.y < _minGroundNormalY) continue;
            Collider2D platform = contact.collider == _bodyCollider ? contact.otherCollider : contact.collider;
            PlatformEffector2D effector = platform.GetComponent<PlatformEffector2D>();
            if (!platform.usedByEffector || effector == null || !effector.useOneWay) continue;
            if (!_droppedPlatforms.Contains(platform)) {
                _droppedPlatforms.Add(platform);
                Physics2D.IgnoreCollision(_bodyCollider, platform, true);
            }
            found = true;
        }
        return found;
    }

    private void RestoreClearedPlatforms() {
        Bounds bodyBounds = _bodyCollider.bounds;
        for (int i = _droppedPlatforms.Count - 1; i >= 0; i--) { 
            Collider2D platform = _droppedPlatforms[i];
            if (platform == null) {
                _droppedPlatforms.RemoveAt(i);
                continue;
            }

            Bounds platformBounds = platform.bounds;
            bool below = bodyBounds.max.y < platformBounds.min.y - _querySkin;
            bool above = bodyBounds.min.y > platformBounds.max.y + 2f * _querySkin;
            bool beside = bodyBounds.max.x < platformBounds.min.x || bodyBounds.min.x > platformBounds.max.x;
            if (!below && !above && !beside) continue;

            Physics2D.IgnoreCollision(_bodyCollider, platform, false);
            _droppedPlatforms.RemoveAt(i);
        }
    }

    private void RestoreAllPlatformCollisions() {
        if (_bodyCollider != null) foreach (Collider2D platform in _droppedPlatforms) if (platform != null) Physics2D.IgnoreCollision(_bodyCollider, platform, false);
        _droppedPlatforms.Clear();
    }
    //  ===

    //  Variable Jump Strength
    private void ApplyJumpCut(ref Vector2 velocity, bool isGrounded) {
        if (isGrounded || velocity.y < 0) {
            _canCutJump = false;
            return;
        }
        if (!_canCutJump || _jumpHeld) return;

        velocity.y *= _jumpCutMultiplier;
        _canCutJump = false;
    }
    //  ---

    //  Sticky Feet on Land
    private void ApplyLandingBrake(ref Vector2 velocity, bool landedThisStep) {
        if (!landedThisStep || Mathf.Abs(velocity.x) < 0.05f) return;
        bool wantsStop = Mathf.Abs(_horizontalInput) < 0.05f;
        bool wantsReverse = _horizontalInput * velocity.x < 0f;
        if (!wantsStop && !wantsReverse) return;

        velocity.x = Mathf.MoveTowards(velocity.x, 0f, _landingBrake * Time.fixedDeltaTime);
    }
    //  ---

    //  Hold Crouch to Stay on Ledge
    private void RetainSupportedFoot(ref Vector2 velocity, bool isGrounded) {
        if (!_safetyHeld || !isGrounded || velocity.y > 0f) return;

        Bounds bodyBounds = _bodyCollider.bounds;

        Vector2 foot = new Vector2(bodyBounds.center.x, bodyBounds.min.y);
        if (!HasFloorAt(foot)) return;

        Vector2 predicted = foot + Vector2.right * velocity.x * Time.fixedDeltaTime;
        if (!HasFloorAt(predicted)) velocity.x = 0f;
    }

    private bool HasFloorAt(Vector2 foot) {
        int count = Physics2D.Raycast(foot + Vector2.up * _supportSkin, Vector2.down, _groundFilter, _supportHits, _supportProbeDepth + _supportSkin);
        if (count == _supportHits.Length) return false;
        for (int i = 0; i < count; i++) if (_supportHits[i].collider != _bodyCollider && _supportHits[i].normal.y >= _minGroundNormalY) return true;
        return false;
    }
    //  ---

    //  Platform Collision
    private bool CheckGrounded() {
        if (_rb.linearVelocity.y > _risingSpeedThreshold) return false;

        _rb.GetContacts(_groundFilter, _contacts);

        foreach (ContactPoint2D contact in _contacts) {
            Collider2D surface = contact.collider == _bodyCollider ? contact.otherCollider : contact.collider;

            if (!contact.enabled || _droppedPlatforms.Contains(surface)) continue;

            if (contact.normal.y >= _minGroundNormalY) return true;
        }
        return false;
    }

    private bool HasCeilingContact()
    {
        foreach (ContactPoint2D contact in _contacts)
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

    private void OnEnable()
    {

    }

    private void OnDisable()
    {
        ClearInput();
    }

    private void ClearInput() {
        _apexJumpActive = false;

        _cornerCorrected = true;
        _headCorrected = true;

        RestoreAllPlatformCollisions();
        _semiJumpActive = false;
        _semiAssistUsed = false;

        _canCutJump = false;

        _wasGrounded = false;
        _hasGroundHistory = false;

        _horizontalInput = 0f;
        _jumpPressedSinceLastStep = false;

        _coyoteAvailable = false;
        _lastGroundedAt = 0d;

        _jumpHeld = false;
        _downHeld = false;
        _safetyHeld = false;

        _hasJumpRequest = false;
        _jumpPressedAt = 0d;
    }
}
