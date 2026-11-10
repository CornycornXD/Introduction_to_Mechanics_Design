
using UnityEngine;

public class RelaxedSemiSolidController : MonoBehaviour
{
    [Header("Relaxed Semi-Solid Configuration")]
    [SerializeField] private LayerMask _semiSolidLayers;
    [SerializeField, Min(0.01f)] private float _maxSemLift = 0.12f;
    [SerializeField, Min(0.1f)] private float _nearApexRiseSpeed = 2f;

    [Header("Physics Queries")]
    [SerializeField] private PlayerPhysicsQueries _physicsQueries;

    private ContactFilter2D _semiFilter;
    private readonly Collider2D[] _semiCandidates = new Collider2D[16];

    private bool _isConfigured;
    public bool IsConfigured => _isConfigured;

    private bool _semiJumpActive;
    private bool _semiAssistUsed;

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

        _semiFilter = new ContactFilter2D
        {
            useTriggers = false
        };

        _semiFilter.SetLayerMask(_semiSolidLayers);

        _isConfigured = true;
    }

    public void BeginJump()
    {
        _semiJumpActive = true;
        _semiAssistUsed = false;
    }

    public bool TryCalculateCorrection(
        Vector2 velocity,
        bool isGrounded,
        bool isJumpHeld,
        bool hasDroppedPlatforms,
        Vector2 positionOffset,
        out Vector2 correction)
    {
        correction = Vector2.zero;

        if (!_isConfigured)
            return false;

        if (isGrounded)
        {
            _semiJumpActive = false;
            _semiAssistUsed = false;
            return false;
        }

        if (velocity.y < 0f)
            _semiJumpActive = false;

        if (!_semiJumpActive ||
            _semiAssistUsed ||
            !isJumpHeld ||
            velocity.y < 0f ||
            velocity.y > _nearApexRiseSpeed ||
            hasDroppedPlatforms)
        {
            return false;
        }

        Bounds bodyBounds =
            _physicsQueries.GetBodyBounds(positionOffset);

        float querySkin = _physicsQueries.QuerySkin;

        Vector2 search = new Vector2(
            bodyBounds.center.x,
            bodyBounds.min.y + _maxSemLift * 0.5f
        );

        Vector2 size = new Vector2(
            bodyBounds.size.x * 0.8f,
            _maxSemLift + 2f * querySkin
        );

        int count = Physics2D.OverlapBox(
            search,
            size,
            0f,
            _semiFilter,
            _semiCandidates
        );

        // Treat a full result buffer conservatively:
        // there may be additional candidates we have not checked.
        if (count == _semiCandidates.Length)
            return false;

        float bestLift = float.PositiveInfinity;

        for (int i = 0; i < count; i++)
        {
            Collider2D platform = _semiCandidates[i];

            if (platform == null)
                continue;

            PlatformEffector2D effector =
                platform.GetComponent<PlatformEffector2D>();

            if (!platform.usedByEffector ||
                effector == null ||
                !effector.useOneWay)
            {
                continue;
            }

            float lift =
                platform.bounds.max.y -
                bodyBounds.min.y +
                querySkin;

            if (lift <= 0f ||
                lift > _maxSemLift ||
                lift >= bestLift)
            {
                continue;
            }

            if (!_physicsQueries.IsSweepClear(
                positionOffset,
                Vector2.up * lift))
            {
                continue;
            }

            bestLift = lift;
        }

        if (float.IsPositiveInfinity(bestLift))
            return false;

        correction = Vector2.up * bestLift;
        _semiAssistUsed = true;

        return true;
    }

    public void ResetSemiSolidState()
    {
        _semiJumpActive = false;
        _semiAssistUsed = false;
    }

    private void OnDisable()
    {
        ResetSemiSolidState();
    }
}
