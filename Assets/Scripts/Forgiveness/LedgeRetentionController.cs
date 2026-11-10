
using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class LedgeRetentionController : MonoBehaviour
{
    // ==============================
    // Configuration
    // ==============================

    [Header("Ledge Retention Configuration")]
    [SerializeField, Min(0.001f)]
    private float _supportSkin = 0.02f;

    [SerializeField, Min(0.01f)]
    private float _supportProbeDepth = 0.1f;


    // ==============================
    // Dependencies
    // ==============================

    [Header("Components")]
    [SerializeField]
    private GroundDetector _groundDetector;

    private BoxCollider2D _bodyCollider;

    private bool _isConfigured;
    public bool IsConfigured => _isConfigured;


    // ==============================
    // Physics Query Buffer
    // ==============================

    // Reuse the buffer to avoid allocating an array for each query.
    private readonly RaycastHit2D[] _supportHits =
        new RaycastHit2D[8];


    // ==============================
    // Initialization
    // ==============================

    private void Awake()
    {
        _bodyCollider = GetComponent<BoxCollider2D>();

        if (_groundDetector == null)
        {
            Debug.LogError(
                "GroundDetector has not been assigned.",
                this
            );

            return;
        }

        _isConfigured = true;
    }


    // ==============================
    // Ledge Retention Processing
    // ==============================

    public bool ShouldStopHorizontalMovement(
        Vector2 velocity,
        bool isGrounded,
        bool isCrouchHeld,
        float deltaTime,
        Vector2 positionOffset)
    {
        if (!_isConfigured)
            return false;

        // Only retain the ledge while crouching,
        // grounded, and not moving upward.
        if (!isCrouchHeld ||
            !isGrounded ||
            velocity.y > 0f)
        {
            return false;
        }


        // Evaluate the collider at its virtual position,
        // including corrections calculated earlier this step.
        Bounds bodyBounds = _bodyCollider.bounds;
        bodyBounds.center += (Vector3)positionOffset;


        // Determine the player's current foot position.
        Vector2 foot = new Vector2(
            bodyBounds.center.x,
            bodyBounds.min.y
        );


        // The player must currently have floor support.
        if (!HasFloorAt(foot))
            return false;


        // Predict the foot position after horizontal movement.
        Vector2 predictedFoot =
            foot + Vector2.right * velocity.x * deltaTime;


        // Stop horizontal movement if the predicted position
        // no longer has floor support.
        return !HasFloorAt(predictedFoot);
    }


    // ==============================
    // Floor Support Detection
    // ==============================

    private bool HasFloorAt(Vector2 foot)
    {
        // Cast downward from slightly above the player's foot.
        // Reuse GroundDetector's existing layer filter.
        int count = Physics2D.Raycast(
            foot + Vector2.up * _supportSkin,
            Vector2.down,
            _groundDetector.GroundFilter,
            _supportHits,
            _supportProbeDepth + _supportSkin
        );


        // Treat an incomplete query as unconfirmed support.
        if (count == _supportHits.Length)
            return false;


        // Find a valid floor surface.
        for (int i = 0; i < count; i++)
        {
            RaycastHit2D hit = _supportHits[i];

            // Ignore the player's own collider.
            if (hit.collider == _bodyCollider)
                continue;

            // Only upward-facing surfaces count as floor support.
            if (hit.normal.y >= _groundDetector.MinGroundNormalY)
                return true;
        }

        return false;
    }
}
