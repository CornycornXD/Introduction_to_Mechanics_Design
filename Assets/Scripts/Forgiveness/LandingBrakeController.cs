
using UnityEngine;

public class LandingBrakeController : MonoBehaviour
{
    // ==============================
    // Configuration
    // ==============================

    [Header("Landing Brake Configuration")]
    [SerializeField, Min(0f)]
    private float _landingBrake = 400f;


    // ==============================
    // Constants
    // ==============================

    private const float _inputThreshold = 0.05f;
    private const float _velocityThreshold = 0.05f;


    // ==============================
    // Landing Brake Processing
    // ==============================

    public float CalculateHorizontalVelocity(
        float horizontalVelocity,
        float horizontalInput,
        bool landedThisStep,
        float deltaTime)
    {
        // Only apply braking during the landing step.
        if (!landedThisStep ||
            Mathf.Abs(horizontalVelocity) < _velocityThreshold)
        {
            return horizontalVelocity;
        }

        // Determine whether the player wants to stop
        // or reverse their current movement direction.
        bool wantsStop =
            Mathf.Abs(horizontalInput) < _inputThreshold;

        bool wantsReverse =
            horizontalInput * horizontalVelocity < 0f;

        if (!wantsStop && !wantsReverse)
            return horizontalVelocity;

        // Gradually reduce horizontal velocity toward zero.
        return Mathf.MoveTowards(
            horizontalVelocity,
            0f,
            _landingBrake * deltaTime
        );
    }
}
