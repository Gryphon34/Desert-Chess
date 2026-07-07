using UnityEngine;

namespace Study_ActionPlatformer
{
    public class AttackEndState : PlayerAnimStateBase
    {
        public AttackEndState(PlayerController owner) : base(owner)
        {

        }

        public override void Enter()
        {
            Owner.StopMovement();
        }

        public override void UpdateState(AnimatorStateInfo stateInfo)
        {
            if (stateInfo.normalizedTime < INPUT_RESET_TIME)
            {
                Animator.SetBool(PlayerController.IS_ATTACK, false);
            }
        }
    }
}
