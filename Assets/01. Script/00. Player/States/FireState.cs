using UnityEngine;

namespace Study_ActionPlatformer
{
    public class FireState : PlayerAnimStateBase
    {
        public FireState(PlayerController owner) : base(owner)
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
                Animator.SetBool(PlayerController.IS_FIRE, false);
            }
        }

        public override void Exit()
        {
        }
    }
}
