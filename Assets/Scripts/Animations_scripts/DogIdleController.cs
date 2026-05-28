using System.Collections;
using UnityEngine;

public class DogIdleController : MonoBehaviour
{
    private const int ScratchingIdleIndex = 0;
    private const int TailWagIdleIndex = 1;
    private const int BarkIdleIndex = 2;
    private const int RestIdleIndex = 3;
    private const int IdleResetIndex = 99;

    [Header("Ambient Variety")]
    [SerializeField] private bool enableAutonomousIdles = true;
    [SerializeField] private Vector2 idleIntervalRange = new Vector2(4f, 8f);
    [SerializeField, Range(0f, 1f)] private float restIdleChance = 0.4f;
    [SerializeField] private Vector2 restDurationRange = new Vector2(5f, 9f);
    [SerializeField] private float sitSettleDuration = 0.85f;
    [SerializeField] private float sitEndRecoveryDuration = 1.1f;
    [SerializeField] private float restCrossFadeDuration = 0.18f;
    [SerializeField] private string lieLoopStateName = "CatSimple_Lie_side_loop_1";
    [SerializeField] private string sitEndStateName = "Sit end";

    [Header("Debug")]
    [SerializeField] private bool enableDebugHotkeys = true;

    private Animator animator;
    private bool idleTriggered;
    private Coroutine waitingCoroutine;
    private float idleCountdown;
    private int lieLoopStateHash;
    private int sitEndStateHash;

    private void Start()
    {
        animator = GetComponent<Animator>();
        animator.SetInteger("IdleIndex", IdleResetIndex);
        lieLoopStateHash = ResolveAnimatorStateHash(lieLoopStateName);
        sitEndStateHash = ResolveAnimatorStateHash(sitEndStateName);
        ScheduleNextIdle();
    }

    private void Update()
    {
        if (enableDebugHotkeys)
        {
            HandleDebugHotkeys();
        }

        if (!enableAutonomousIdles || idleTriggered || waitingCoroutine != null)
        {
            return;
        }

        idleCountdown -= Time.deltaTime;
        if (idleCountdown > 0f)
        {
            return;
        }

        TriggerAmbientIdle();
    }

    private void HandleDebugHotkeys()
    {
        if (Input.GetKeyUp(KeyCode.Alpha1))
        {
            animator.SetBool("Move", false);
            TriggerOneShotIdle(ScratchingIdleIndex);
        }
        else if (Input.GetKeyUp(KeyCode.Alpha2))
        {
            animator.SetBool("Move", false);
            TriggerOneShotIdle(TailWagIdleIndex);
        }
        else if (Input.GetKeyUp(KeyCode.Alpha3))
        {
            animator.SetBool("Move", false);
            TriggerOneShotIdle(BarkIdleIndex);
        }
        else if (Input.GetKeyUp(KeyCode.Alpha4))
        {
            animator.SetBool("Move", false);
            TriggerRestIdle(RandomRange(restDurationRange));
        }
        else if (Input.GetKeyUp(KeyCode.P))
        {
            animator.SetBool("Move", false);
            animator.SetTrigger("Pickup");
        }
        else if (Input.GetKeyUp(KeyCode.O))
        {
            animator.SetBool("Move", false);
            animator.SetTrigger("PutDown");
        }

        if (Input.GetKeyUp(KeyCode.Alpha5))
        {
            animator.SetTrigger("SitEndTrigger");
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (animator.GetFloat("Speed") < 0.1f)
            {
                animator.SetTrigger("JumpInPlace");
            }
            else
            {
                animator.SetTrigger("Jump");
            }
        }
    }

    private void TriggerAmbientIdle()
    {
        animator.SetBool("Move", false);

        if (Random.value < restIdleChance)
        {
            TriggerRestIdle(RandomRange(restDurationRange));
            return;
        }

        int idleIndex = Random.Range(ScratchingIdleIndex, BarkIdleIndex + 1);
        TriggerOneShotIdle(idleIndex);
    }

    private void TriggerOneShotIdle(int idleIndex)
    {
        if (idleTriggered || waitingCoroutine != null)
        {
            return;
        }

        idleTriggered = true;
        animator.SetInteger("IdleIndex", idleIndex);
        waitingCoroutine = StartCoroutine(WaitForAnimationToFinish());
    }

    private void TriggerRestIdle(float restDuration)
    {
        if (idleTriggered || waitingCoroutine != null)
        {
            return;
        }

        idleTriggered = true;
        animator.SetInteger("IdleIndex", RestIdleIndex);
        waitingCoroutine = StartCoroutine(RestRoutine(restDuration));
    }

    private IEnumerator RestRoutine(float restDuration)
    {
        float totalDuration = Mathf.Max(sitSettleDuration, restDuration);

        yield return new WaitForSeconds(sitSettleDuration);

        bool usedLieLoop = false;
        if (lieLoopStateHash != 0)
        {
            animator.CrossFadeInFixedTime(lieLoopStateHash, restCrossFadeDuration, 0, 0f);
            usedLieLoop = true;
        }

        float remainingRest = Mathf.Max(0f, totalDuration - sitSettleDuration);
        if (remainingRest > 0f)
        {
            yield return new WaitForSeconds(remainingRest);
        }

        if (usedLieLoop && sitEndStateHash != 0)
        {
            animator.CrossFadeInFixedTime(sitEndStateHash, restCrossFadeDuration, 0, 0f);
        }
        else
        {
            animator.SetTrigger("SitEndTrigger");
        }

        yield return new WaitForSeconds(sitEndRecoveryDuration);

        FinishIdleState();
    }

    private IEnumerator WaitForAnimationToFinish()
    {
        yield return null;

        float waitTime = GetRemainingClipTime();
        yield return new WaitForSeconds(waitTime);

        while (animator.IsInTransition(0))
        {
            yield return null;
        }

        FinishIdleState();
    }

    private float GetRemainingClipTime()
    {
        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        AnimatorClipInfo[] clips = animator.GetCurrentAnimatorClipInfo(0);

        if (clips != null && clips.Length > 0)
        {
            float clipLength = clips[0].clip.length;
            float normalized = stateInfo.normalizedTime;
            float timeIntoClip = (normalized % 1f) * clipLength;
            return Mathf.Max(0f, clipLength - timeIntoClip);
        }

        return 0.5f;
    }

    private void FinishIdleState()
    {
        animator.SetInteger("IdleIndex", IdleResetIndex);
        animator.SetBool("Move", true);
        idleTriggered = false;
        waitingCoroutine = null;
        ScheduleNextIdle();
    }

    private void ScheduleNextIdle()
    {
        idleCountdown = RandomRange(idleIntervalRange);
    }

    private int ResolveAnimatorStateHash(string stateName)
    {
        if (animator == null || string.IsNullOrEmpty(stateName))
        {
            return 0;
        }

        int hash = Animator.StringToHash(stateName);
        if (animator.HasState(0, hash))
        {
            return hash;
        }

        string layerName = animator.GetLayerName(0);
        string[] candidates =
        {
            layerName + "." + stateName,
            "Base Layer." + stateName,
            layerName + ".IdleSM." + stateName,
            "Base Layer.IdleSM." + stateName,
            layerName + ".IdleSM.SitSM." + stateName,
            "Base Layer.IdleSM.SitSM." + stateName
        };

        for (int i = 0; i < candidates.Length; i++)
        {
            hash = Animator.StringToHash(candidates[i]);
            if (animator.HasState(0, hash))
            {
                return hash;
            }
        }

        return 0;
    }

    private float RandomRange(Vector2 range)
    {
        float min = Mathf.Min(range.x, range.y);
        float max = Mathf.Max(range.x, range.y);
        return Random.Range(min, max);
    }
}
