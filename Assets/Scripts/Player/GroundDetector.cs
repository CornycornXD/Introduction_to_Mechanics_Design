using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
public class GroundDetector : MonoBehaviour
{
    // ==============================
    // Configuration
    // ==============================

    [Header("Ground Detection")]
    [SerializeField]
    private LayerMask _groundLayers = 1;

    [SerializeField]
    private LayerMask _semiSolidLayers;

    [SerializeField, Range(0.1f, 1f)]
    private float _minGroundNormalY = 0.7f;

    private const float _risingSpeedThreshold = 0.1f;

    // ==============================
    // Components and Physics
    // ==============================

    private Rigidbody2D _rb;
    private BoxCollider2D _bodyCollider;

    private ContactFilter2D _groundFilter;

    private readonly List<ContactPoint2D> _contacts =
        new List<ContactPoint2D>();

    private readonly HashSet<Collider2D> _ignoredPlatforms =
        new HashSet<Collider2D>();

    // ==============================
    // Detection States
    // ==============================

    public bool IsGrounded { get; private set; }

    public bool HasCeilingContact { get; private set; }

    public bool HasIgnoredPlatforms => _ignoredPlatforms.Count > 0;

    public IReadOnlyList<ContactPoint2D> Contacts => _contacts;

    public float MinGroundNormalY => _minGroundNormalY;

    public ContactFilter2D GroundFilter => _groundFilter;

    // ==============================
    // Initialization
    // ==============================

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _bodyCollider = GetComponent<BoxCollider2D>();

        _groundFilter = new ContactFilter2D
        {
            useTriggers = false
        };

        _groundFilter.SetLayerMask(
            _groundLayers | _semiSolidLayers
        );
    }

    // ==============================
    // Ignored Platforms
    // ==============================

    public void SetIgnoredPlatform(Collider2D platform)
    {
        if (platform != null)
            _ignoredPlatforms.Add(platform);
    }

    public void RemoveIgnoredPlatform(Collider2D platform)
    {
        _ignoredPlatforms.Remove(platform);
    }

    public void RemoveDestroyedIgnoredPlatforms()
    {
        // Remove destroyed Unity collider references.
        // Unity's overloaded null comparison detects destroyed objects.
        _ignoredPlatforms.RemoveWhere(
            platform => platform == null
        );
    }

    // ==============================
    // Ground Detection
    // ==============================

    public void RefreshGroundState(float verticalVelocity)
    {
        // Refresh contacts before evaluating either state.
        _rb.GetContacts(_groundFilter, _contacts);

        IsGrounded = false;
        HasCeilingContact = false;

        foreach (ContactPoint2D contact in _contacts)
        {
            if (!contact.enabled)
                continue;

            Collider2D surface =
                contact.collider == _bodyCollider
                    ? contact.otherCollider
                    : contact.collider;

            if (surface == null)
                continue;

            // Ignore dropped platforms for ground detection.
            if (_ignoredPlatforms.Contains(surface))
                continue;

            // Check for ceiling contact.
            if (contact.normal.y <= -_minGroundNormalY)
            {
                HasCeilingContact = true;
            }

            // Ignore ground contact while rising.
            if (verticalVelocity > _risingSpeedThreshold)
                continue;

            if (contact.normal.y >= _minGroundNormalY)
            {
                IsGrounded = true;
            }
        }
    }
}