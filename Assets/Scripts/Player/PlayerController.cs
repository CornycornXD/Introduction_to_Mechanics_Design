
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    // ==============================
    // Dependencies
    // ==============================

    [Header("Components")]
    [SerializeField] private PlayerInputReader _inputReader;
    [SerializeField] private MovementMotor _movementMotor;
    [SerializeField] private GroundDetector _groundDetector;
    [SerializeField] private JumpController _jumpController;
    [SerializeField] private ApexController _apexController;
    [SerializeField] private VariableJumpController _variableJumpController;
    [SerializeField] private LandingBrakeController _landingBrakeController;
    [SerializeField] private CornerClipController _cornerClipController;
    [SerializeField] private HeadCorrectionController _headCorrectionController;
    [SerializeField] private RelaxedSemiSolidController _relaxedSemiSolidController;
    [SerializeField] private DropThroughController _dropThroughController;
    [SerializeField] private LedgeRetentionController _ledgeRetentionController;


    // ==============================
    // Components and Physics
    // ==============================

    private Rigidbody2D _rb;

    private bool _hasValidDependencies;
    private bool _isConfigured;
    private bool _isSubscribed;


    // ==============================
    // Cached Input States
    // ==============================

    // Updated through PlayerInputReader events.
    // Physics calculations consume these cached values.
    private float _horizontalInput;
    private bool _downHeld;
    private bool _crouchHeld;


    // ==============================
    // Ground History
    // ==============================

    private bool _hasGroundHistory;
    private bool _wasGrounded;


    // ==============================
    // Initialization
    // ==============================

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();

        // Validate all required references before accessing dependencies.
        _hasValidDependencies =
            _inputReader != null &&
            _movementMotor != null &&
            _groundDetector != null &&
            _jumpController != null &&
            _apexController != null &&
            _variableJumpController != null &&
            _landingBrakeController != null &&
            _cornerClipController != null &&
            _headCorrectionController != null &&
            _relaxedSemiSolidController != null &&
            _dropThroughController != null &&
            _ledgeRetentionController != null;

        if (!_hasValidDependencies)
        {
            Debug.LogError(
                "PlayerController is missing one or more dependencies.",
                this
            );

            return;
        }

        // MovementMotor handles gravity manually.
        _rb.gravityScale = 0f;

        // Prevent rotation during physics simulation.
        _rb.freezeRotation = true;
    }


    private void Start()
    {
        // Stop initialization if required references are missing.
        if (!_hasValidDependencies)
            return;

        // Ensure dependent components have completed initialization.
        if (!_movementMotor.IsConfigured ||
            !_jumpController.IsConfigured ||
            !_cornerClipController.IsConfigured ||
            !_headCorrectionController.IsConfigured ||
            !_relaxedSemiSolidController.IsConfigured ||
            !_dropThroughController.IsConfigured ||
            !_ledgeRetentionController.IsConfigured)
        {
            Debug.LogError(
                "PlayerController initialization failed. " +
                "One or more dependent components are not configured.",
                this
            );

            return;
        }

        _isConfigured = true;

        // Start performs the initial subscription after all dependencies
        // have completed their Awake initialization.
        SubscribeToInput();
    }

    // ==============================
    // Input Subscription
    // ==============================

    private void OnEnable()
    {
        // On the first activation, Start handles subscription.
        // On subsequent activations, subscribe again after re-enabling.
        SubscribeToInput();
    }


    private void OnDisable()
    {
        UnsubscribeFromInput();

        // Clear cached input states.
        _horizontalInput = 0f;
        _downHeld = false;
        _crouchHeld = false;

        // Restore temporarily ignored platform collisions.
        if (_dropThroughController != null)
            _dropThroughController.RestoreAllPlatformCollisions();

        // Reset all movement mechanic states.
        ResetMechanicStates();

        // Prevent stale grounded-state transitions after reactivation.
        _hasGroundHistory = false;
        _wasGrounded = false;
    }


    private void SubscribeToInput()
    {
        if (!_isConfigured ||
            _inputReader == null ||
            _isSubscribed)
        {
            return;
        }

        // PlayerController owns all subscriptions to player input events.
        _inputReader.MoveChanged += OnMoveChanged;
        _inputReader.JumpPressed += OnJumpPressed;
        _inputReader.JumpReleased += OnJumpReleased;
        _inputReader.DownChanged += OnDownChanged;
        _inputReader.CrouchChanged += OnCrouchChanged;
        _inputReader.InputReset += OnInputReset;

        // Synchronize cached input states in case input was already received.
        _horizontalInput = _inputReader.HorizontalInput;
        _downHeld = _inputReader.DownHeld;
        _crouchHeld = _inputReader.CrouchHeld;

        // Synchronize the physical Jump state without creating
        // a new buffered jump request.
        _jumpController.SynchronizeJumpHeld(
            _inputReader.JumpHeld
        );

        _isSubscribed = true;
    }


    private void UnsubscribeFromInput()
    {
        if (!_isSubscribed || _inputReader == null)
            return;

        // Unsubscribe from every event registered above.
        _inputReader.MoveChanged -= OnMoveChanged;
        _inputReader.JumpPressed -= OnJumpPressed;
        _inputReader.JumpReleased -= OnJumpReleased;
        _inputReader.DownChanged -= OnDownChanged;
        _inputReader.CrouchChanged -= OnCrouchChanged;
        _inputReader.InputReset -= OnInputReset;

        _isSubscribed = false;
    }


    // ==============================
    // Mechanic State Reset
    // ==============================

    private void ResetMechanicStates()
    {
        // Clear jump buffering, coyote time and held-jump state.
        if (_jumpController != null)
            _jumpController.ResetJumpState();

        if (_apexController != null)
            _apexController.ResetApexState();

        if (_variableJumpController != null)
            _variableJumpController.ResetJumpCut();

        if (_cornerClipController != null)
            _cornerClipController.ResetCornerClip();

        if (_headCorrectionController != null)
            _headCorrectionController.ResetHeadCorrection();

        if (_relaxedSemiSolidController != null)
            _relaxedSemiSolidController.ResetSemiSolidState();
    }


    // ==============================
    // Input Event Handlers
    // ==============================

    private void OnMoveChanged(float input)
    {
        // Cache movement input instead of polling the input component.
        _horizontalInput = input;
    }


    private void OnJumpPressed()
    {
        // Forward the fresh input notification to the jump mechanic.
        _jumpController.RegisterJumpPress();
    }


    private void OnJumpReleased()
    {
        // Forward the release notification for variable jump handling.
        _jumpController.RegisterJumpRelease();
    }


    private void OnDownChanged(bool isHeld)
    {
        // Cache Down input for drop-through processing.
        _downHeld = isHeld;
    }


    private void OnCrouchChanged(bool isHeld)
    {
        // Cache crouch input for ledge-retention processing.
        _crouchHeld = isHeld;
    }


    private void OnInputReset()
    {
        // Clear cached input when the input reader loses focus or disables.
        _horizontalInput = 0f;
        _downHeld = false;
        _crouchHeld = false;

        // Clear pending jump requests and other mechanic states.
        ResetMechanicStates();

        // Restore ignored platforms when input is cleared.
        if (_dropThroughController != null)
            _dropThroughController.RestoreAllPlatformCollisions();

        // Prevent stale landing history.
        _hasGroundHistory = false;
        _wasGrounded = false;
    }


    // ==============================
    // Physics Update
    // ==============================

    private void FixedUpdate()
    {
        if (!_isConfigured)
            return;

        // Retrieve the current Rigidbody velocity.
        Vector2 velocity = _rb.linearVelocity;

        // Accumulate positional corrections.
        // PlayerController applies the final Rigidbody position only once.
        Vector2 positionOffset = Vector2.zero;


        // ------------------------------
        // 1. Restore Cleared Platforms
        // ------------------------------

        _dropThroughController.RestoreClearedPlatforms();


        // ------------------------------
        // 2. Refresh Ground State
        // ------------------------------

        _groundDetector.RefreshGroundState(velocity.y);

        bool isGrounded = _groundDetector.IsGrounded;


        // ------------------------------
        // 3. Detect Landing Transition
        // ------------------------------

        bool landedThisStep =
            _hasGroundHistory && isGrounded && !_wasGrounded;


        // ------------------------------
        // 4. Update Apex State
        // ------------------------------

        _apexController.UpdateApexState(
            isGrounded,
            _groundDetector.HasCeilingContact,
            velocity.y
        );


        // ------------------------------
        // 5. Calculate Normal Movement
        // ------------------------------

        velocity = _movementMotor.CalculateVelocity(
            velocity,
            _horizontalInput,
            isGrounded,
            Time.fixedDeltaTime,
            _apexController.SpeedMultiplier,
            _apexController.GravityMultiplier
        );


        // ------------------------------
        // 6. Process Drop-through or Normal Jump
        // ------------------------------

        bool wantsDropThrough =
            _downHeld &&
            _jumpController.HasPendingJumpPress &&
            isGrounded;

        if (wantsDropThrough &&
            _dropThroughController.TryBeginDropThrough())
        {
            // Discard the pending jump request.
            _jumpController.CancelJumpForDropThrough();

            // End any active relaxed semi-solid assistance.
            _relaxedSemiSolidController.ResetSemiSolidState();

            // The player is no longer grounded.
            isGrounded = false;

            // Begin downward movement.
            velocity.y = Mathf.Min(velocity.y, -1f);
        }
        else
        {
            // Process a normal jump if drop-through did not occur.
            bool shouldJump = _jumpController.ProcessJumpRequest(
                Time.fixedTimeAsDouble,
                isGrounded
            );

            if (shouldJump)
            {
                // Apply the configured jump velocity.
                velocity.y = _movementMotor.JumpSpeed;

                // Initialize mechanics associated with a new jump.
                _apexController.BeginJump();
                _variableJumpController.BeginJump();
                _relaxedSemiSolidController.BeginJump();

                // Reset spatial correction states for the new jump.
                _cornerClipController.ResetCornerClip();
                _headCorrectionController.ResetHeadCorrection();

                // The player is no longer grounded.
                isGrounded = false;
            }
        }


        // ------------------------------
        // 7. Apply Landing Brake
        // ------------------------------

        if (isGrounded)
        {
            velocity.x =
                _landingBrakeController.CalculateHorizontalVelocity(
                    velocity.x,
                    _horizontalInput,
                    landedThisStep,
                    Time.fixedDeltaTime
                );
        }


        // ------------------------------
        // 8. Update Ground History
        // ------------------------------

        _wasGrounded = isGrounded;
        _hasGroundHistory = true;


        // ------------------------------
        // 9. Apply Corner Clip
        // ------------------------------

        if (_cornerClipController.TryCalculateCorrection(
            velocity,
            _horizontalInput,
            isGrounded,
            positionOffset,
            Time.fixedDeltaTime,
            out Vector2 cornerCorrection))
        {
            positionOffset += cornerCorrection;
        }


        // ------------------------------
        // 10. Apply Head Correction
        // ------------------------------

        if (_headCorrectionController.TryCalculateCorrection(
            velocity,
            isGrounded,
            positionOffset,
            Time.fixedDeltaTime,
            out Vector2 headCorrection))
        {
            positionOffset += headCorrection;
        }


        // ------------------------------
        // 11. Apply Relaxed Semi-Solid Lift
        // ------------------------------

        if (_relaxedSemiSolidController.TryCalculateCorrection(
            velocity,
            isGrounded,
            _jumpController.IsJumpHeld,
            _dropThroughController.HasDroppedPlatforms,
            positionOffset,
            out Vector2 semiSolidCorrection))
        {
            positionOffset += semiSolidCorrection;
        }


        // ------------------------------
        // 12. Apply Variable Jump
        // ------------------------------

        bool shouldCutJump = _variableJumpController.ShouldCutJump(
            isGrounded,
            velocity.y,
            _jumpController.IsJumpHeld
        );

        if (shouldCutJump)
        {
            velocity.y *= _variableJumpController.JumpCutMultiplier;
        }


        // ------------------------------
        // 13. Apply Ledge Retention
        // ------------------------------

        if (_ledgeRetentionController.ShouldStopHorizontalMovement(
            velocity,
            isGrounded,
            _crouchHeld,
            Time.fixedDeltaTime,
            positionOffset))
        {
            // Stop horizontal movement before leaving supported ground.
            velocity.x = 0f;
        }


        // ------------------------------
        // 14. Apply Position Correction
        // ------------------------------

        if (positionOffset.sqrMagnitude > 0f)
        {
            _rb.position += positionOffset;
        }


        // ------------------------------
        // 15. Apply Final Velocity
        // ------------------------------

        _rb.linearVelocity = velocity;
    }
}
