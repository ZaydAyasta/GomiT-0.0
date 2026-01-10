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
    private int idleIndexParam = Animator.StringToHash("IdleIndex");
    private int speedParam = Animator.StringToHash("Speed");

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
        animator.SetFloat(speedParam, value);
        if (Mathf.Abs(value) <= idleSpeedThreshold)
        {
            if (idleCoroutine == null)
                idleCoroutine = StartCoroutine(IdleRoutine());
        }
        else
        {
            if (idleCoroutine != null)
            {
                StopCoroutine(idleCoroutine);
                idleCoroutine = null;
            }
            animator.SetInteger(idleIndexParam, 0);
        }
    }

    private IEnumerator IdleRoutine()
    {
        while (true)
        {
            float wait = Random.Range(playerData.idleMinDelay, playerData.idleMaxDelay);
            yield return new WaitForSeconds(wait);

            int choice = ChooseWeightedIndex(playerData.idleProbabilities) + 1;
            animator.SetInteger(idleIndexParam, choice);

            float length = GetAnimationClipLengthForIdle(choice);
            yield return new WaitForSeconds(length > 0 ? length : idlePlayDuration);

            animator.SetInteger(idleIndexParam, 0);
        }
    }

    private int ChooseWeightedIndex(float[] weights)
    {
        float total = 0f;
        foreach (var w in weights) total += w;
        float r = Random.Range(0f, total);
        float acc = 0f;
        for (int i = 0; i < weights.Length; i++)
        {
            acc += weights[i];
            if (r <= acc) return i;
        }
        return weights.Length - 1;
    }

    private float GetAnimationClipLengthForIdle(int idleIndex)
    {
        string clipName = $"idle{idleIndex}";
        foreach (var clip in animator.runtimeAnimatorController.animationClips)
        {
            if (clip.name == clipName) return clip.length;
        }
        return 0f;
    }

    private void OnDisable()
    {
        if (idleCoroutine != null)
        {
            StopCoroutine(idleCoroutine);
            idleCoroutine = null;
        }
    }

}
