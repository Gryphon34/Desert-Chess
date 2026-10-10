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

            // 마법 상태에 들어온 순간 바로 끕니다.
            // 예전에는 UpdateState에서 normalizedTime < 0.4일 때만 껐는데,
            // Idle -> Fire 전이(0.25초)가 끝나면 0.5초짜리 클립이 이미 50% 진행된 상태라
            // 조건에 한 번도 걸리지 않았습니다. 그래서 IsFire가 계속 켜진 채로 남아
            // Fire -> Idle -> Fire가 무한 반복됐습니다.
            Animator.SetBool(PlayerController.IS_FIRE, false);
        }

        public override void UpdateState(AnimatorStateInfo stateInfo)
        {
        }

        public override void Exit()
        {
        }
    }
}
