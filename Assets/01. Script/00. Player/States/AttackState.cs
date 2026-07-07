using Study.Utilities;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Study_ActionPlatformer
{
    public class AttackState : PlayerAnimStateBase
    {
        public AttackState(PlayerController owner) : base(owner) { }

        public override void Enter()
        {
            // 공격중엔 이동 금지
            Owner.StopMovement();
        }

        public override void UpdateState(AnimatorStateInfo stateInfo)
        {
            // AnimatorStateInfo.normalizedTime
            // - 현재 애니메이션 진행율 나타냅니다
            // - 시작 되지 않았으면 0% = 0.0f
            // - 절반 진행됐으면 50% = 0.5f
            // - 끝났으면 100% = 1.0f (보통 잘 안쓰임)

            // 상태의 초반부에는 직전 공격 입력을 제거해준다
            if (stateInfo.normalizedTime < INPUT_RESET_TIME)
            {
                Animator.SetBool(PlayerController.IS_ATTACK, false);
            }
            // 콤보 입력 구간 (30% ~ 90%)
            else if (INPUT_RESET_TIME < stateInfo.normalizedTime &&
                stateInfo.normalizedTime < COMBO_INPUT_END_TIME)
            {
                // 다음 콤보 입력이 없다면 입력을 받아준다
                if (Animator.GetBool(PlayerController.IS_ATTACK) == false)
                {
                    bool input = SimpleInput.GetKeyDown(Key.Z);
                    Animator.SetBool(PlayerController.IS_ATTACK, input);
                }
            }
        }
    }
}

