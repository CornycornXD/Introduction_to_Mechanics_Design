
using UnityEngine;

public class HeadCorrectionController : MonoBehaviour
{
    // ==============================
    // Configuration
    // ==============================

    [Header("Head Correction Configuration")]
    [SerializeField, Min(0.01f)]
    private float _maxHeadCorrection = 0.16f;

    [SerializeField, Min(0.005f)]
    private float _headSearchStep = 0.02f;


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

    private bool _headCorrected;


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
    // Head Correction Processing
    // ==============================

    public bool TryCalculateCorrection(
        Vector2 velocity,
        bool isGrounded,
        Vector2 positionOffset,
        float deltaTime,
        out Vector2 correction)
    {
        correction = Vector2.zero;

        // Reset the correction state when grounded.
        if (isGrounded)
        {
            _headCorrected = false;
            return false;
        }

        // Only allow correction while rising,
        // and only once per jump.
        if (_headCorrected || velocity.y <= 0f)
            return false;


        // Retrieve the player's bounds at its virtual position.
        Bounds bodyBounds =
            _physicsQueries.GetBodyBounds(positionOffset);

        float querySkin = _physicsQueries.QuerySkin;


        // Calculate the expected upward travel this physics step.
        float rise =
            velocity.y * deltaTime + querySkin;


        // Calculate three probe positions along the upper edge.
        Vector2 top = new Vector2(
            bodyBounds.center.x,
            bodyBounds.max.y - querySkin
        );

        float edge =
            bodyBounds.extents.x - 2f * querySkin;


        // Check the middle, left and right positions.
        bool middle = _physicsQueries.ProbeSolid(
            top,
            Vector2.up,
            rise,
            out _
        );

        bool left = _physicsQueries.ProbeSolid(
            top + Vector2.left * edge,
            Vector2.up,
            rise,
            out _
        );

        bool right = _physicsQueries.ProbeSolid(
            top + Vector2.right * edge,
            Vector2.up,
            rise,
            out _
        );


        // No correction is possible if:
        // 1. The middle is obstructed.
        // 2. Both sides have the same obstruction result.
        if (middle || left == right)
            return false;


        // Determine the direction with available clearance.
        float direction = left ? -1f : 1f;


        // Ensure the search step is never smaller than 0.005.
        float step = Mathf.Max(0.005f, _headSearchStep);

        // Search horizontally for a position with sufficient clearance.
        for (
            float distance = step;
            distance <= _maxHeadCorrection;
            distance += step)
        {
            Vector2 shift =
                Vector2.right * (direction * distance);

            // Check horizontal movement from the original virtual position.
            if (!_physicsQueries.IsSweepClear(
                positionOffset,
                shift))
            {
                continue;
            }

            // Check upward movement at the candidate position.
            if (!_physicsQueries.IsSweepClear(
                positionOffset + shift,
                Vector2.up * rise))
            {
                continue;
            }

            // Keep the latest successful correction.
            correction = shift;

            _headCorrected = true;

            // Continue searching for a potentially better correction.
        }

        return correction.sqrMagnitude > 0f;
    }


    // ==============================
    // State Reset
    // ==============================

    public void ResetHeadCorrection()
    {
        _headCorrected = false;
    }
}
