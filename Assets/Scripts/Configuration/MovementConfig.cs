
using UnityEngine;

[CreateAssetMenu(
    fileName = "MovementConfig",
    menuName = "Player/Movement Config"
)]
public class MovementConfig : ScriptableObject
{
    [Header("Movement")]
    [SerializeField, Min(0f)]
    private float _moveSpeed = 7f;

    [SerializeField, Min(0f)]
    private float _jumpSpeed = 12f;

    [SerializeField, Min(0f)]
    private float _gravity = 30f;

    [SerializeField, Min(0f)]
    private float _maxFallSpeed = 20f;

    [Header("Acceleration")]
    [SerializeField, Min(0f)]
    private float _groundAcceleration = 25f;

    [SerializeField, Min(0f)]
    private float _airAcceleration = 12f;


    // Read-only access for other systems

    public float MoveSpeed => _moveSpeed;
    public float JumpSpeed => _jumpSpeed;
    public float Gravity => _gravity;
    public float MaxFallSpeed => _maxFallSpeed;

    public float GroundAcceleration => _groundAcceleration;
    public float AirAcceleration => _airAcceleration;
}
