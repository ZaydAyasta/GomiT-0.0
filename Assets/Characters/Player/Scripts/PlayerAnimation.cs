using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Animator))]
public class PlayerAnimation : MonoBehaviour
{
    public PlayerData playerData;
    public float idleSpeedThreshold = 0.1f;

    private Animator animator;
    private Coroutine idleCoroutine;

    private int lastIdleChoice = -1;
    bool isPlayingIdleSpecial;


    // Cooldown tracking: índice lógico (0..N-1) -> next allowed time (Time.time)
    private Dictionary<int, float> idleNextAllowedTime = new Dictionary<int, float>();

    // Fallback cooldown (segundos) si el usuario dejó 0/0 en PlayerData para un idle
    private const float fallbackCooldownIfZero = 4f;

    private readonly int speedParam = Animator.StringToHash("Speed");
    private readonly int idleIndexParam = Animator.StringToHash("IdleIndex"); // 0 = base, 1..N = especiales

    private void Awake()
    {
        animator = GetComponent<Animator>();

        if (playerData == null)
        {
            Debug.LogError("[PlayerAnimation] PlayerData is null!");
            enabled = false;
            return;
        }

        ValidatePlayerData();

        // Inicializar tiempos
        idleNextAllowedTime.Clear();
        for (int i = 0; i < playerData.idleProbabilities.Length; i++)
        {
            idleNextAllowedTime[i] = 0f;
        }
    }

    private void ValidatePlayerData()
    {
        int len = playerData.idleProbabilities.Length;

        // Asegura que cooldown arrays existan y tengan el mismo tamaño
        if (playerData.idleCooldownMin == null || playerData.idleCooldownMin.Length != len)
        {
            Debug.LogWarning("[PlayerAnimation] idleCooldownMin length mismatch — rellenando con ceros.");
            playerData.idleCooldownMin = new float[len];
        }

        if (playerData.idleCooldownMax == null || playerData.idleCooldownMax.Length != len)
        {
            Debug.LogWarning("[PlayerAnimation] idleCooldownMax length mismatch — rellenando con ceros.");
            playerData.idleCooldownMax = new float[len];
        }

        // Si probabilidad es 0, el idle nunca debe salir (válido).
        // Pero si cooldowns son 0 y la probabilidad > 0, ponemos fallback para que se note.
        for (int i = 0; i < len; i++)
        {
            if (playerData.idleProbabilities[i] > 0f)
            {
                if (playerData.idleCooldownMin[i] <= 0f && playerData.idleCooldownMax[i] <= 0f)
                {
                    // Aplicamos fallback solo si la intención es que el idle tenga probabilidad >0
                    playerData.idleCooldownMin[i] = fallbackCooldownIfZero;
                    playerData.idleCooldownMax[i] = fallbackCooldownIfZero;
                    Debug.LogWarning($"[PlayerAnimation] Idle {i} had zero cooldowns but weight > 0. Applying fallback cooldown {fallbackCooldownIfZero}s.");
                }
            }
        }
    }

    public void SetSpeed(float speed, bool grounded)
    {
        animator.SetFloat(speedParam, speed);

        if (!grounded)
        {
            StopIdleRoutine();
            animator.SetFloat(idleIndexParam, 0f);
            return;
        }

        if (Mathf.Abs(speed) <= idleSpeedThreshold)
        {
            if (idleCoroutine == null)
                idleCoroutine = StartCoroutine(IdleRoutine());
        }
        else
        {
            StopIdleRoutine();
            animator.SetFloat(idleIndexParam, 0f);
        }
    }

    private IEnumerator IdleRoutine()
    {
        while (true)
        {
            float wait = Random.Range(
                playerData.idleMinDelay,
                playerData.idleMaxDelay
            );

            yield return new WaitForSeconds(wait);

            int idleChoice = ChooseIdleWithCooldown();

            if (idleChoice == -1)
            {
                // No hay idles válidos ahora mismo: esperar un tick y reintentar
                yield return new WaitForSeconds(0.5f);
                continue;
            }

            // Ejecutar idle: IMPORTANT -> pasamos (idleChoice + 1) al Animator
            // porque 0 = base (sin idle especial).
            int animatorIndex = idleChoice + 1;
            animator.SetFloat(idleIndexParam, animatorIndex);
            Debug.Log($"[PlayerAnimation] Playing idle {idleChoice} -> animator Index {animatorIndex}");

            // Esperar a que el clip (o clips) activos terminen
            yield return StartCoroutine(WaitForIdleClip());

            // Volver a idle base visualmente
            animator.SetFloat(idleIndexParam, 0f);

            // Aplicar cooldown real al idle ejecutado (usa índice lógico)
            ApplyCooldown(idleChoice);
        }
    }

    private int ChooseIdleWithCooldown()
    {
        List<int> validIndices = new List<int>();
        List<float> validWeights = new List<float>();

        for (int i = 0; i < playerData.idleProbabilities.Length; i++)
        {
            // Si peso 0 -> no es candidato
            if (playerData.idleProbabilities[i] <= 0f) continue;

            // Evitar repetición inmediata
            if (i == lastIdleChoice)
                continue;

            // Chequear cooldown individual
            if (Time.time < idleNextAllowedTime[i])
                continue;

            validIndices.Add(i);
            validWeights.Add(playerData.idleProbabilities[i]);
        }

        if (validIndices.Count == 0)
            return -1;

        float total = 0f;
        foreach (float w in validWeights)
            total += w;

        float r = Random.Range(0f, total);
        float acc = 0f;

        for (int k = 0; k < validWeights.Count; k++)
        {
            acc += validWeights[k];
            if (r <= acc)
            {
                int chosen = validIndices[k];
                lastIdleChoice = chosen;
                return chosen;
            }
        }

        // fallback
        int last = validIndices[validIndices.Count - 1];
        lastIdleChoice = last;
        return last;
    }

    private void ApplyCooldown(int idleIndex)
    {
        float min = playerData.idleCooldownMin[idleIndex];
        float max = playerData.idleCooldownMax[idleIndex];

        if (min > max)
        {
            float t = min; min = max; max = t;
        }

        float cooldown = Mathf.Approximately(min, max) ? min : Random.Range(min, max);
        if (cooldown <= 0f)
        {
            cooldown = fallbackCooldownIfZero;
        }

        idleNextAllowedTime[idleIndex] = Time.time + cooldown;

        Debug.Log($"[PlayerAnimation] Idle {idleIndex} on cooldown for {cooldown:F2}s until {idleNextAllowedTime[idleIndex]:F2}");
    }

    private IEnumerator WaitForIdleClip()
    {
        // Esperar 1 frame a que el Animator aplique el cambio de parámetro y seleccione clips
        yield return null;

        AnimatorClipInfo[] clips = animator.GetCurrentAnimatorClipInfo(0);

        float clipLength = 0f;
        if (clips != null && clips.Length > 0)
        {
            for (int i = 0; i < clips.Length; i++)
            {
                if (clips[i].clip != null)
                    clipLength = Mathf.Max(clipLength, clips[i].clip.length);
            }
        }

        // Si no encontramos clips (por BlendTree o timing), fallback razonable
        if (clipLength <= 0f)
        {
            // fallback pequeño para q no haya loops infinitos
            clipLength = 0.5f;
        }

        yield return new WaitForSeconds(clipLength);
    }

    private void StopIdleRoutine()
    {
        if (idleCoroutine != null)
        {
            StopCoroutine(idleCoroutine);
            idleCoroutine = null;
        }
    }

    private void OnDisable()
    {
        StopIdleRoutine();
    }
}
