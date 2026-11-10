
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
public class DropThroughController : MonoBehaviour
{
    // ==============================
    // Dependencies
    // ==============================

    [Header("Components")]
    [SerializeField]
    private GroundDetector _groundDetector;

    [SerializeField]
    private PlayerPhysicsQueries _physicsQueries;

    private BoxCollider2D _bodyCollider;

    private bool _isConfigured;
    public bool IsConfigured => _isConfigured;


    // ==============================
    // Dropped Platform Tracking
    // ==============================

    private readonly List<Collider2D> _droppedPlatforms =
        new List<Collider2D>();

    public bool HasDroppedPlatforms => _droppedPlatforms.Count > 0;


    // ==============================
    // Initialization
    // ==============================

    private void Awake()
    {
        _bodyCollider = GetComponent<BoxCollider2D>();

        // Validate dependencies before enabling drop-through processing.
        if (_groundDetector == null ||
            _physicsQueries == null)
        {
            Debug.LogError(
                "DropThroughController is missing one or more dependencies.",
                this
            );

            return;
        }

        _isConfigured = true;
    }


    // ==============================
    // Drop-through Processing
    // ==============================

    public bool TryBeginDropThrough()
    {
        if (!_isConfigured)
            return false;

        bool foundPlatform = false;

        // Use the latest contact snapshot provided by GroundDetector.
        IReadOnlyList<ContactPoint2D> contacts =
            _groundDetector.Contacts;

        foreach (ContactPoint2D contact in contacts)
        {
            if (!contact.enabled)
                continue;

            // Only consider contacts representing floor surfaces.
            if (contact.normal.y < _groundDetector.MinGroundNormalY)
                continue;

            // Identify the platform collider.
            Collider2D platform =
                contact.collider == _bodyCollider
                    ? contact.otherCollider
                    : contact.collider;

            if (platform == null)
                continue;

            // Only one-way platforms using a PlatformEffector2D
            // can be dropped through.
            PlatformEffector2D effector =
                platform.GetComponent<PlatformEffector2D>();

            if (!platform.usedByEffector ||
                effector == null ||
                !effector.useOneWay)
            {
                continue;
            }

            // Avoid registering the same platform more than once.
            if (!_droppedPlatforms.Contains(platform))
            {
                _droppedPlatforms.Add(platform);

                // Temporarily disable collision with this platform.
                Physics2D.IgnoreCollision(
                    _bodyCollider,
                    platform,
                    true
                );
            }

            // Keep GroundDetector from treating ignored platforms as ground.
            _groundDetector.SetIgnoredPlatform(platform);

            foundPlatform = true;
        }

        return foundPlatform;
    }


    // ==============================
    // Platform Restoration
    // ==============================

    public void RestoreClearedPlatforms()
    {
        if (!_isConfigured)
            return;

        Bounds bodyBounds = _bodyCollider.bounds;
        float querySkin = _physicsQueries.QuerySkin;

        // Iterate backwards so entries can safely be removed.
        for (int i = _droppedPlatforms.Count - 1; i >= 0; i--)
        {
            Collider2D platform = _droppedPlatforms[i];

            // Remove destroyed platform references from both tracking collections.
            if (platform == null)
            {
                // Remove the destroyed platform from local tracking.
                _droppedPlatforms.RemoveAt(i);

                // Remove any destroyed references remaining in GroundDetector.
                _groundDetector.RemoveDestroyedIgnoredPlatforms();

                continue;
            }

            Bounds platformBounds = platform.bounds;

            // Check whether the player has completely cleared
            // the platform vertically or horizontally.
            bool below =
                bodyBounds.max.y <
                platformBounds.min.y - querySkin;

            // Use the player's bottom boundary to detect when
            // the entire body has moved above the platform.
            bool above =
                bodyBounds.min.y >
                platformBounds.max.y + 2f * querySkin;

            bool beside =
                bodyBounds.max.x < platformBounds.min.x ||
                bodyBounds.min.x > platformBounds.max.x;

            // Keep collision ignored until the player has cleared the platform.
            if (!below && !above && !beside)
                continue;

            RestorePlatform(platform);
            _droppedPlatforms.RemoveAt(i);
        }
    }


    // ==============================
    // Individual Platform Restoration
    // ==============================

    private void RestorePlatform(Collider2D platform)
    {
        if (platform == null)
            return;

        // Re-enable physical collision with the platform.
        Physics2D.IgnoreCollision(
            _bodyCollider,
            platform,
            false
        );

        // Remove the platform from GroundDetector's ignored-platform collection.
        _groundDetector.RemoveIgnoredPlatform(platform);
    }


    // ==============================
    // Complete Collision Restoration
    // ==============================

    public void RestoreAllPlatformCollisions()
    {
        foreach (Collider2D platform in _droppedPlatforms)
        {
            // Always clean up GroundDetector, including destroyed collider references.
            _groundDetector?.RemoveIgnoredPlatform(platform);

            // Physics2D requires valid colliders to restore collision.
            if (platform == null || _bodyCollider == null)
                continue;

            Physics2D.IgnoreCollision(
                _bodyCollider,
                platform,
                false
            );
        }

        // Clear tracking after all restoration attempts have been processed.
        _droppedPlatforms.Clear();
    }


    // ==============================
    // Lifecycle Cleanup
    // ==============================

    private void OnDisable()
    {
        // Prevent ignored platform collisions from persisting after deactivation.
        RestoreAllPlatformCollisions();
    }
}
