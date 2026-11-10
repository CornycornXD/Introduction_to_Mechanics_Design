
using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputReader : MonoBehaviour
{
    // ==============================
    // Events
    // ==============================

    public event Action<float> MoveChanged;
    public event Action JumpPressed;
    public event Action JumpReleased;
    public event Action<bool> DownChanged;
    public event Action<bool> CrouchChanged;
    public event Action InputReset;


    // ==============================
    // Input States
    // ==============================

    public float HorizontalInput { get; private set; }

    public bool JumpHeld { get; private set; }
    public bool DownHeld { get; private set; }
    public bool CrouchHeld { get; private set; }


    // ==============================
    // Movement Input
    // ==============================

    public void OnMovement(InputAction.CallbackContext context)
    {
        float input = context.canceled
            ? 0f
            : context.ReadValue<float>();

        if (Mathf.Approximately(HorizontalInput, input))
            return;

        HorizontalInput = input;

        MoveChanged?.Invoke(HorizontalInput);
    }


    // ==============================
    // Jump Input
    // ==============================

    public void OnJump(InputAction.CallbackContext context)
    {
        if (context.performed && !JumpHeld)
        {
            JumpHeld = true;
            JumpPressed?.Invoke();
        }
        else if (context.canceled && JumpHeld)
        {
            JumpHeld = false;
            JumpReleased?.Invoke();
        }
    }


    // ==============================
    // Down / Drop-through Input
    // ==============================

    public void OnDown(InputAction.CallbackContext context)
    {
        bool isHeld = context.ReadValue<float>() > 0f;

        if (DownHeld == isHeld)
            return;

        DownHeld = isHeld;

        DownChanged?.Invoke(DownHeld);
    }


    // ==============================
    // Crouch / Ledge Retention Input
    // ==============================

    public void OnCrouch(InputAction.CallbackContext context)
    {
        bool isHeld = context.ReadValue<float>() > 0f;

        if (CrouchHeld == isHeld)
            return;

        CrouchHeld = isHeld;

        CrouchChanged?.Invoke(CrouchHeld);
    }


    // ==============================
    // Input Reset
    // ==============================

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
            ClearInput();
    }

    private void OnDisable()
    {
        ClearInput();
    }

    private void ClearInput()
    {
        HorizontalInput = 0f;
        MoveChanged?.Invoke(0f);

        if (JumpHeld)
            JumpReleased?.Invoke();

        JumpHeld = false;

        DownHeld = false;
        DownChanged?.Invoke(false);

        CrouchHeld = false;
        CrouchChanged?.Invoke(false);

        // Notify subscribers to clear pending input requests.
        InputReset?.Invoke();
    }
}
