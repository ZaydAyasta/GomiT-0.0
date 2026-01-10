using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Animator))]
public class PlayerAnimation : MonoBehaviour
{
    public PlayerData playerData;
    public float idleSpeedThreshold = 0.1f;
    public float idlePlayDuration = 2.0f;

    private Animator animator;
    private Coroutine idleCoroutine;

    private readonly int idleIndexParam = Animator.StringToHash("IdleIndex");
    private readonly int speedParam = Animator.StringToHash("Speed");

    private int lastIdleIndex = -1;

    private void Awake()
    {
        animator = GetComponent<Animator>();
    }

    private void OnEnable()
    {
        animator.SetInteger(idleIndexParam, 0);
    }

    public void SetSpeed(float value)
    {
        if (!gameObject.activeInHierarchy) return;

        animator.SetFloat(speedParam, value);

        if (Mathf.Abs(value) <= idleSpeedThreshold)
        {
            if (idleCoroutine == null)
                idleCoroutine = StartCoroutine(IdleRoutine());
        }
        else
        {
            StopIdleRoutine();
        }
    }

    private IEnumerator IdleRoutine()
    {
        while (true)
        {
            float wait = Random.Range(playerData.idleMinDelay, playerData.idleMaxDelay);
            yield return new WaitForSeconds(wait);

            int idleIndex = ChooseNextIdleIndex();
            animator.SetInteger(idleIndexParam, idleIndex);

            float clipLength = GetAnimationClipLength(idleIndex);
            yield return new WaitForSeconds(
                clipLength > 0f ? clipLength : idlePlayDuration
            );

            animator.SetInteger(idleIndexParam, 0);
        }
    }

    private int ChooseNextIdleIndex()
    {
        int index;
        do
        {
            index = ChooseWeightedIndex(playerData.idleProbabilities) + 1;
        }
        while (index == lastIdleIndex);

        lastIdleIndex = index;
        return index;
    }

    private int ChooseWeightedIndex(float[] weights)
    {
        float total = 0f;
        foreach (float w in weights) total += w;

        float r = Random.Range(0f, total);
        float acc = 0f;

        for (int i = 0; i < weights.Length; i++)
        {
            acc += weights[i];
            if (r <= acc)
                return i;
        }

        return weights.Length - 1;
    }

    private float GetAnimationClipLength(int idleIndex)
    {
        string clipName = $"idle{idleIndex}";

        foreach (var clip in animator.runtimeAnimatorController.animationClips)
        {
            if (clip.name == clipName)
                return clip.length;
        }

        return 0f;
    }

    private void StopIdleRoutine()
    {
        if (idleCoroutine != null)
        {
            StopCoroutine(idleCoroutine);
            idleCoroutine = null;
        }

        animator.SetInteger(idleIndexParam, 0);
    }

    private void OnDisable()
    {
        StopIdleRoutine();
    }
}