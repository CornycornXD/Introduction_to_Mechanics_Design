
using UnityEngine;

public class MovementMotor : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField]
    private MovementConfig _movementConfig;

    private bool _isConfigured;
    public bool IsConfigured => _isConfigured;

    private void Awake()
    {
        if (_movementConfig == null)
        {
            Debug.LogError(
                "MovementConfig has not been assigned.",
                this
            );

            return;
        }

        _isConfigured = true;
    }

    public float JumpSpeed => _isConfigured ? _movementConfig.JumpSpeed : 0f;

    public Vector2 CalculateVelocity(
        Vector2 currentVelocity,
        float horizontalInput,
        bool isGrounded,
        float deltaTime,
        float speedMultiplier = 1f,
        float gravityMultiplier = 1f)
    {
        if (!_isConfigured)
            return currentVelocity;

        float acceleration = isGrounded
            ? _movementConfig.GroundAcceleration
            : _movementConfig.AirAcceleration;

        currentVelocity.x = Mathf.MoveTowards(
            currentVelocity.x,
            horizontalInput * _movementConfig.MoveSpeed * speedMultiplier,
            acceleration * deltaTime
        );

        currentVelocity.y = Mathf.Max(
            currentVelocity.y
                - _movementConfig.Gravity
                * gravityMultiplier
                * deltaTime,
            -_movementConfig.MaxFallSpeed
        );

        return currentVelocity;
    }
}
