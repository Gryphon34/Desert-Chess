// Assets/01. Script/01. Enemy/MagicController.cs (신규 파일)
using System.Collections;
using UnityEngine;
using Study.Utilities;

namespace Study_ActionPlatformer
{
    /// 마법 몬스터의 공격 처리
    /// 사거리 안에서 즉시 데미지, 넉백 함께 적용
    public class MagicController : EnemyController
    {
        [Header("마법 효과")]
        [SerializeField] private float knockbackSpeed = 6f;
        [SerializeField] private float knockbackDuration = 0.2f;

        protected override void ProcessAttack()
        {
            if (Target == null) return;

            // 선딜 사이에 플레이어가 빠져나갔다면 헛스윙 처리합니다.
            Vector3 adjustTargetPosition = Target.position;
            adjustTargetPosition.y = transform.position.y;
            if (transform.IsInRange(adjustTargetPosition, AttackRange) == false) return;

            CombatEntity receiver = Target.GetComponentInParent<CombatEntity>();
            if (receiver == null || Enemy == null) return;

            CombatEvent @event;
            @event.EventType = CombatEventType.DamageEvent;
            @event.Amount = RollAttackDamage();
            @event.Position = receiver.transform.position;

            CombatSystem.Instance.To(Enemy, receiver, @event);

            ApplyKnockback(receiver);
        }

        /// 맞은 대상을 밀어냅니다. CharacterController2D를 쓰는 대상(지금은 플레이어)만
        /// 처리하고, 없으면 조용히 넘어갑니다.
        private void ApplyKnockback(CombatEntity receiver)
        {
            CharacterController2D controller = receiver.GetComponent<CharacterController2D>();
            if (controller == null) return;

            float direction = Mathf.Sign(receiver.transform.position.x - transform.position.x);
            StartCoroutine(KnockbackCoroutine(controller, direction));
        }

        private IEnumerator KnockbackCoroutine(CharacterController2D controller, float direction)
        {
            // AddExternalMovement는 한 프레임짜리 이동량이라, 여러 프레임에 걸쳐
            // 나눠 더해줘야 함.(한 번에 큰 값을 넣으면 순간이동)
            float elapsed = 0f;
            while (elapsed < knockbackDuration)
            {
                controller.AddExternalMovement(new Vector3(direction * knockbackSpeed * Time.deltaTime, 0f, 0f));
                elapsed += Time.deltaTime;
                yield return null;
            }
        }
    }
}