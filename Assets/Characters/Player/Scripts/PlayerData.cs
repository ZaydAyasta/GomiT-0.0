using UnityEngine;

[CreateAssetMenu(menuName = "Player/PlayerData")]
public class PlayerData : ScriptableObject
{
    [Header("Movement")]
    public float walkSpeed = 4f;
    public float runSpeed = 7f;
    public float acceleration = 20f;
    public float deceleration = 30f;
    public float jumpForce = 8f;

    [Header("Idle System")]
    public float idleMinDelay = 3f;
    public float idleMaxDelay = 8f;

    public float[] idleProbabilities = new float[] { 1f, 0.6f };

    [Header("Other")]
    public float groundCheckRadius = 0.1f;
}