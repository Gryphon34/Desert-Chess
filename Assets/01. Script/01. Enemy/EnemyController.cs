using Study.Utilities;
using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

namespace Study_ActionPlatformer
{
    public class EnemyController : MonoBehaviour
    {
        // 코루틴을 이용한 FSM 만들기
        // 코루틴의 yield return StartCoroutine(코루틴); 함수를 이용해서
        // 코루틴들끼리 연결되어 끊임없이 순환하는 구조의
        // 소규모 인공지능 캐릭터를 만들어 봅시다.

        // : Simple FSM 이라고 부름(나만)

        private static readonly int IS_MOVE = Animator.StringToHash("IsMove");
        private static readonly int ATTACK = Animator.StringToHash("Attack");

        private const float ATTACK_HIT_DELAY = 0.5f;
        private const float ATTACK_COOLDOWN = 2.0f;

        [SerializeField] private float moveSpeed = 1f;
        [SerializeField] private float traceRange = 10.0f;
        [SerializeField] private float attackRange = 3f;
        [SerializeField] private float baseUpdateTerm = 0.1f;





        // 인스펙터에서 직접 지정할 수도 있고(테스트 씬), 비워두면 런타임에 자동으로
        // 플레이어를 찾습니다(RoundManager가 스폰하는 경우).
        //
        // FormerlySerializedAs : 예전 필드명이 "Target"이었기 때문에 붙였습니다.
        // 이게 없으면 이미 씬에 저장돼 있던 Target 연결이 전부 끊깁니다.
        [FormerlySerializedAs("Target")]
        [SerializeField] private Transform target;

        /// <summary>
        /// 추격/공격 대상입니다.
        ///
        /// 왜 프로퍼티인가:
        /// 몬스터는 라운드 중에 Instantiate되기 때문에, 씬에 미리 존재하는 플레이어를
        /// 인스펙터로 연결해 둘 수가 없습니다. 그렇다고 Awake에서 한 번만 찾으면
        /// 스크립트 실행 순서에 따라 Player.LocalPlayer가 아직 null일 수 있습니다.
        /// 그래서 "필요할 때마다, 없으면 그때 찾는다"로 처리합니다.
        ///
        /// 파괴된 오브젝트는 유니티에서 == null이 true가 되므로,
        /// 플레이어가 죽거나 교체되어도 다음 접근에서 자동으로 다시 찾습니다.
        /// </summary>
        public Transform Target
        {
            get
            {
                if (target == null && Player.LocalPlayer != null)
                    target = Player.LocalPlayer.transform;

                return target;
            }
            set => target = value;
        }

        private Animator Animator { get; set; }
        // 파생 컨트롤러(RangeController 등)도 "누가 때리는지"를 알아야 하므로 protected입니다.
        protected Enemy Enemy { get; private set; }
        private RoundManager roundManager;

        private Vector3 originalScale;

        // 빈 코루틴 필드 (빈 객체랑 동일하다)
        protected IEnumerator nextStateCoroutine;

        private void Awake()
        {
            Animator = GetComponentInChildren<Animator>();
            Enemy = GetComponentInChildren<Enemy>();
            originalScale = transform.localScale;

            // y값 보정이 필요합니다.
            pointA = transform.position;
            pointA.y = transform.position.y;

            // 순찰 지점이 지정되지 않았다면 제자리를 순찰 지점으로 삼습니다.
            // (프리팹 세팅 누락 하나로 Awake에서 예외가 나면 그 몬스터는 통째로 죽은 오브젝트가 됩니다)
            pointB = (patrolPoint != null) ? patrolPoint.position : transform.position;
            pointB.y = transform.position.y;

            goalPoint = pointB;
        }

        private void OnEnable()
        {
            StartCoroutine(FiniteStateMachineCoroutine());
        }

        // 메인 코루틴 루프
        private IEnumerator FiniteStateMachineCoroutine()
        {
            // 기본상태를 넣어주고 루프를 시작한다.
            // IEnumerator
            // : 유니티의 코루틴 한정으로, 특정 코루틴의 진행상태를
            //  저장하는 변수라고 생각해주세요

            nextStateCoroutine = IdleStateCoroutine();

            // 게임 오브젝트가 켜져있다면 반복하는
            // 루프를 구성합니다
            while (gameObject.activeInHierarchy)
            {
                // yield return StartCoroutine(코루틴);
                // : 매개변수로 주어진 코루틴이 종료될때까지
                //  처리를 양보합니다. => 대기한다
                yield return StartCoroutine(nextStateCoroutine);
            }
        }

        private IEnumerator IdleStateCoroutine()
        {
            float waitTime = 0.0f;
            const float IDLE_WAIT_TIME = 3.0f; // 개발하실때 이런 변수는 밖으로 빼시는걸 추천

            WaitForSeconds term = new WaitForSeconds(baseUpdateTerm);

            while (true)
            {
                // Idle상태의 탈출조건

                // 1. 3초가 지났을때 PatrolState로 전환(Transition)
                if (IDLE_WAIT_TIME < waitTime)
                {
                    nextStateCoroutine = PatrolStateCoroutine();
                    yield break; // 코루틴 자체를 탈출하는 키워드 입니다
                }

                // 2. 타깃이 TraceRange안에 있을때 AttackState로 전환
                if (Target != null && Target.IsInRange(transform.position, traceRange))
                {
                    // 플레이어와 내가 같은 층에 있을때 (조건 검사)
                    if (CompareFloor(transform.position, Target.position) == 0)
                    {
                        nextStateCoroutine = AttackStateCoroutine();
                        yield break; // 코루틴 자체를 탈출하는 키워드 입니다
                    }
                }

                yield return term;
                waitTime += baseUpdateTerm;
            }
        }

        [SerializeField] private Transform patrolPoint;
        private Vector3 pointA, pointB, goalPoint; // y좌표를 내 좌표계로 바꿔줘야 한다.

        private IEnumerator PatrolStateCoroutine()
        {
            // Patrol은 목표지점(goalPoint)을 향해 움직입니다
            // 목표지점에 도달하면
            // 내 다음목적지 포인트를 갱신하고
            // Idle 상태로 전환 됩니다

            const float STOPPING_DISTANCE = 0.1f;

            while (true)
            {
                Vector3 adjustGoalPoint = transform.position;
                adjustGoalPoint.x = goalPoint.x;

                if (Target != null && Target.IsInRange(transform.position, traceRange))
                {
                    // 플레이어와 내가 같은 층에 있을때 (조건 검사)
                    if (CompareFloor(transform.position, Target.position) == 0)
                    {
                        nextStateCoroutine = AttackStateCoroutine();
                        yield break; // 코루틴 자체를 탈출하는 키워드 입니다
                    }
                }

                // 내가 현재 목표지점에 가까운지?
                // - 목표지점이 B인지 ?  A지점으로 갱신 : B지점으로 갱신

                // 목적지에 가까워졌으면
                if (transform.IsInRange(adjustGoalPoint, STOPPING_DISTANCE))
                {
                    // 삼항 연산자를 사용해서 pointB의 위치라면 pointA, 아니라면 pointB 바꿔준다\
                    // 내가 가까운 목표 지점에 따라서 해당 목표지점의 반대 지점으로 바꿔주기
                    goalPoint = transform.IsSamePosition(pointB, STOPPING_DISTANCE) ? pointA : pointB;
                    nextStateCoroutine = IdleStateCoroutine();
                    Animator.SetBool(IS_MOVE, false);
                    yield break;
                }

                Move(adjustGoalPoint);
                yield return null;
            }
        }

        private IEnumerator AttackStateCoroutine()
        {
            while (true)
            {
                // 1. Target이 사라질경우
                if (Target == null)
                {
                    nextStateCoroutine = IdleStateCoroutine();
                    Animator.SetBool(IS_MOVE, false);
                    yield break;
                }

                // 2. Target이 범위(추적 범위) 밖으로 이동할 경우
                //  : 타겟이 매우 빠르게 추적 가능한 범위 바깥으로 이동하게된 케이스
                if (Target.IsInRange(transform.position, traceRange) == false)
                {
                    nextStateCoroutine = IdleStateCoroutine();
                    Animator.SetBool(IS_MOVE, false);
                    yield break;
                }

                //======== 반복문 탈출 검사가 끝나면 =======
                // 공격하거나 (타겟이 사거리 안에 있으면)
                Vector3 adjustTargetPosition = Target.position;
                adjustTargetPosition.y = transform.position.y;

                if (transform.IsInRange(adjustTargetPosition, attackRange))
                {
                    Animator.SetBool(IS_MOVE, false);
                    Animator.SetTrigger(ATTACK);

                    // 공격 모션이 타격 프레임에 도달 할 때까지 대기한다
                    // - 정석은 애니메이션 이벤트 데이터등을 넣어서
                    // 다격 프레임을 정확하게 알아내는게 맞습니다.
                    // - 여기서는 간단하게 시간으로 처리합니다.

                    // 공격 판정을 위한 대기
                    yield return new WaitForSeconds(ATTACK_HIT_DELAY);

                    ProcessAttack();

                    // 공격 전체 쿨다운 대기
                    yield return new WaitForSeconds(ATTACK_COOLDOWN - ATTACK_HIT_DELAY);
                }
                else Move(Target.position); // 이동 한다(타겟이 사거리 안에 들어올때까지)

                yield return null;
            }
        }

        public void SetRoundManager(RoundManager manager)
        {
            roundManager = manager;
        }

        protected void Move(Vector3 goalPosition)
        {
            Animator.SetBool(IS_MOVE, true);
            // 1차원 방향(사이드뷰, 플랫포머 이니까)
            float moveDirection = UpdateDirection(goalPosition);
            transform.Translate(new Vector3(moveDirection, 0, 0) * (moveSpeed * Time.deltaTime));
        }

        /// <summary>
        /// 캐릭터의 방향을 업데이트하고, x축으로 전방 방향(-1, 1 부호)을 반환합니다
        /// </summary>
        /// <param name="direction"></param>
        /// <returns></returns>
        protected float UpdateDirection(Vector3 goalPosition)
        {
            float dirToGoal = goalPosition.x - transform.position.x;
            // 부호만 받아낸다.
            float moveDirection = Mathf.Sign(dirToGoal);
            // 방향에 맞춰서 오른쪽/왼쪽 전환(스케일 x 값을 이용)
            transform.localScale =
                (new Vector3(moveDirection * originalScale.x, originalScale.y, originalScale.z));
            return moveDirection;
        }

        [Header("공격력")]
        [SerializeField] private int attackMinDamage = 4;
        [SerializeField] private int attackMaxDamage = 7;

        /// <summary>이번 공격의 데미지를 뽑습니다. (Range.Range의 int 버전은 max가 배타적이라 +1)</summary>
        protected int RollAttackDamage()
        {
            return Random.Range(attackMinDamage, attackMaxDamage + 1);
        }

        /// <summary>
        /// 근접 몬스터의 타격 처리입니다.
        ///
        /// 왜 HitBox를 안 쓰는가:
        /// HitBox/HurtBox 방식은 애니메이션 프레임에 맞춰 콜라이더를 켜고 꺼야 해서
        /// 프리팹 세팅이 필요합니다. 이 FSM은 이미 ATTACK_HIT_DELAY로 "지금이 타격
        /// 프레임"임을 알고 있으므로, 그 순간 사거리를 다시 확인해서 CombatSystem으로
        /// 데미지를 직접 보냅니다. 판정의 최종 경로(CombatSystem.To)는 동일하므로
        /// 데미지 팝업 같은 옵저버들도 그대로 동작합니다.
        /// </summary>
        protected virtual void ProcessAttack()
        {
            if (Target == null) return;

            // 선딜(ATTACK_HIT_DELAY) 사이에 플레이어가 빠져나갔다면 헛스윙 처리합니다.
            Vector3 adjustTargetPosition = Target.position;
            adjustTargetPosition.y = transform.position.y;
            if (transform.IsInRange(adjustTargetPosition, attackRange) == false) return;

            CombatEntity receiver = Target.GetComponentInParent<CombatEntity>();
            if (receiver == null || Enemy == null) return;

            CombatEvent @event;
            @event.EventType = CombatEventType.DamageEvent;
            @event.Amount = RollAttackDamage();
            @event.Position = receiver.transform.position;

            CombatSystem.Instance.To(Enemy, receiver, @event);
        }

        // 내 Transform과 Target의 Transform의 y값을 비교하여
        // 같은 층에 있는지를 조회하는 함수.
        // a가 b보다 낮은 층에 있으면 -1을
        // a와 b가 갖은 층에 있으면 0을
        // a가 b보다 높은 층에 있으면 1을

        // Floor에 대한 정의 필요함

        // 프로젝트마다 정의가 달라져야함 
        private int CompareFloor(Vector3 a, Vector3 b)
        {
            const float EPSILON = 1.0f; // 천장고 이런 느낌의 변수
            float yDistance = a.y - b.y;

            if (Mathf.Abs(yDistance) <= EPSILON) return 0;
            else if (yDistance > 0) return 1;
            else return -1; //(yDistance < 0)
        }


        // 변동될 가능성이 높은것 같은디 허허
        [SerializeField] private GameObject deadEffect;
        [SerializeField] private Vector3 deadEffectOffset;
        [SerializeField] private float deadEffectLifeTime = 0.5f;

        public void Dead()
        {
            Enemy enemy = GetComponent<Enemy>();
            if (Player.LocalPlayer != null && enemy != null)
            {
                // 규칙: 빈 슬롯이 있으면 자동 흡수, 없으면 플레이어의 선택을 기다린다.
                Player.LocalPlayer.HandleMonsterDrop(enemy.DroppedWeaponInfo);
            }

            roundManager?.NotifyEnemyDefeated(this);

            // Enemy가 죽게 되면 죽음 이펙트를 생성하고, 스스로를 삭제합니다.
            // deadEffect가 비어 있으면 Instantiate가 예외를 던지고, 그러면 아래의
            // Destroy(gameObject)가 실행되지 않아 "죽었는데 사라지지 않는 시체"가
            // 남습니다. 연출은 없어도 되지만 삭제는 반드시 되어야 하므로 분리합니다.
            if (deadEffect != null)
            {
                GameObject effect = Instantiate(deadEffect,
                    transform.position + deadEffectOffset, Quaternion.identity);

                // 이펙트는 일정시간이 지난뒤에 자동으로 삭제 됩니다.
                // 여기서는 Destroy(삭제할 대상, 지연시간); 함수를 사용합니다.
                Destroy(effect, deadEffectLifeTime); // effect를 deadEffectLifeTime시간 이후에 삭제한다.
            }

            Destroy(gameObject);
        }
    }





}
