
using UnityEngine;

public class JumpController : MonoBehaviour
{
    // ==============================
    // Configuration
    // ==============================

    [Header("Jump Configuration")]
    [SerializeField, Min(0f)]
    private double _coyoteDuration = 0.12d;

    [SerializeField, Min(0f)]
    private double _jumpBufferDuration = 0.12d;


    // ==============================
    // Initialization State
    // ==============================

    // Retained for PlayerController's initialization check.
    // This component has no external dependencies to validate.
    public bool IsConfigured { get; private set; }


    // ==============================
    // Jump States
    // ==============================

    // Represents the current physical Jump button state.
    public bool IsJumpHeld { get; private set; }

    // Indicates whether a fresh press was captured
    // during the current physics processing step.
    public bool IsJumpPressedThisStep { get; private set; }

    // Allows PlayerController to prioritize drop-through
    // before processing a normal jump request.
    public bool HasPendingJumpPress => _jumpPressedSinceLastStep;

    private bool _jumpPressedSinceLastStep;

    private bool _hasJumpRequest;
    private double _jumpPressedAt;

    private double _lastGroundedAt;
    private bool _coyoteAvailable;


    // ==============================
    // Initialization
    // ==============================

    private void Awake()
    {
        // This component has no external dependencies.
        // Mark it ready after its own initialization is complete.
        IsConfigured = true;
    }


    // ==============================
    // Input Notifications
    // ==============================

    // Registers a fresh Jump press received from PlayerInputReader.
    // The request is stored until the next physics processing step.
    public void RegisterJumpPress()
    {
        _jumpPressedSinceLastStep = true;
        IsJumpHeld = true;
    }


    // Registers the physical release of the Jump button.
    public void RegisterJumpRelease()
    {
        IsJumpHeld = false;
    }


    // Synchronizes the physical Jump button state when
    // PlayerController subscribes or becomes active again.
    //
    // This deliberately does not create a fresh jump request
    // or activate the jump buffer.
    public void SynchronizeJumpHeld(bool isHeld)
    {
        IsJumpHeld = isHeld;
    }


    // ==============================
    // Jump Processing
    // ==============================

    public bool ProcessJumpRequest(
        double currentTime,
        bool isGrounded)
    {
        if (!IsConfigured)
            return false;

        // 1. Capture input received since the previous physics step.
        CaptureJumpInput(currentTime);

        // 2. Update coyote-time eligibility.
        UpdateCoyoteTime(currentTime, isGrounded);

        // 3. Check whether a valid buffered request exists.
        if (!HasValidBufferedJump(currentTime))
            return false;

        // 4. Check whether the player can currently perform a jump.
        if (!CanPerformJump(isGrounded))
            return false;

        // 5. Consume the request to prevent duplicate jumps.
        ConsumeJumpRequest();

        return true;
    }


    // ==============================
    // Input Capture
    // ==============================

    private void CaptureJumpInput(double currentTime)
    {
        bool freshPress = _jumpPressedSinceLastStep;

        // Consume the event flag for this physics step.
        _jumpPressedSinceLastStep = false;

        IsJumpPressedThisStep = freshPress;

        // A new press starts or refreshes the jump buffer.
        if (freshPress)
            RecordJumpPress(currentTime);
    }


    // ==============================
    // Jump Buffering
    // ==============================

    private void RecordJumpPress(double currentTime)
    {
        _hasJumpRequest = true;
        _jumpPressedAt = currentTime;
    }


    private bool HasValidBufferedJump(double currentTime)
    {
        if (!_hasJumpRequest)
            return false;

        // Calculate how long the request has been waiting.
        double requestAge = currentTime - _jumpPressedAt;

        // Discard requests that have exceeded the buffer duration.
        if (requestAge > _jumpBufferDuration)
        {
            _hasJumpRequest = false;
            _jumpPressedAt = 0d;

            return false;
        }

        return true;
    }


    // ==============================
    // Coyote Time
    // ==============================

    private void UpdateCoyoteTime(
        double currentTime,
        bool isGrounded)
    {
        if (isGrounded)
        {
            // Refresh the last grounded timestamp.
            _lastGroundedAt = currentTime;

            // Restore coyote eligibility while grounded.
            _coyoteAvailable = true;
        }
        else if (currentTime > _lastGroundedAt + _coyoteDuration)
        {
            // Expire coyote eligibility after leaving the ground.
            _coyoteAvailable = false;
        }
    }


    private bool CanPerformJump(bool isGrounded)
    {
        // A buffered request can be consumed while grounded
        // or while the player is still within coyote time.
        return isGrounded || _coyoteAvailable;
    }


    // ==============================
    // Jump Request Consumption
    // ==============================

    private void ConsumeJumpRequest()
    {
        // Clear the buffered request after a successful jump.
        _hasJumpRequest = false;
        _jumpPressedAt = 0d;

        // Prevent another coyote jump from the same grounded state.
        _coyoteAvailable = false;
    }


    // ==============================
    // Drop-through Handling
    // ==============================

    public void CancelJumpForDropThrough()
    {
        // Discard the pending fresh press.
        _jumpPressedSinceLastStep = false;
        IsJumpPressedThisStep = false;

        // Discard the buffered jump request.
        _hasJumpRequest = false;
        _jumpPressedAt = 0d;

        // Prevent the same request from triggering a coyote jump.
        _coyoteAvailable = false;

        // Intentionally preserve IsJumpHeld.
        // The physical Jump button may still be held after drop-through.
    }


    // ==============================
    // State Reset
    // ==============================

    public void ResetJumpState()
    {
        IsJumpHeld = false;
        IsJumpPressedThisStep = false;

        _jumpPressedSinceLastStep = false;

        _hasJumpRequest = false;
        _jumpPressedAt = 0d;

        _coyoteAvailable = false;
        _lastGroundedAt = 0d;
    }
}
