
//using UnityEngine;
//using UnityEngine.InputSystem;
//using System;

//public class InputHandler : MonoBehaviour
//{
//    private PlayerInput _input;

//    public static event Action JumpPerformed;
//    public static event Action MoveLeftPerformed;
//    public static event Action MoveRightPerformed;

//    private void Awake()
//    {
//        _input = new PlayerInput();
//    }

//    private void OnEnable()
//    {
//        _input.Enable();

//        _input.Player.Jump.performed += HandleJump;
//        _input.Player.MoveLeft.performed += HandleMoveLeft;
//        _input.Player.MoveRight.performed += HandleMoveRight;
//    }

//    private void OnDisable()
//    {
//        _input.Disable();

//        _input.Player.Jump.performed -= HandleJump;
//        _input.Player.MoveLeft.performed -= HandleMoveLeft;
//        _input.Player.MoveRight.performed -= HandleMoveRight;
//    }

//    // Start is called once before the first execution of Update after the MonoBehaviour is created
//    void Start()
//    {

//    }

//    // Update is called once per frame
//    void Update()
//    {

//    }

//    private void HandleJump(InputAction.CallbackContext context)
//    {
//        JumpPerformed?.Invoke();
//    }
//    private void HandleMoveLeft(InputAction.CallbackContext context)
//    {
//        MoveLeftPerformed?.Invoke();
//    }

//    private void HandleMoveRight(InputAction.CallbackContext context)
//    {
//        MoveRightPerformed?.Invoke();
//    }
//}
