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

    [Header("Slingshot Launch")]
    [Tooltip("Distancia minima de estiramiento antes de impulsar al jugador al soltar el agarre.")]
    public float minSlingshotStretch = 0.25f;

    [Tooltip("Velocidad generada por cada unidad de estiramiento.")]
    public float slingshotVelocityPerUnit = 8f;

    [Tooltip("Velocidad minima aplicada cuando el estiramiento supera el minimo.")]
    public float minSlingshotLaunchSpeed = 3f;

    [Tooltip("Velocidad maxima que puede generar la resortera.")]
    public float maxSlingshotLaunchSpeed = 14f;

    [Tooltip("Tiempo durante el que el movimiento normal no pisa la velocidad del lanzamiento.")]
    public float slingshotControlLockTime = 0.2f;

    [Header("Idle Timers")]
    [Tooltip("Segundos en idle_normal hasta iniciar idle_transition")]
    public float idleToTransitionDelay = 15f;

    [Tooltip("Segundos que espera en idle_tieso antes de empezar el loop de idles")]
    public float transitionToLoopDelay = 5f;

    [Header("Idle Loop (specials)")]
    [Tooltip("Probabilidad relativa por idle especial. El índice debe coincidir con el orden del BlendTree.")]
    public float[] specialIdleProbabilities = new float[] { 0.8f, 0.1f };

    [Tooltip("Cooldown mínimo por special idle (mismo index)")]
    public float[] specialIdleCooldownMin = new float[] { 2f, 4f };

    [Tooltip("Cooldown máximo por special idle (mismo index)")]
    public float[] specialIdleCooldownMax = new float[] { 2.5f, 6f };

    [Header("Idle Loop – Attempt Logic")]
    [Tooltip("Cada cuántos segundos se intenta sacar un special o blink")]
    public float specialAttemptInterval = 1.2f;

    [Tooltip("Probabilidad de que un intento busque un special (si falla, se usa blink como filler)")]
    [Range(0f, 1f)]
    public float specialAttemptChance = 0.35f;

    [Tooltip("Si está activo, un idle especial (no blink) NO puede repetirse dos veces seguidas")]
    public bool requireDifferentNonBlink = true;

    [Header("Special tuning")]
    [Tooltip("Índice dentro de specialIdleProbabilities que corresponde al blink")]
    public int blinkSpecialIndex = 0;

    [Tooltip("Permite que el blink se repita aunque haya sido el último special")]
    public bool blinkCanRepeat = true;

    [Header("Other")]
    public float groundCheckRadius = 0.1f;
}
