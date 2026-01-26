using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Animator))]
public class PlayerAnimation : MonoBehaviour
{
    public PlayerData playerData;
    public float idleSpeedThreshold = 0.1f;

    private Animator animator;
    private Coroutine idleCoroutine;

    private int lastIdleChoice = -1;

    private readonly int speedParam = Animator.StringToHash("Speed");
    private readonly int idleIndexParam = Animator.StringToHash("IdleIndex");

    private void Awake()
    {
        animator = GetComponent<Animator>();
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

            int idleChoice = ChooseWeightedIndex(playerData.idleProbabilities);

            animator.SetFloat(idleIndexParam, idleChoice);

            yield return WaitForIdleClip(idleChoice);

            animator.SetFloat(idleIndexParam, 0f);
        }
    }

    private IEnumerator WaitForIdleClip(int idleIndex)
    {
        yield return null; 

        AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
        float clipLength = info.length;

        yield return new WaitForSeconds(clipLength);
    }

    private int ChooseWeightedIndex(float[] weights)
    {
        int index;

        do
        {
            float total = 0f;
            foreach (float w in weights)
                total += w;

            float r = Random.Range(0f, total);
            float acc = 0f;

            index = weights.Length - 1;

            for (int i = 0; i < weights.Length; i++)
            {
                acc += weights[i];
                if (r <= acc)
                {
                    index = i;
                    break;
                }
            }
        }
        while (index == lastIdleChoice && weights.Length > 1);

        lastIdleChoice = index;
        return index;
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
