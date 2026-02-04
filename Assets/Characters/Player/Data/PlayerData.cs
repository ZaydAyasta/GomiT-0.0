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

    [Header("Idle Timers")]
    [Tooltip("Segundos en idle_normal hasta iniciar idle_transition")]
    public float idleToTransitionDelay = 15f;

    [Tooltip("Segundos que espera en idle_tieso antes de empezar el loop de idles")]
    public float transitionToLoopDelay = 5f;

    [Header("Idle Loop (specials)")]
    [Tooltip("Probabilidad relativa por idle especial. El primer elemento corresponde a 'idle_blink' si así lo mapeas.")]
    public float[] specialIdleProbabilities = new float[] { 0.8f, 0.1f };

    [Tooltip("Cooldown mínimo por special idle (same indexing)")]
    public float[] specialIdleCooldownMin = new float[] { 2f, 4f };

    [Tooltip("Cooldown máximo por special idle (same indexing)")]
    public float[] specialIdleCooldownMax = new float[] { 2.5f, 6f };

    [Header("Other")]
    public float groundCheckRadius = 0.1f;

    [Header("Special tuning")]
    public int blinkSpecialIndex = 0;
    public bool blinkCanRepeat = true;
}
