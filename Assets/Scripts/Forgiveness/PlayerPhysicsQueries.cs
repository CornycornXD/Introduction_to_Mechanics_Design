using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class PlayerPhysicsQueries : MonoBehaviour
{
    // ==============================
    // Configuration
    // ==============================

    [Header("Spatial Queries")]
    [SerializeField]
    private LayerMask _obstacleLayers = 1;

    [SerializeField, Min(0.001f)]
    private float _querySkin = 0.01f;


    // ==============================
    // Components and Physics
    // ==============================

    private BoxCollider2D _bodyCollider;

    private ContactFilter2D _solidFilter;

    private readonly RaycastHit2D[] _castResults =
        new RaycastHit2D[16];

    private readonly Collider2D[] _overlapResults =
        new Collider2D[16];


    // ==============================
    // Initialization
    // ==============================

    private void Awake()
    {
        _bodyCollider = GetComponent<BoxCollider2D>();

        _solidFilter = new ContactFilter2D
        {
            useTriggers = false
        };

        _solidFilter.SetLayerMask(_obstacleLayers);
    }


    // ==============================
    // Configuration Access
    // ==============================

    public float QuerySkin => _querySkin;


    // ==============================
    // Body Bounds
    // ==============================

    public Bounds GetBodyBounds(Vector2 positionOffset)
    {
        Bounds bounds = _bodyCollider.bounds;

        // Evaluate the collider at its virtual position.
        bounds.center += (Vector3)positionOffset;

        return bounds;
    }


    // ==============================
    // Sweep Collision Check
    // ==============================

    public bool IsSweepClear(
        Vector2 positionOffset,
        Vector2 delta)
    {
        Bounds bodyBounds = GetBodyBounds(positionOffset);

        Vector2 size =
            (Vector2)bodyBounds.size
            - Vector2.one * (2f * _querySkin);

        Vector2 center = bodyBounds.center;


        // Check whether the destination position is occupied.
        int overlaps = Physics2D.OverlapBox(
            center + delta,
            size,
            0f,
            _solidFilter,
            _overlapResults
        );

        // A full buffer means the query may be incomplete.
        // Treat it conservatively as blocked.
        if (overlaps == _overlapResults.Length)
            return false;

        for (int i = 0; i < overlaps; i++)
        {
            if (_overlapResults[i] != _bodyCollider)
                return false;
        }


        // No movement means the destination check is sufficient.
        if (delta.sqrMagnitude == 0f)
            return true;


        // Check the path between the starting and destination positions.
        int count = Physics2D.BoxCast(
            center,
            size,
            0f,
            delta.normalized,
            _solidFilter,
            _castResults,
            delta.magnitude
        );

        if (count == _castResults.Length)
            return false;

        for (int i = 0; i < count; i++)
        {
            if (_castResults[i].collider != _bodyCollider)
                return false;
        }

        return true;
    }


    // ==============================
    // Solid Obstacle Probe
    // ==============================

    public bool ProbeSolid(
        Vector2 origin,
        Vector2 direction,
        float distance,
        out RaycastHit2D hit)
    {
        int count = Physics2D.Raycast(
            origin,
            direction,
            _solidFilter,
            _castResults,
            distance
        );


        // A full buffer means the query may be incomplete.
        // Report a blocked result, but no specific hit.
        if (count == _castResults.Length)
        {
            hit = default;
            return true;
        }


        // Find the first obstacle that isn't the player's collider.
        for (int i = 0; i < count; i++)
        {
            if (_castResults[i].collider == _bodyCollider)
                continue;

            hit = _castResults[i];
            return true;
        }

        hit = default;
        return false;
    }
}