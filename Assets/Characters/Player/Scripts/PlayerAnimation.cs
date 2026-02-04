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
    [Tooltip("IdleIndex donde empiezan los special idles")]
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

        animator.SetFloat(idleIndexHash, 0);
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
                animator.SetFloat(idleIndexHash, 0);

                if (timeInIdleNormal < playerData.idleToTransitionDelay)
                {
                    yield return null;
                    continue;
                }

                animator.SetFloat(idleIndexHash, 1);
                if (debugLogs) Debug.Log("[Idle] idle_transition");
                yield return WaitForCurrentState();

                animator.SetFloat(idleIndexHash, 2);
                if (debugLogs) Debug.Log("[Idle] idle_tieso");
                yield return new WaitForSeconds(playerData.transitionToLoopDelay);

                inTiesoLoop = true;
                lastSpecialIndex = -1;
                continue;
            }

            // FASE 2: loop de idles especiales
            animator.SetFloat(idleIndexHash, 2); // asegurar tieso

            int chosen = ChooseSpecialIndex();
            if (chosen == -1)
            {
                yield return new WaitForSeconds(0.5f);
                continue;
            }

            // evitar repetir special (excepto blink si está permitido)
            bool isBlink = chosen == playerData.blinkSpecialIndex;
            if (chosen == lastSpecialIndex && !(isBlink && playerData.blinkCanRepeat))
            {
                int alt = ChooseAlternativeSpecial(chosen);
                if (alt == -1)
                {
                    yield return new WaitForSeconds(0.3f);
                    continue;
                }
                chosen = alt;
            }

            int animatorIndex = specialBaseAnimatorIndex + chosen;
            animator.SetFloat(idleIndexHash, animatorIndex);

            if (debugLogs)
                Debug.Log($"[Idle] Special {chosen} -> AnimatorIndex {animatorIndex}");

            yield return WaitForCurrentState();

            ApplyCooldown(chosen);
            lastSpecialIndex = chosen;

            animator.SetFloat(idleIndexHash, 2); // volver a tieso
            yield return new WaitForSeconds(0.2f);
        }
    }

    private IEnumerator WaitForCurrentState(int layer = 0)
    {
        yield return null;
        AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(layer);
        yield return new WaitForSeconds(info.length);
    }

    private int ChooseSpecialIndex()
    {
        int n = playerData.specialIdleProbabilities?.Length ?? 0;
        if (n == 0) return -1;

        float total = 0f;
        List<int> candidates = new List<int>();

        for (int i = 0; i < n; i++)
        {
            if (playerData.specialIdleProbabilities[i] <= 0f) continue;
            if (Time.time < specialNextAllowedTime[i]) continue;

            total += playerData.specialIdleProbabilities[i];
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
            Debug.Log($"[Idle] Cooldown special {index}: {cd:F2}s");
    }

    private void OnDisable()
    {
        if (idleCoroutine != null)
            StopCoroutine(idleCoroutine);
        idleCoroutine = null;
    }
}
