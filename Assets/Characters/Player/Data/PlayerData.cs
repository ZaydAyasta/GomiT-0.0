using UnityEngine;

[CreateAssetMenu(menuName = "Characters/Player/PlayerData")]
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

    [Tooltip("Probabilidad por idle (índice del Blend Tree)")]
    public float[] idleProbabilities = new float[] { 0f, 0.6f, 0.3f };

    [Tooltip("Cooldown mínimo por idle")]
    public float[] idleCooldownMin = new float[] { 0f, 0f, 0f };

    [Tooltip("Cooldown máximo por idle")]
    public float[] idleCooldownMax = new float[] { 0f, 0f, 0f };

    [Header("Other")]
    public float groundCheckRadius = 0.1f;
}
