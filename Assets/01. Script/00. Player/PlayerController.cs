using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Study.Utilities;


namespace Study_ActionPlatformer
{
    // 조작, 애니메이션 재생을 담당하는 클래스 입니다.

    // [책임 분리]
    // - PlayerController       : "무엇을 할지" 결정하는 클래스 입니다.
    //                           입력을 읽고, 상태를 갈아끼우고, 공용 동작을 제공합니다
    // - CharacterController2D  : "어떻게 움직일지"를 결정하는 클래스 입니다.
    // - 각종 States 들         : 애니메이션 실행시 상태별 행동들이 정의되어 있습니다.

    //  PlayerController는 상태패턴 기반의 FSM(유한상태기계)입니다.
    // 저는 아래 내용의 구조를 Follower FSM 이라고 부릅니다
    // 아래의 상태머신의 전이(Transition)의 주인은 애니메이터로 설정되어있습니다.
    // 매 프레임 현재 애니메이터의 애니메이션을 읽어서 그에 맞는
    // 상태 클래스를 실행시키도록 되어있습니다. 그래서 전이를 결정하는 로직이 없습니다.
    // (원래 FSM은 Transition이 매우 중요함)
    public class PlayerController : MonoBehaviour
    {
        // 애니메이터 상태 Tag
        // - 애미메이터의 상태 마다 지정할 수 있느 string Tag
        // - 컴포넌트에서는 해당 정보를 읽어서 현재 애니메이션 상태를 추측할 수 있다

        private const string ANIM_TAG_ATTACK = "Attack";
        private const string ANIM_TAG_ATTACK_END = "Attack_End";
        private const string ANIM_TAG_MOVEMENT = "Movement";
        private const string ANIM_TAG_FIRE = "Fire";
        private const string ANIM_TAG_JUMP = "Jump";

        // 애니메이터 파라미터 해시
        // - string을 이용해서 파라미터를 전달하면 가비지가 생기며, 실수(오타)가 많이 일어남
        //  특정 string값을 애니메이터 별로 Hash값으로 치환하여 애니메이션 파라미터로 사용
        // - static readonly : 상수처럼 다루고 싶어서 사용하는 키워드 입니다.

        public static readonly int MOVEMENT = Animator.StringToHash("Movement");
        public static readonly int IS_ATTACK = Animator.StringToHash("IsAttack");
        public static readonly int IS_GROUNDED = Animator.StringToHash("IsGrounded");
        public static readonly int JUMP = Animator.StringToHash("Jump");
        public static readonly int DESCENDING = Animator.StringToHash("Descending");
        public static readonly int IS_FIRE = Animator.StringToHash("IsFire");


        public Animator Animator { get; private set; }
        private SpriteRenderer SpriteRenderer { get; set; }
        private CharacterController2D Controller2D { get; set; }

        // 바라보는 방향. +1 = 오른쪽, -1은 = 왼쪽으로 처리함
        public int FacingDirection { get; private set; } = 1;

        // 각 상태를 제어하기 위한 멤버 변수들
        private Dictionary<int, PlayerAnimStateBase> StateDic { get; set; } = new();
        private PlayerAnimStateBase defaultState; // 예외처리를 위한 기본 상태
        private PlayerAnimStateBase currentState; // 상태객체를 담아서 제어하기 위한 빈 객체 변수 

        private bool prevIsGrounded = false;

        private void Awake()
        {
            Animator = GetComponentInChildren<Animator>();
            SpriteRenderer = GetComponentInChildren<SpriteRenderer>();
            Controller2D = GetComponent<CharacterController2D>();

            defaultState = new MovementState(this);

            StateDic.Add(Animator.StringToHash(ANIM_TAG_MOVEMENT), defaultState);
            StateDic.Add(Animator.StringToHash(ANIM_TAG_ATTACK), new AttackState(this));
            StateDic.Add(Animator.StringToHash(ANIM_TAG_ATTACK_END), new AttackEndState(this));
            StateDic.Add(Animator.StringToHash(ANIM_TAG_FIRE), new FireState(this));
            StateDic.Add(Animator.StringToHash(ANIM_TAG_JUMP), new JumpState(this));

        }

        private void Update()
        {
            UpdateSlotInput();
            UpdateAnimState();

            // 아래는 판정에 의한 애니메이션 재생용도라서
            // Update()에서 호출함
            UpdateJumpInput();
            UpdateGroundedAnimation();
        }

        private void UpdateSlotInput()
        {
            Player player = Player.LocalPlayer;
            if (player == null) return;

            // New Input System의 Key 열거형은 숫자열 키를 Digit1~Digit0으로 정의합니다.
            // (레거시 KeyCode의 Alpha1이 아닙니다)
            if (SimpleInput.GetKeyDown(Key.Digit1)) player.SelectWeaponSlot(0);
            else if (SimpleInput.GetKeyDown(Key.Digit2)) player.SelectWeaponSlot(1);
            else if (SimpleInput.GetKeyDown(Key.Digit3)) player.SelectWeaponSlot(2);
            else if (SimpleInput.GetKeyDown(Key.Digit4)) player.SelectMagicSlot(0);
            else if (SimpleInput.GetKeyDown(Key.Digit5)) player.SelectMagicSlot(1);
            else if (SimpleInput.GetKeyDown(Key.Digit6)) player.SelectMagicSlot(2);
        }

        private void UpdateAnimState()
        {
            AnimatorStateInfo stateInfo = Animator.GetCurrentAnimatorStateInfo(0);
            // # AnimatorStateInfo? 
            // - Animator의 상태 정보를 담고 있는 구조체 입니다.
            // - 현재 '애니메이터'의 특정 '레이어'가 어떤 '상태'에 머물러 있는지
            //  그리고 현재 '상태'가 재생중인 '애니메이션'의 진행 상태는 어떠한지
            //  등에 대한 정보를 제공합니다.
            // - 현재 재생중인 애니메이션이 얼마나 진행이 되었는지?
            // - 현재 재생중인 상태의 Tag는 무엇인지?
            // - 현재 재생중인 상태의 이름은 뭔지?

            if (StateDic.TryGetValue(
                stateInfo.tagHash, out PlayerAnimStateBase nextState) == false)
            {
                // 검색해서 없으면 nextState = defaultState 처리
                nextState = defaultState;
            }

            // 상태가 변경이 되었다면
            if (currentState != nextState)
            {
                currentState?.Exit();
                // ?. : null 체크하는 키워드. null이 아닐때만 호출한다는 표현
                // => if(currentState != null) currentState.Exit() 과 같은 표현
                currentState = nextState;
                nextState.Enter();
            }

            currentState.UpdateState(stateInfo);
        }


        // 아래부터는 상태 클래스들이 사용하는 공용함수들입니다

        public void HandleMovement()
        {
            Vector2 inputVector = SimpleInput.GetMoveAxisRaw();
            float absMovement = Mathf.Abs(inputVector.x);
            Animator.SetFloat(MOVEMENT, absMovement);

            if (absMovement > 0) // 이동량이 있을때
            {
                // 삼항 연산자로 정면을 설정해준다
                FacingDirection = (inputVector.x < 0) ? -1 : 1;

                // x 스케일을 반전해서 좌우를 뒤집는다.
                // (히트박스를 뒤집기 위해서 스케일 반전을 사용)
                Animator.transform.localScale = new Vector3(FacingDirection, 1, 1);
            }


            Controller2D.SetMoveInput(inputVector.x);

            if (SimpleInput.GetKeyDown(Key.Z))
            {
                BeginWeaponAttack();
            }

            if (SimpleInput.GetKeyDown(Key.X))
            {
                // 마법은 여기서 딱 한 번만 차감됩니다.
                // (예전에는 여기서 한 번, 명중 시 HitBox에서 또 한 번 차감돼서
                //  5회짜리 마법이 실제로는 2~3회 만에 사라졌습니다)
                Player player = Player.LocalPlayer;
                if (player != null && player.TryFireActiveMagic())
                {
                    Animator.SetBool(IS_FIRE, true);
                }
            }
        }

        /// <summary>
        /// 무기 공격을 시작합니다. [기획서 9번] 사용 횟수는 "공격을 시도한 순간"
        /// 1회 차감됩니다. 명중 여부나 맞은 적의 수와는 무관합니다.
        ///
        /// 횟수가 다 떨어진 경우 TryConsumeActiveWeaponUse()가 슬롯을 주먹으로
        /// 되돌리고 false를 반환합니다. 이때도 공격 자체는 나가야 하므로
        /// (주먹으로 때리는 것) 반환값과 무관하게 애니메이션을 재생합니다.
        /// 이 함수는 첫 타와 콤보 입력 양쪽에서 호출되므로 여기 하나만 고치면 됩니다.
        /// </summary>
        public void BeginWeaponAttack()
        {
            Player.LocalPlayer?.TryConsumeActiveWeaponUse();
            Animator.SetBool(IS_ATTACK, true);
        }

        public void StopMovement()
        {
            Controller2D.SetMoveInput(0.0f);
        }

        // 점프는 AnyState로 동작하기 때문에 상태머신에서 입력처리를 합니다.
        // 상태 객체에게 호출을 위임해도 무방합니다
        private void UpdateJumpInput()
        {
            if (SimpleInput.GetKeyDown(Key.Space))
            {
                // 점프가 가능한 상태인지는 CharacterController가 판단함.
                Controller2D.RequestJump();
            }
        }



        /// <summary>
        /// CharacterController2D의 접지 상태를 애니메이터로 옮겨주는 함수
        /// </summary>
        private void UpdateGroundedAnimation()
        {
            bool isGrounded = Controller2D.IsGrounded;
            Animator.SetBool(IS_GROUNDED, isGrounded);

            // 이전 프레임에서는 접지(바닥에 닿아있는) 상태였다가
            // 현재 프레임에서는 접지 하지 않는 상태가 되었을 때

            if (prevIsGrounded && isGrounded == false)
            {
                // VerticalVelocity가 0보다 크면 => 상승
                if (Controller2D.VerticalVelocity > 0.0f)
                    Animator.SetTrigger(JUMP);
                // 아니면 하강
                else
                    Animator.SetTrigger(DESCENDING);
            }

            prevIsGrounded = isGrounded;
        }


    }

}

