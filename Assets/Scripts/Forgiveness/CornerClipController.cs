
using UnityEngine;

public class CornerClipController : MonoBehaviour
{
    // ==============================
    // Configuration
    // ==============================

    [Header("Corner Clip Configuration")]
    [SerializeField, Min(0.01f)]
    private float _maxFootLift = 0.18f;

    // ==============================
    // Dependencies
    // ==============================

    [Header("Physics Queries")]
    [SerializeField]
    private PlayerPhysicsQueries _physicsQueries;

    private bool _isConfigured;
    public bool IsConfigured => _isConfigured;

    // ==============================
    // Correction State
    // ==============================

    private bool _cornerCorrected;

    // ==============================
    // Initialization
    // ==============================

    private void Awake()
    {
        if (_physicsQueries == null)
        {
            Debug.LogError(
                "PlayerPhysicsQueries has not been assigned.",
                this
            );

            return;
        }

        _isConfigured = true;
    }

    // ==============================
    // Corner Clip Processing
    // ==============================

    public bool TryCalculateCorrection(
        Vector2 velocity,
        float horizontalInput,
        bool isGrounded,
        Vector2 positionOffset,
        float deltaTime,
        out Vector2 correction)
    {
        correction = Vector2.zero;

        // Reset the correction state when grounded.
        if (isGrounded)
        {
            _cornerCorrected = false;
            return false;
        }

        // Only allow one correction during a jump.
        if (_cornerCorrected ||
            velocity.y <= 0f ||
            Mathf.Abs(horizontalInput) < 0.05f ||
            horizontalInput * velocity.x <= 0f)
        {
            return false;
        }


        // Retrieve the player's bounds at its virtual position.
        Bounds bodyBounds =
            _physicsQueries.GetBodyBounds(positionOffset);


        // Determine the direction of horizontal movement.
        float direction = Mathf.Sign(velocity.x);


        // Calculate the raycast starting position.
        float querySkin = _physicsQueries.QuerySkin;

        Vector2 side = new Vector2(
            bodyBounds.center.x +
                direction * (bodyBounds.extents.x - querySkin),

            bodyBounds.min.y + querySkin
        );


        // Calculate how far the player will travel horizontally.
        float travel =
            Mathf.Abs(velocity.x) * deltaTime +
            2f * querySkin;


        // Search for an obstacle in the movement direction.
        bool hasObstacle = _physicsQueries.ProbeSolid(
            side,
            Vector2.right * direction,
            travel,
            out RaycastHit2D hit
        );

        if (!hasObstacle || hit.collider == null)
            return false;


        // Calculate the upward correction needed to clear the obstacle.
        float lift =
            hit.collider.bounds.max.y -
            bodyBounds.min.y +
            2f * querySkin;


        // Reject invalid or excessive corrections.
        if (lift <= 0f || lift > _maxFootLift)
            return false;


        // Calculate the required upward and forward movements.
        Vector2 up = Vector2.up * lift;

        Vector2 forward =
            Vector2.right * (direction * travel);


        // Verify that both movements are collision-free.
        if (!_physicsQueries.IsSweepClear(positionOffset, up))
            return false;

        if (!_physicsQueries.IsSweepClear(positionOffset + up, forward))
            return false;


        // Return the correction instead of moving the Rigidbody.
        correction = up;

        _cornerCorrected = true;

        return true;
    }


    // ==============================
    // State Reset
    // ==============================

    public void ResetCornerClip()
    {
        _cornerCorrected = false;
    }
}
