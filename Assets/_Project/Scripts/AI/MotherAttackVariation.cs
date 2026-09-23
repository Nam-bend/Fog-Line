using UnityEngine;

// Each completed or interrupted attack prepares a different move for the next trigger.
public sealed class MotherAttackVariation : StateMachineBehaviour
{
    private static readonly int Variant = Animator.StringToHash("AttackVariant");

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        int current = Mathf.Clamp(animator.GetInteger(Variant), 0, 3);
        animator.SetInteger(Variant, (current + Random.Range(1, 4)) % 4);
    }
}
