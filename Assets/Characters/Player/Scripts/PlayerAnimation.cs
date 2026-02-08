using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Animator))]
public class PlayerAnimation : MonoBehaviour
{
    [Header("Data")]
    public PlayerData playerData;

    [Header("Idle detection")]
    public float idleSpeedThreshold = 0.1f;

    [Header("Animator mapping")]
    [Tooltip("IdleIndex donde empiezan los special idles (blend tree index base)")]
    public int specialBaseAnimatorIndex = 3;

    [Header("Debug")]
    public bool debugLogs = false;

    private Animator animator;
    private Coroutine idleCoroutine;

    private float timeInIdleNormal;
    private bool inTiesoLoop;

    private float[] specialNextAllowedTime;
    private int lastSpecialIndex = -1;

    private readonly int speedHash = Animator.StringToHash("Speed");
    private readonly int idleIndexHash = Animator.StringToHash("IdleIndex");

    private void Awake()
    {
        animator = GetComponent<Animator>();

        if (playerData == null)
        {
            Debug.LogError("[PlayerAnimation] PlayerData not assigned.");
            enabled = false;
            return;
        }

        int n = playerData.specialIdleProbabilities?.Length ?? 0;
        specialNextAllowedTime = new float[n];
        for (int i = 0; i < n; i++)
            specialNextAllowedTime[i] = 0f;
    }

    public void SetSpeed(float speed, bool grounded)
    {
        animator.SetFloat(speedHash, speed);

        // cualquier movimiento o salto rompe el idle
        if (!grounded || Mathf.Abs(speed) > idleSpeedThreshold)
        {
            ExitToIdleNormal();
            return;
        }

        timeInIdleNormal += Time.deltaTime;

        if (idleCoroutine == null)
            idleCoroutine = StartCoroutine(IdleStateMachine());
    }

    private void ExitToIdleNormal()
    {
        timeInIdleNormal = 0f;
        inTiesoLoop = false;
        lastSpecialIndex = -1;

        if (idleCoroutine != null)
        {
            StopCoroutine(idleCoroutine);
            idleCoroutine = null;
        }

        animator.SetFloat(idleIndexHash, 0f);
    }

    private IEnumerator IdleStateMachine()
    {
        while (true)
        {
            float speed = animator.GetFloat(speedHash);
            if (Mathf.Abs(speed) > idleSpeedThreshold)
            {
                ExitToIdleNormal();
                yield break;
            }

            // idle_normal -> transition -> tieso
            if (!inTiesoLoop)
            {
                animator.SetFloat(idleIndexHash, 0f);

                if (timeInIdleNormal < playerData.idleToTransitionDelay)
                {
                    yield return null;
                    continue;
                }

                animator.SetFloat(idleIndexHash, 1f);
                if (debugLogs) Debug.Log("[Idle] idle_transition");
                yield return WaitForCurrentState();

                animator.SetFloat(idleIndexHash, 2f);
                if (debugLogs) Debug.Log("[Idle] idle_tieso");
                yield return new WaitForSeconds(playerData.transitionToLoopDelay);

                SeedInitialSpecialCooldowns();

                inTiesoLoop = true;
                lastSpecialIndex = -1;
                continue;
            }

            // FASE 2: loop de idles especiales (tieso -> blink/other -> tieso -> ...)
            animator.SetFloat(idleIndexHash, 2f); // tieso

            // Espera el intervalo entre intentos de special (esto controla ritmo de parpadeos)
            float interval = Mathf.Max(0.05f, playerData.specialAttemptInterval);
            yield return new WaitForSeconds(interval);

            // Decide si intentamos un special o hacemos un blink de "relleno"
            bool trySpecial = Random.value <= Mathf.Clamp01(playerData.specialAttemptChance);

            if (!trySpecial)
            {
                // no intentamos special: reproducir blink si está disponible (es el filler por defecto)
                int blinkIdx = playerData.blinkSpecialIndex;
                if (IsSpecialAvailable(blinkIdx))
                {
                    yield return PlaySpecialAndWait(blinkIdx);
                    continue;
                }
                else
                {
                    // blink no disponible: simplemente volvemos a loop tieso y esperamos el siguiente intervalo
                    if (debugLogs) Debug.Log("[Idle] Blink no disponible, manteniendo tieso hasta siguiente intento");
                    continue;
                }
            }

            int chosen = ChooseSpecialIndex();

            if (chosen == -1)
            {
                // Ningún special libre por cooldown o peso -> fallback a blink si puede
                int blinkIdx = playerData.blinkSpecialIndex;
                if (IsSpecialAvailable(blinkIdx))
                {
                    yield return PlaySpecialAndWait(blinkIdx);
                    continue;
                }
                else
                {
                    // Ningún special: esperar un rato y reintentar
                    if (debugLogs) Debug.Log("[Idle] Ningún special disponible, esperando breve y reintentando");
                    yield return new WaitForSeconds(0.25f);
                    continue;
                }
            }

            bool isBlink = chosen == playerData.blinkSpecialIndex;

            // Si el elegido es el mismo que el último y la regla exige distinto para non-blink, evitarlo
            bool requireDifferent = playerData.requireDifferentNonBlink; // si este flag no existe, debería añadirse a PlayerData; en tu asset aparece activado
            if (chosen == lastSpecialIndex && !(isBlink && playerData.blinkCanRepeat) && requireDifferent)
            {
                int alt = ChooseAlternativeSpecial(chosen);
                if (alt == -1)
                {
                    // No hay alternative: en este caso, preferimos hacer blink (si está disponible)
                    int blinkIdx = playerData.blinkSpecialIndex;
                    if (IsSpecialAvailable(blinkIdx) || playerData.blinkCanRepeat)
                    {
                        chosen = blinkIdx;
                    }
                    else
                    {
                        // Blink no disponible tampoco: breve espera y reintento (mantener tieso)
                        if (debugLogs) Debug.Log("[Idle] Solo quedaba el mismo special y blink no disponible -> esperar y reintentar");
                        yield return new WaitForSeconds(0.25f);
                        continue;
                    }
                }
                else
                {
                    chosen = alt;
                }
            }

            yield return PlaySpecialAndWait(chosen);
        }
    }

    private IEnumerator PlaySpecialAndWait(int chosen)
    {
        int animatorIndex = specialBaseAnimatorIndex + chosen;
        animator.SetFloat(idleIndexHash, animatorIndex);

        if (debugLogs)
            Debug.Log($"[Idle] Special {chosen} -> AnimatorIndex {animatorIndex}");

        yield return WaitForCurrentState();

        ApplyCooldown(chosen);
        lastSpecialIndex = chosen;

        animator.SetFloat(idleIndexHash, 2f);
        yield return new WaitForSeconds(0.2f);
    }

    private IEnumerator WaitForCurrentState(int layer = 0)
    {
        yield return null;
        AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(layer);
        // fallback
        float wait = info.length > 0f ? info.length : 0.5f;
        yield return new WaitForSeconds(wait);
    }

    private int ChooseSpecialIndex()
    {
        int n = playerData.specialIdleProbabilities?.Length ?? 0;
        if (n == 0) return -1;

        float total = 0f;
        List<int> candidates = new List<int>();

        for (int i = 0; i < n; i++)
        {
            float w = playerData.specialIdleProbabilities[i];
            if (w <= 0f) continue;

            if (Time.time < specialNextAllowedTime[i])
            {
                if (debugLogs) Debug.Log($"[Idle] Special {i} on cooldown until {specialNextAllowedTime[i]:F2} (now {Time.time:F2})");
                continue;
            }

            total += w;
            candidates.Add(i);
        }

        if (candidates.Count == 0) return -1;

        float r = Random.Range(0f, total);
        float acc = 0f;

        foreach (int i in candidates)
        {
            acc += playerData.specialIdleProbabilities[i];
            if (r <= acc)
                return i;
        }

        return candidates[candidates.Count - 1];
    }

    private int ChooseAlternativeSpecial(int exclude)
    {
        int n = playerData.specialIdleProbabilities?.Length ?? 0;
        for (int i = 0; i < n; i++)
        {
            if (i == exclude) continue;
            if (playerData.specialIdleProbabilities[i] <= 0f) continue;
            if (Time.time < specialNextAllowedTime[i]) continue;
            return i;
        }
        return -1;
    }

    private void ApplyCooldown(int index)
    {
        float min = index < playerData.specialIdleCooldownMin.Length
            ? playerData.specialIdleCooldownMin[index]
            : 1f;

        float max = index < playerData.specialIdleCooldownMax.Length
            ? playerData.specialIdleCooldownMax[index]
            : min;

        if (min > max) (min, max) = (max, min);

        float cd = Mathf.Approximately(min, max)
            ? min
            : Random.Range(min, max);

        specialNextAllowedTime[index] = Time.time + Mathf.Max(0.05f, cd);

        if (debugLogs)
            Debug.Log($"[Idle] Cooldown special {index}: {cd:F2}s (until {specialNextAllowedTime[index]:F2})");
    }

    // seed pa los cooldowns
    private void SeedInitialSpecialCooldowns()
    {
        int n = playerData.specialIdleProbabilities?.Length ?? 0;
        for (int i = 0; i < n; i++)
        {
            if (playerData.specialIdleProbabilities[i] <= 0f)
            {
                specialNextAllowedTime[i] = 0f;
                continue;
            }

            if (i == playerData.blinkSpecialIndex && playerData.blinkCanRepeat)
            {
                specialNextAllowedTime[i] = Time.time;
                if (debugLogs) Debug.Log($"[Idle] Seed: blink {i} allowed immediately");
                continue;
            }

            float min = (playerData.specialIdleCooldownMin != null && i < playerData.specialIdleCooldownMin.Length)
                ? playerData.specialIdleCooldownMin[i]
                : 0f;
            float max = (playerData.specialIdleCooldownMax != null && i < playerData.specialIdleCooldownMax.Length)
                ? playerData.specialIdleCooldownMax[i]
                : min;

            if (min > max) (min, max) = (max, min);

            if (Mathf.Approximately(min, 0f) && Mathf.Approximately(max, 0f))
            {
                specialNextAllowedTime[i] = Time.time;
                if (debugLogs) Debug.Log($"[Idle] Seed: special {i} no cooldown config, allowed");
                continue;
            }

            float seed = Mathf.Approximately(min, max) ? min : Random.Range(min, max);
            specialNextAllowedTime[i] = Time.time + seed;

            if (debugLogs)
                Debug.Log($"[Idle] Seed cooldown special {i}: seed {seed:F2}s (available at {specialNextAllowedTime[i]:F2})");
        }
    }

    private bool IsSpecialAvailable(int idx)
    {
        if (idx < 0) return false;
        if (playerData.specialIdleProbabilities == null) return false;
        if (idx >= playerData.specialIdleProbabilities.Length) return false;
        if (playerData.specialIdleProbabilities[idx] <= 0f) return false;
        if (Time.time < specialNextAllowedTime[idx]) return false;
        return true;
    }

    private void OnDisable()
    {
        if (idleCoroutine != null)
            StopCoroutine(idleCoroutine);
        idleCoroutine = null;
    }
}
