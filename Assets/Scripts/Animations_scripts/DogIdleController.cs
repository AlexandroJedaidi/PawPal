using UnityEngine;
using System.Collections;

public class DogIdleController : MonoBehaviour
{
    Animator animator;
    bool idleTriggered = false;
    Coroutine waitingCoroutine = null;


    void Start()
    {
        animator = GetComponent<Animator>();
        animator.SetInteger("IdleIndex", 99);
    }

    void Update()
    {
        if (Input.GetKeyUp(KeyCode.Alpha1))
        {
            animator.SetBool("Move", false);
            TriggerRandomIdle(0);
        }
        else if (Input.GetKeyUp(KeyCode.Alpha2))
        {
            animator.SetBool("Move", false);
            TriggerRandomIdle(1);
        }
        else if (Input.GetKeyUp(KeyCode.Alpha3))
        {
            animator.SetBool("Move", false);
            TriggerRandomIdle(2);
        }
        else if (Input.GetKeyUp(KeyCode.Alpha4))
        {
            animator.SetBool("Move", false);
            TriggerRandomIdle(3);
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
        if (!idleTriggered && !animator.GetBool("Move"))
        {
            animator.SetBool("Move", true);
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

    void TriggerRandomIdle(int idleIndex)
    {
        // Set guard immediately so we don't start another idle while this one starts up
        idleTriggered = true;
        animator.SetInteger("IdleIndex", idleIndex);

        // Only start one Wait coroutine
        if (waitingCoroutine == null)
            waitingCoroutine = StartCoroutine(WaitForAnimationToFinish());
    }

    IEnumerator EndSitAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        // Sit-End-Transition auslösen
        animator.SetTrigger("SitEndTrigger");
    }

    IEnumerator WaitForAnimationToFinish()
    {
        // Let the animator apply the new state for at least one frame (handles transitions)
        yield return null;

        // Try to get the currently playing clip and its remaining time
        float waitTime = 0f;
        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        AnimatorClipInfo[] clips = animator.GetCurrentAnimatorClipInfo(0);

        if (clips != null && clips.Length > 0)
        {
            // Use the first clip's length as an approximation
            float clipLength = clips[0].clip.length;

            // normalizedTime might be >1 if it already looped. Get fractional progress:
            float normalized = stateInfo.normalizedTime;
            float timeIntoClip = (normalized % 1f) * clipLength; // fractional part -> progress in current loop
            waitTime = Mathf.Max(0f, clipLength - timeIntoClip);

            // As a safety, also clamp a reasonable max wait (optional)
            // waitTime = Mathf.Clamp(waitTime, 0f, 10f);
        }
        else
        {
            // Fallback if no clip info: wait a small amount to avoid tight loops
            waitTime = 0.5f;
        }

        // Wait the computed time, and then also wait until not in transition
        yield return new WaitForSeconds(waitTime);

        // Wait until the animator is not in a transition (helps transition-edge cases)
        while (animator.IsInTransition(0))
            yield return null;

        // Finally set Move = true to indicate we're done idling
        animator.SetBool("Move", true);

        // Reset guards
        idleTriggered = false;
        waitingCoroutine = null;

        Debug.Log("Idle animation finished - Move set to true");
    }

    // Helper: get the state name from hash (optional)
    private string GetCurrentStateName(AnimatorStateInfo info)
    {
        return info.IsName("Scratching") ? "Scratching" :
               info.IsName("TailWag") ? "TailWag" :
               info.IsName("Sit start") ? "Sit start" :
               info.IsName("Sit loop") ? "Sit loop" :
               info.IsName("Sit end") ? "Sit end" :
               info.IsName("Bark") ? "Bark" : "";
    }
}
