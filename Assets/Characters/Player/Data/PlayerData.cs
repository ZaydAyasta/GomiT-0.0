using UnityEngine;

[CreateAssetMenu(menuName = "Characters/Player/PlayerData")]
public class PlayerData : ScriptableObject
{
    //estos valores no sirven xd, se modifican en el inspector por si acaso
    [Header("Movement")]
    public float walkSpeed = 4f;
    public float runSpeed = 7f;
    public float acceleration = 20f;
    public float deceleration = 30f;
    public float jumpForce = 8f;

    [Header("Idle System")]
    public float idleMinDelay = 3f;
    public float idleMaxDelay = 8f;

    [Tooltip("Probabilidad por idle (índice del Blend Tree)")] //aca son 3 pero en el inspector pones los q quieras
    public float[] idleProbabilities = new float[] { 0f, 0.6f, 0.3f }; 

    [Header("Other")]
    public float groundCheckRadius = 0.1f;
}
