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
        protected float AttackRange => attackRange;
        [SerializeField] private float baseUpdateTerm = 0.1f;

        // 지형 처리
        // 예전에는 transform.Translate로 좌우만 움직여서 벽을 통과하고, 발판 끝에서도
        // 허공으로 계속 걸어나가 맵 밖으로 벗어났습니다(중력도 없었습니다).
        // 이제 벽/낭떠러지 앞에서 멈추고, 발밑이 비면 떨어집니다.
        [Header("지형")]
        [Tooltip("비워두면 'Ground' 레이어를 사용합니다.")]
        [SerializeField] private LayerMask groundLayer;
        [SerializeField] private float gravity = -20f;
        [SerializeField] private float maxFallSpeed = 20f;
        [Tooltip("앞쪽 발밑으로 이 깊이 안에 바닥이 없으면 낭떠러지로 보고 멈춥니다.")]
        [SerializeField] private float ledgeCheckDepth = 0.6f;

        private const float SKIN_WIDTH = 0.02f;

        // 몸통 충돌 박스(루트 기준 오프셋/크기). 좌우 반전(scale.x)과 무관하게 가운데 정렬이라
        // Awake에서 한 번만 계산합니다.
        private Vector2 bodyOffset = new Vector2(0f, 0.72f);
        private Vector2 bodySize = new Vector2(1.2f, 1.44f);
        private float verticalVelocity = 0f;
        private bool isGrounded = false;

        // 다른 층 추적
        // 플레이어가 아래층이면 발판 끝에서 뛰어내리고, 위층이면 점프해서 따라갑니다.
        // 길찾기가 아니라 "막히면 점프, 계속 실패하면 반대로 돌아가기" 방식의 단순한 규칙입니다.
        [Header("다른 층 추적")]
        [SerializeField] private bool chaseAcrossFloors = true;
        [Tooltip("점프로 올라갈 수 있는 높이(유닛)")]
        [SerializeField] private float jumpHeight = 3f;
        [SerializeField] private float jumpCooldown = 0.6f;
        [Tooltip("플레이어가 위에 있고 좌우 거리가 이 안이면 바로 점프합니다.")]
        [SerializeField] private float jumpTriggerDistance = 2.5f;
        [Tooltip("이 횟수만큼 점프해도 더 올라가지 못하면 반대 방향으로 돌아가 다른 길을 찾습니다.")]
        [SerializeField] private int failedJumpsBeforeDetour = 2;
        [Tooltip("이 시간 동안 제자리면 막힌 것으로 보고 반대 방향으로 돌아갑니다.")]
        [SerializeField] private float stuckTime = 1.0f;
        [SerializeField] private float detourDuration = 1.5f;

        private const float JUMP_PROGRESS_HEIGHT = 0.5f;

        private float lastJumpTime = float.NegativeInfinity;
        private bool isJumping = false;
        private float jumpStartY;
        private int failedJumps = 0;
        private float stuckTimer = 0f;
        private float detourUntil = float.NegativeInfinity;
        private float detourDirection = 1f;





        // 인스펙터에서 직접 지정할 수도 있고(테스트 씬), 비워두면 런타임에 자동으로
        // 플레이어를 찾습니다(RoundManager가 스폰하는 경우).
        //
        // FormerlySerializedAs : 예전 필드명이 "Target"이었기 때문에 붙였습니다.
        // 이게 없으면 이미 씬에 저장돼 있던 Target 연결이 전부 끊깁니다.
        [FormerlySerializedAs("Target")]
        [SerializeField] private Transform target;

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

            CacheBody();
            if (groundLayer.value == 0) groundLayer = LayerMask.GetMask("Ground");
        }

        private void CacheBody()
        {
            Collider2D[] colliders = GetComponentsInChildren<Collider2D>();
            for (int i = 0; i < colliders.Length; ++i)
            {
                if (colliders[i].isTrigger) continue;

                Bounds b = colliders[i].bounds;
                if (b.size.x <= 0f || b.size.y <= 0f) continue;

                bodyOffset = b.center - transform.position;
                bodySize = b.size;
                return;
            }
        }

        private void Update()
        {
            ApplyGravity();
        }

        // 발밑이 비어 있으면 떨어지고, 바닥에 닿으면 멈춥니다. 점프 중에는 천장에 막힙니다.
        // (스폰 지점이 바닥보다 살짝 위라서, 이게 없으면 공중에 떠 있게 됩니다)
        private void ApplyGravity()
        {
            verticalVelocity = Mathf.Max(verticalVelocity + gravity * Time.deltaTime, -maxFallSpeed);
            float delta = verticalVelocity * Time.deltaTime;
            if (delta == 0f) return;

            Vector2 dir = delta > 0f ? Vector2.up : Vector2.down;
            float distance = Mathf.Abs(delta);

            RaycastHit2D hit = Physics2D.BoxCast(BodyCenter, CastSize, 0f, dir,
                distance + SKIN_WIDTH, groundLayer);

            bool wasGrounded = isGrounded;
            if (hit.collider != null)
            {
                distance = Mathf.Max(0f, hit.distance - SKIN_WIDTH);
                verticalVelocity = 0f;
                isGrounded = delta < 0f;
            }
            else
            {
                isGrounded = false;
            }

            transform.position += (Vector3)(dir * distance);

            if (wasGrounded == false && isGrounded) OnLanded();
        }

        // 점프로 실제로 더 높은 곳에 올라갔는지 확인합니다. 천장에 부딪혀 제자리에 내려왔다면
        // 실패로 셉니다.
        private void OnLanded()
        {
            if (isJumping == false) return;
            isJumping = false;

            if (transform.position.y > jumpStartY + JUMP_PROGRESS_HEIGHT) failedJumps = 0;
            else failedJumps += 1;
        }

        private bool TryJump()
        {
            if (isGrounded == false) return false;
            if (Time.time < lastJumpTime + jumpCooldown) return false;

            verticalVelocity = Mathf.Sqrt(2f * Mathf.Abs(gravity) * jumpHeight);
            isGrounded = false;
            isJumping = true;
            jumpStartY = transform.position.y;
            lastJumpTime = Time.time;
            return true;
        }

        private Vector2 BodyCenter => (Vector2)transform.position + bodyOffset;
        private Vector2 CastSize => new Vector2(bodySize.x - SKIN_WIDTH * 2f, bodySize.y - SKIN_WIDTH * 2f);

        // 이번 프레임에 moveDirection 쪽으로 갈 수 있는 거리를 돌려줍니다.
        // 벽에 막히면 벽 앞까지만 갑니다. 앞쪽 발밑이 비어 있으면(낭떠러지) allowDrop일 때만
        // 걸어 나가고, 아니면 0입니다. 공중에서는 벽만 확인합니다(점프 중 좌우 이동).
        private float ResolveHorizontalMove(float moveDirection, float distance, bool allowDrop,
            out bool hitWall, out bool atLedge)
        {
            hitWall = false;
            atLedge = false;

            Vector2 dir = new Vector2(moveDirection, 0f);
            RaycastHit2D wall = Physics2D.BoxCast(BodyCenter, CastSize, 0f, dir,
                distance + SKIN_WIDTH, groundLayer);
            if (wall.collider != null)
            {
                distance = Mathf.Max(0f, wall.distance - SKIN_WIDTH);
                hitWall = true;
            }

            if (isGrounded == false) return distance;

            Vector2 center = BodyCenter;
            Vector2 frontFoot = new Vector2(
                center.x + moveDirection * (bodySize.x * 0.5f + distance + SKIN_WIDTH),
                center.y - bodySize.y * 0.5f + SKIN_WIDTH);
            RaycastHit2D floor = Physics2D.Raycast(frontFoot, Vector2.down, ledgeCheckDepth, groundLayer);
            if (floor.collider == null)
            {
                atLedge = true;
                if (allowDrop == false) return 0f;
            }

            return distance;
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
                    // 같은 층에 있을 때, 또는 다른 층 추적이 켜져 있을 때 추적을 시작합니다.
                    if (chaseAcrossFloors || IsSameFloorAsTarget())
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
                    // 같은 층에 있을 때, 또는 다른 층 추적이 켜져 있을 때 추적을 시작합니다.
                    if (chaseAcrossFloors || IsSameFloorAsTarget())
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

                // 같은 층일 때만 공격합니다. y를 맞춰 거리를 재기 때문에, 이 조건이 없으면
                // 바로 위/아래층에 있는 플레이어도 좌우 거리만 가까우면 공격했습니다.
                if (transform.IsInRange(adjustTargetPosition, attackRange) && IsSameFloorAsTarget())
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
                else ChaseMove(Target.position); // 이동 한다(타겟이 사거리 안에 들어올때까지)

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
            if (isGrounded == false) return; // 순찰 중에는 공중에서 움직이지 않습니다.

            float distance = ResolveHorizontalMove(moveDirection, moveSpeed * Time.deltaTime,
                false, out _, out _);
            transform.position += new Vector3(moveDirection * distance, 0f, 0f);
        }

        /// <summary>
        /// 플레이어를 쫓아 이동합니다. 같은 층이면 Move와 같고, 다른 층이면
        /// - 플레이어가 아래: 발판 끝에서 뛰어내립니다.
        /// - 플레이어가 위: 벽/발판 끝에 막히거나 플레이어 바로 아래에 오면 점프합니다.
        /// 점프가 계속 실패하거나(천장) 한자리에 막혀 있으면 잠시 반대 방향으로 돌아갑니다.
        /// </summary>
        protected void ChaseMove(Vector3 targetPosition)
        {
            Animator.SetBool(IS_MOVE, true);

            int floor = chaseAcrossFloors ? CompareFloor(transform.position, targetPosition) : 0;
            bool targetAbove = floor < 0;
            bool targetBelow = floor > 0;

            float toTarget = targetPosition.x - transform.position.x;
            bool detouring = Time.time < detourUntil;
            float moveDirection = detouring ? detourDirection : Mathf.Sign(toTarget);
            UpdateDirection(transform.position + Vector3.right * moveDirection);

            float wanted = moveSpeed * Time.deltaTime;
            float moved = ResolveHorizontalMove(moveDirection, wanted, targetBelow,
                out bool hitWall, out bool atLedge);
            transform.position += new Vector3(moveDirection * moved, 0f, 0f);

            if (isGrounded)
            {
                bool shouldJump =
                    (targetAbove && (hitWall || atLedge || Mathf.Abs(toTarget) <= jumpTriggerDistance)) ||
                    (floor == 0 && hitWall); // 같은 층이어도 낮은 턱은 넘어갑니다.

                if (shouldJump) TryJump();
            }

            UpdateStuck(moveDirection, wanted, moved, targetAbove);
        }

        private void UpdateStuck(float moveDirection, float wanted, float moved, bool targetAbove)
        {
            if (Time.time < detourUntil) return;

            bool repeatedFailedJumps = targetAbove && failedJumps >= failedJumpsBeforeDetour;

            if (isGrounded && moved < wanted * 0.1f) stuckTimer += Time.deltaTime;
            else stuckTimer = 0f;

            if (repeatedFailedJumps || stuckTimer >= stuckTime)
            {
                detourDirection = -moveDirection;
                detourUntil = Time.time + detourDuration;
                failedJumps = 0;
                stuckTimer = 0f;
            }
        }

        private bool IsSameFloorAsTarget()
        {
            return Target != null && CompareFloor(transform.position, Target.position) == 0;
        }

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

        [Header("공격력 폴백")]
        [Tooltip("평소에는 쓰이지 않습니다. weapons.tsv를 읽지 못했거나 몬스터에게 무기가 " +
            "지정되지 않은 예외 상황에서만 이 값이 사용됩니다.")]
        [SerializeField] private int attackMinDamage = 4;
        [SerializeField] private int attackMaxDamage = 7;

        /// <summary>
        /// 이번 공격의 데미지를 뽑습니다.
        ///
        /// 몬스터는 "자기가 들고 있는 무기"로 공격합니다. 그 무기는 처치했을 때
        /// 플레이어가 흡수하는 무기(Enemy.DroppedWeaponInfo)와 같은 것이고,
        /// 수치는 weapons.tsv(AttackTable)에서 옵니다.
        /// 덕분에 "화면에 보이는 몬스터의 위력 = 잡았을 때 얻는 무기의 위력"이 되어
        /// 기획서 4번(라운드별 몬스터 능력 = 랜덤)이 자연스럽게 지켜집니다.
        /// </summary>
        protected int RollAttackDamage()
        {
            // 표가 정상이면 항상 이쪽으로 갑니다.
            if (Enemy != null && Enemy.DroppedWeaponInfo.IsEmpty == false)
            {
                return Enemy.DroppedWeaponInfo.RollDamage();
            }

            // 표를 못 읽은 경우까지 데미지가 0이 되면 "때려도 안 아픈 몬스터"가 되므로,
            // 인스펙터 값으로 대체합니다. (Random.Range의 int 버전은 max가 배타적이라 +1)
            return Random.Range(attackMinDamage, attackMaxDamage + 1);
        }

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

        // 보스 라운드의 잡몹(SpawnMinionsState가 스폰)인지 표시합니다. 0이면 일반 몬스터입니다.
        // 일반 몬스터는 RoundManager.SpawnEnemy()를 거쳐 스폰되지만, 잡몹은 SpawnMinionsState가
        // 직접 Instantiate하므로 roundManager 참조가 없습니다 — 그래서 별도 표식이 필요합니다.
        private int chargeMinionAmount = 0;

        /// <summary>
        /// 이 몬스터를 "충전용 잡몹"으로 표시합니다. 처치 시 플레이어의 활성 슬롯을
        /// rechargeAmount만큼 충전합니다(기획서 9번). SpawnMinionsState가 스폰 직후 호출합니다.
        /// </summary>
        public void MarkAsChargeMinion(int rechargeAmount)
        {
            chargeMinionAmount = rechargeAmount;
        }

        public void Dead()
        {
            Enemy enemy = GetComponent<Enemy>();
            if (Player.LocalPlayer != null && enemy != null)
            {
                // 규칙: 빈 슬롯이 있으면 자동 흡수, 없으면 플레이어의 선택을 기다린다.
                Player.LocalPlayer.HandleMonsterDrop(enemy.DroppedWeaponInfo);

                if (chargeMinionAmount > 0)
                {
                    Player.LocalPlayer.RechargeActiveSlots(chargeMinionAmount);
                }
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
