using System;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

public class MovementHandler : MonoBehaviour
{
    private Rigidbody2D _rb;

    private float _moveInput;

    // player stats - scriptObj later
    [SerializeField] private float _jumpForce;
    [SerializeField] private float _speed;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
    }
    //private void OnEnable()
    //{
    //    InputHandler.JumpPerformed += Jump;
    //    InputHandler.MoveLeftPerformed += MoveLeft;
    //    InputHandler.MoveRightPerformed += MoveRight;
    //}

    //private void OnDisable()
    //{
    //    InputHandler.JumpPerformed -= Jump;
    //    InputHandler.MoveLeftPerformed -= MoveLeft;
    //    InputHandler.MoveRightPerformed -= MoveRight;
    //}
    void Start()
    {
        
    }


    // Update is called once per frame
    void FixedUpdate()
    {
        //  Normal Horizontal Movement
        _rb.linearVelocity = new Vector2 (_moveInput * _speed, _rb.linearVelocity.y);

        //  Jump Buffering

        
        //  Coyote Time


        //  Anti-Gravity Apex


        //  Speed Apex


        //  Sticky Feet on Land


        //  Corner Clip on Jumps


        //  Hold Crouch to Stay on Ledge


        //  Variable Jump Strength
    }

    public void OnJump(InputAction.CallbackContext context) {
        Debug.Log(context.phase);
        if (context.performed) {
            Debug.Log("Jump");
            _rb.AddForce(new Vector2(0, 100) * _jumpForce);
        }
    }

    public void OnMovement(InputAction.CallbackContext context) {
        _moveInput = context.ReadValue<float>();
    }
}
