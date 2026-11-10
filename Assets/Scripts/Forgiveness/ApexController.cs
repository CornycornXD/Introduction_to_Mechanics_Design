
using UnityEngine;

public class ApexController : MonoBehaviour
{
    // ==============================
    // Configuration
    // ==============================

    [Header("Apex Configuration")]
    [SerializeField, Min(0.1f)]
    private float _apexSpeedThreshold = 2f;

    [SerializeField, Range(0.1f, 1f)]
    private float _apexGravityMultiplier = 0.35f;

    [SerializeField, Range(1f, 1.5f)]
    private float _apexSpeedMultiplier = 1.25f;


    // ==============================
    // Apex State
    // ==============================

    private bool _apexJumpActive;

    public float GravityMultiplier { get; private set; } = 1f;

    public float SpeedMultiplier { get; private set; } = 1f;


    // ==============================
    // Jump Initialization
    // ==============================

    public void BeginJump()
    {
        _apexJumpActive = true;

        GravityMultiplier = 1f;
        SpeedMultiplier = 1f;
    }


    // ==============================
    // Apex Processing
    // ==============================

    public void UpdateApexState(
        bool isGrounded,
        bool hasCeilingContact,
        float verticalSpeed)
    {
        // End the apex effect when landing,
        // hitting a ceiling, or descending beyond the threshold.
        if (isGrounded ||
            hasCeilingContact ||
            verticalSpeed < -_apexSpeedThreshold)
        {
            _apexJumpActive = false;
        }

        // Reset multipliers to their default values.
        GravityMultiplier = 1f;
        SpeedMultiplier = 1f;

        if (!_apexJumpActive)
            return;

        // Check whether the player is within the apex band.
        bool inApexBand =
            Mathf.Abs(verticalSpeed) <= _apexSpeedThreshold;

        if (!inApexBand)
            return;

        GravityMultiplier = _apexGravityMultiplier;
        SpeedMultiplier = _apexSpeedMultiplier;
    }


    // ==============================
    // State Reset
    // ==============================

    public void ResetApexState()
    {
        _apexJumpActive = false;

        GravityMultiplier = 1f;
        SpeedMultiplier = 1f;
    }
}
