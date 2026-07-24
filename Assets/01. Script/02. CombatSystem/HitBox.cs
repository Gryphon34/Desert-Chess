using System.Collections.Generic;
using UnityEngine;

namespace Study_ActionPlatformer
{
    // # HitBox?
    // - 히트박스는 게임에서 캐릭터나 사물의 위치, 충돌, 피탄, 피격 판정을
    //  계산하기 위해 설정한 "가상의 상자" 입니다.
    // - HitBox(공격 판정) <-> HurtBox(피격판정) 구조로 보통 작성되며,
    // (HurtBox는 일반 콜라이더 써도 되긴함)
    // - 상자를 밀어내거나 부수는 판정도 포함됩니다.

    public class HitBox : MonoBehaviour
    {
        // 이 히트박스가 "무기용"인지 "마법용"인지 표시합니다.
        [field: SerializeField] public AttackSlotCategory Category { get; private set; } = AttackSlotCategory.Weapon;

        // 이 히트박스가 이번에 사용할 공격 데이터.
        public AttackInfo AttackInfo { get; private set; }

        [SerializeField] private float damageMultiplier = 1.0f;

        public void SetAttackInfo(AttackInfo info)
        {
            AttackInfo = info;
        }

        private int RollFinalDamage()
        {
            int baseDamage = AttackInfo.RollDamage();

            // "맞았는데 0 데미지"는 버그처럼 보이므로 최소 1을 보장합니다.
            return Mathf.Max(1, Mathf.RoundToInt(baseDamage * damageMultiplier));
        }

        // 이 히트박스의 주인. 부모 계층에서 찾아 보관한다
        private CombatEntity Owner { get; set; }

        // 이번 휘두름에서 이미 때린 대상들.
        private HashSet<CombatEntity> checkList = new HashSet<CombatEntity>();

        private void Awake()
        {
            //  GetComponentInParent<타입>() : 부모 게임오브젝트들을 탐색함녀 <타입>
            // 의 컴포넌트를 찾는다. 
            Owner = GetComponentInParent<CombatEntity>();

            if (Owner == null)
            {
                Debug.LogError($"{name} : 부모 계층에서 CombatEntity를 찾지 못했습니다." +
                    $"오브젝트를 삭제합니다");
                Destroy(gameObject);
            }
        }

        private void OnEnable()
        {
            // 히트박스가 다시 켜질때 (재사용 될때 checkList를 비워준다)
            checkList.Clear();
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            // 아래는 Legacy Code
            // HurtBox가 없을경우 종료한다. (Physics Layer 설정으로 아예 안들어오게 좋음)
            //if (collision.TryGetComponent<HurtBox>(out HurtBox other) == false) return;

            CombatEntity receiver = ResolveReceiver(collision);
            if (receiver == null) return;

            // 자기 자신은 때리지 않는다.
            if (receiver == Owner) return;

            // 이번 휘두름에서 이미 때린 대상이면 종료한다.
            if (checkList.Contains(receiver)) return;

            // 중복 체크 방지를 위해 HashSet에 추가한다
            checkList.Add(receiver);

            // 데미지 처리하는 로직.
            // 미리 준비해놓고
            CombatEntity sender = Owner;

            int damage = RollFinalDamage();

            // 담아주고
            CombatEvent @event;
            @event.EventType = CombatEventType.DamageEvent;
            @event.Amount = damage;
            @event.Position = collision.ClosestPoint(sender.transform.position);

            // 보낸다
            CombatSystem.Instance.To(sender, receiver, @event);
        }

        private CombatEntity ResolveReceiver(Collider2D collision)
        {
            HurtBox hurtBox = CombatSystem.Instance.GetHurtBoxOrNull(collision);
            if (hurtBox != null) return hurtBox.Owner;

            CombatEntity entity = collision.GetComponentInParent<CombatEntity>();
            if (entity == null) return null;

            Debug.LogWarning($"{entity.name} : HurtBox가 등록되어 있지 않습니다. " +
                $"계층 탐색으로 대신 처리했습니다. 프리팹에 HurtBox를 붙여주세요.");
            return entity;
        }
    }
}

