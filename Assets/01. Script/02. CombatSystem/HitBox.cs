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
        // Player의 두 Sync 함수가 이 값을 보고 자기 몫의 히트박스만 갱신합니다.
        // (근접 무기 히트박스와 마법 히트박스가 같은 부모 밑에 같이 있을 경우
        //  서로의 AttackInfo를 덮어쓰지 않도록 하기 위함입니다)
        [field: SerializeField] public AttackSlotCategory Category { get; private set; } = AttackSlotCategory.Weapon;

        // 이 히트박스가 이번에 사용할 공격 데이터.
        //
        // [기획서 5-3, 5-4] 데미지의 출처는 "플레이어가 장착한 슬롯" 하나뿐입니다.
        // 예전에는 이 값을 인스펙터에서 직접 넣었는데(콤보1 = 100~150 등), 실제로는
        // Player.SyncActiveWeaponInfoToHitBoxes()가 매번 덮어써서 그 값이 쓰이지
        // 않았습니다. 인스펙터에 남아 있는 죽은 숫자가 기획서(공격력 1~10)와 달라
        // 혼란만 주므로, 아예 직렬화하지 않고 "런타임 주입 전용"으로 못 박습니다.
        public AttackInfo AttackInfo { get; private set; }

        // 콤보 단계별 위력 차이는 여기서 만듭니다.
        // 무기가 무엇이든 콤보1 < 콤보2 < 콤보3 < 점프공격 순으로 세지도록,
        // 슬롯의 기본 데미지에 이 배율을 곱합니다.
        // (예전 프리팹의 100/150/200/250이 담고 있던 의도를 배율로 옮긴 것입니다)
        [SerializeField] private float damageMultiplier = 1.0f;

        public void SetAttackInfo(AttackInfo info)
        {
            AttackInfo = info;
        }

        /// <summary>슬롯 데미지에 콤보 단계 배율을 적용한 최종 데미지를 뽑습니다.</summary>
        private int RollFinalDamage()
        {
            int baseDamage = AttackInfo.RollDamage();

            // 배율을 곱한 뒤 반올림하면 0이 될 수 있습니다.
            // "맞았는데 0 데미지"는 버그처럼 보이므로 최소 1을 보장합니다.
            return Mathf.Max(1, Mathf.RoundToInt(baseDamage * damageMultiplier));
        }

        // 이 히트박스의 주인. 부모 계층에서 찾아 보관한다
        private CombatEntity Owner { get; set; }

        // 이번 휘두름에서 이미 때린 대상들.
        // 연속 공격을 방지하기 위해 HashSet을 사용합니다. (List를 이용해도 상관없습니다)
        // HurtBox가 아니라 CombatEntity를 담는 이유:
        // 한 개체에 HurtBox가 여러 개(예: 보스의 부위별 콜라이더) 달려 있어도
        // "한 번 휘두르면 한 번만 맞는다"를 개체 단위로 보장하기 위해서입니다.
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

            // [기획서 9번] 사용 횟수 차감은 여기서 하지 않습니다.
            // 여기(명중 시점)에서 차감하면 광역 공격으로 3명을 맞힐 때 한 번 휘두르고
            // 3회가 소모됩니다. "5번 사용"은 명중 수가 아니라 공격 횟수이므로,
            // 차감은 공격 입력을 받는 PlayerController/AttackState가 책임집니다.
            // (덤으로, 차감이 AttackInfo를 갈아끼우는 바람에 마지막 일격이 주먹
            //  데미지로 들어가던 순서 문제도 함께 사라집니다)

            int damage = RollFinalDamage();

            // 담아주고
            CombatEvent @event;
            @event.EventType = CombatEventType.DamageEvent;
            @event.Amount = damage;
            @event.Position = collision.ClosestPoint(sender.transform.position);

            // 보낸다
            CombatSystem.Instance.To(sender, receiver, @event);
        }

        /// <summary>
        /// 충돌한 콜라이더로부터 "맞은 개체"를 찾아냅니다.
        ///
        /// 1순위는 CombatSystem에 등록된 HurtBox입니다. 충돌은 매우 빈번하게 일어나므로
        /// Dictionary 조회(O(1))가 GetComponent 계열 탐색보다 훨씬 쌉니다.
        ///
        /// 2순위(폴백)는 계층 탐색입니다. HurtBox 컴포넌트를 아직 붙이지 않은 프리팹
        /// (예: 보스)이라도 전투가 성립하도록 구제해 주되, 세팅이 빠졌다는 사실을
        /// 경고로 남겨서 조용히 넘어가지 않게 합니다.
        /// </summary>
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

