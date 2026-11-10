
using UnityEngine;

public class VariableJumpController : MonoBehaviour
{
    // ==============================
    // Configuration
    // ==============================

    [Header("Variable Jump Configuration")]
    [SerializeField, Range(0.1f, 1f)]
    private float _jumpCutMultiplier = 0.45f;


    // ==============================
    // Jump State
    // ==============================

    private bool _canCutJump;

    public float JumpCutMultiplier => _jumpCutMultiplier;


    // ==============================
    // Jump Initialization
    // ==============================

    public void BeginJump()
    {
        // A successful jump can be shortened
        // if the player releases the Jump button.
        _canCutJump = true;
    }


    // ==============================
    // Variable Jump Processing
    // ==============================

    public bool ShouldCutJump(
        bool isGrounded,
        float verticalVelocity,
        bool isJumpHeld)
    {
        // Jump cutting is no longer relevant
        // after landing or beginning to descend.
        if (isGrounded || verticalVelocity < 0f)
        {
            _canCutJump = false;
            return false;
        }

        // Only cut once, and only after Jump is released.
        if (!_canCutJump || isJumpHeld)
            return false;

        _canCutJump = false;

        return true;
    }


    // ==============================
    // State Reset
    // ==============================

    public void ResetJumpCut()
    {
        _canCutJump = false;
    }
}
