using System;
using UnityEngine;

namespace Study_ActionPlatformer
{
    // 스탯, 전투 관련 기능을 넣어놓을 겁니다. 그리고 어떤 개체에서든 Player를
    // 찾을 수 있는 기능을 만들겁니다.
    // CombatEntity를 상속 받았기 때문에 "전투에 참여하는 개체"의
    // 공통부(Pivot, TakeDamage 등 순수가상함수 계약)는
    // 물려 받고, 플레이어의 고유의 것만 여기 남습니다.

    public class Player : CombatEntity
    {
        private const int DEFAULT_SLOT_USES = 5;

        public static Player LocalPlayer { get; set; }

        public override BaseStat BaseStat => Stat;

        private PlayerStat Stat { get; set; }

        [SerializeField] private AttackInfo[] weaponSlots = new AttackInfo[3];
        [SerializeField] private AttackInfo[] magicSlots = new AttackInfo[3];

        public event Action<AttackInfo> MonsterAbsorbed;
        public event Action<AttackInfo> AbsorptionChoiceRequested;

        private AttackInfo? pendingAbsorption;
        private AttackSlotCategory? pendingAbsorptionCategory;

        public int ActiveWeaponSlot { get; private set; } = 0;
        public int ActiveMagicSlot { get; private set; } = 0;

        public AttackInfo ActiveWeaponInfo => weaponSlots[ActiveWeaponSlot];
        public AttackInfo ActiveMagicInfo => magicSlots[ActiveMagicSlot];

        private HitBox[] HitBoxes { get; set; }

        public AttackInfo attackInfo;

        private void Awake()
        {
            LocalPlayer = this;
            Stat ??= new PlayerStat();
            Stat.ResetToFull();

            InitializeSlotDefaults();
            HitBoxes = GetComponentsInChildren<HitBox>(true);

            if (weaponSlots.Length > 0)
            {
                attackInfo = weaponSlots[ActiveWeaponSlot];
            }

            SyncActiveWeaponInfoToHitBoxes();
        }

        public void SelectWeaponSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= weaponSlots.Length) return;

            ActiveWeaponSlot = slotIndex;
            attackInfo = weaponSlots[slotIndex];
            SyncActiveWeaponInfoToHitBoxes();
        }

        public bool TryConsumeActiveWeaponUse()
        {
            if (weaponSlots.Length == 0) return false;

            AttackInfo activeWeapon = weaponSlots[ActiveWeaponSlot];
            if (activeWeapon.RemainingUses <= 0)
            {
                weaponSlots[ActiveWeaponSlot] = CreateDefaultWeaponInfo();
                attackInfo = weaponSlots[ActiveWeaponSlot];
                SyncActiveWeaponInfoToHitBoxes();
                return false;
            }

            activeWeapon.RemainingUses -= 1;
            weaponSlots[ActiveWeaponSlot] = activeWeapon;
            attackInfo = activeWeapon;

            if (activeWeapon.RemainingUses <= 0)
            {
                weaponSlots[ActiveWeaponSlot] = CreateDefaultWeaponInfo();
                attackInfo = weaponSlots[ActiveWeaponSlot];
                ActiveWeaponSlot = 0;
            }

            SyncActiveWeaponInfoToHitBoxes();
            return true;
        }

        public bool TryConsumeActiveMagicUse()
        {
            if (magicSlots.Length == 0) return false;

            AttackInfo activeMagic = magicSlots[ActiveMagicSlot];
            if (activeMagic.RemainingUses <= 0)
            {
                magicSlots[ActiveMagicSlot] = CreateDefaultMagicInfo();
                SyncActiveMagicInfoToHitBoxes();
                return false;
            }

            activeMagic.RemainingUses -= 1;
            magicSlots[ActiveMagicSlot] = activeMagic;

            if (activeMagic.RemainingUses <= 0)
            {
                magicSlots[ActiveMagicSlot] = CreateDefaultMagicInfo();
                ActiveMagicSlot = 0;
            }

            SyncActiveMagicInfoToHitBoxes();
            return true;
        }

        public void SelectMagicSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= magicSlots.Length) return;

            ActiveMagicSlot = slotIndex;
            SyncActiveMagicInfoToHitBoxes();
        }

        public bool TryFireActiveMagic()
        {
            if (ActiveMagicInfo.Key == AttackKey.None)
                return false;

            return TryConsumeActiveMagicUse();
        }

        /// <summary>
        /// 몬스터 처치로 무기를 획득했을 때 호출하는 진입점입니다.
        /// 규칙: 빈 슬롯이 있으면 자동으로 흡수하고, 빈 슬롯이 없으면
        /// 플레이어의 선택을 기다립니다(AbsorptionChoiceRequested 이벤트 발행).
        /// </summary>
        public void HandleMonsterDrop(AttackInfo droppedWeapon)
        {
            if (droppedWeapon.Key == AttackKey.None) return;

            if (droppedWeapon.Category == AttackSlotCategory.Magic)
            {
                HandleMagicAbsorption(droppedWeapon);
                return;
            }

            HandleWeaponAbsorption(droppedWeapon);
        }

        private void HandleWeaponAbsorption(AttackInfo droppedWeapon)
        {
            int emptySlotIndex = FindEmptyWeaponSlot();
            if (emptySlotIndex >= 0)
            {
                weaponSlots[emptySlotIndex] = droppedWeapon;

                if (emptySlotIndex == ActiveWeaponSlot)
                {
                    attackInfo = droppedWeapon;
                    SyncActiveWeaponInfoToHitBoxes();
                }

                MonsterAbsorbed?.Invoke(droppedWeapon);
                return;
            }

            pendingAbsorption = droppedWeapon;
            pendingAbsorptionCategory = AttackSlotCategory.Weapon;
            AbsorptionChoiceRequested?.Invoke(droppedWeapon);
        }

        private void HandleMagicAbsorption(AttackInfo droppedWeapon)
        {
            int emptySlotIndex = FindEmptyMagicSlot();
            if (emptySlotIndex >= 0)
            {
                magicSlots[emptySlotIndex] = droppedWeapon;
                if (emptySlotIndex == ActiveMagicSlot)
                {
                    SyncActiveMagicInfoToHitBoxes();
                }

                MonsterAbsorbed?.Invoke(droppedWeapon);
                return;
            }

            pendingAbsorption = droppedWeapon;
            pendingAbsorptionCategory = AttackSlotCategory.Magic;
            AbsorptionChoiceRequested?.Invoke(droppedWeapon);
        }

        /// <summary>
        /// 빈 슬롯이 없을 때, 플레이어가 교체할 슬롯을 선택하면 UI가 호출합니다.
        /// </summary>
        public void ConfirmAbsorption(int slotIndexToReplace)
        {
            if (pendingAbsorption == null || pendingAbsorptionCategory == null) return;

            AttackInfo dropped = pendingAbsorption.Value;

            if (pendingAbsorptionCategory.Value == AttackSlotCategory.Magic)
            {
                if (slotIndexToReplace < 0 || slotIndexToReplace >= magicSlots.Length) return;
                magicSlots[slotIndexToReplace] = dropped;
                if (slotIndexToReplace == ActiveMagicSlot)
                {
                    SyncActiveMagicInfoToHitBoxes();
                }
            }
            else
            {
                if (slotIndexToReplace < 0 || slotIndexToReplace >= weaponSlots.Length) return;
                weaponSlots[slotIndexToReplace] = dropped;

                if (slotIndexToReplace == ActiveWeaponSlot)
                {
                    attackInfo = dropped;
                    SyncActiveWeaponInfoToHitBoxes();
                }
            }

            pendingAbsorption = null;
            pendingAbsorptionCategory = null;
            MonsterAbsorbed?.Invoke(dropped);
        }

        /// <summary>
        /// 빈 슬롯이 없을 때, 플레이어가 흡수를 포기하면 UI가 호출합니다.
        /// </summary>
        public void DeclineAbsorption()
        {
            pendingAbsorption = null;
            pendingAbsorptionCategory = null;
        }

        private int FindEmptyWeaponSlot()
        {
            for (int i = 0; i < weaponSlots.Length; ++i)
            {
                if (weaponSlots[i].Key == AttackKey.None)
                    return i;
            }

            return -1;
        }

        private int FindEmptyMagicSlot()
        {
            for (int i = 0; i < magicSlots.Length; ++i)
            {
                if (magicSlots[i].Key == AttackKey.None)
                    return i;
            }

            return -1;
        }

        // 기획서 5-1 : 시작 무기는 주먹 하나뿐입니다.
        // 무기 슬롯[0]에만 기본 주먹을 채우고, 나머지 무기 슬롯과 마법 슬롯은
        // (초기 마법이 없으므로) 흡수 전까지 빈 상태(Key == AttackKey.None)로 둡니다.
        private void InitializeSlotDefaults()
        {
            if (weaponSlots.Length > 0 && weaponSlots[0].Key == AttackKey.None)
            {
                weaponSlots[0] = CreateDefaultWeaponInfo();
            }

            if (magicSlots.Length > 0)
            {
                for (int i = 0; i < magicSlots.Length; ++i)
                {
                    magicSlots[i] = CreateDefaultMagicInfo();
                }
            }
        }

        private AttackInfo CreateDefaultWeaponInfo()
        {
            return new AttackInfo
            {
                Category = AttackSlotCategory.Weapon,
                Key = AttackKey.Combo1,
                MinDamage = 1,
                MaxDamage = 2,
                RemainingUses = DEFAULT_SLOT_USES,
                damageCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f),
            };
        }

        private AttackInfo CreateDefaultMagicInfo()
        {
            return new AttackInfo
            {
                Category = AttackSlotCategory.Magic,
                Key = AttackKey.None,
                MinDamage = 0,
                MaxDamage = 0,
                RemainingUses = 0,
                damageCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f),
            };
        }

        private void SyncActiveWeaponInfoToHitBoxes()
        {
            if (HitBoxes == null || HitBoxes.Length == 0) return;

            for (int i = 0; i < HitBoxes.Length; ++i)
            {
                // 마법용 히트박스까지 무기 데이터로 덮어쓰지 않도록 카테고리를 확인한다.
                if (HitBoxes[i].Category != AttackSlotCategory.Weapon) continue;
                HitBoxes[i].SetAttackInfo(attackInfo);
            }
        }

        private void SyncActiveMagicInfoToHitBoxes()
        {
            if (HitBoxes == null || HitBoxes.Length == 0) return;

            for (int i = 0; i < HitBoxes.Length; ++i)
            {
                // 무기용 히트박스까지 마법 데이터로 덮어쓰지 않도록 카테고리를 확인한다.
                if (HitBoxes[i].Category != AttackSlotCategory.Magic) continue;
                HitBoxes[i].SetAttackInfo(ActiveMagicInfo);
            }
        }

        public int WeaponSlotCount => weaponSlots.Length;
        public int MagicSlotCount => magicSlots.Length;

        public AttackInfo GetWeaponSlot(int index) => weaponSlots[index];
        public AttackInfo GetMagicSlot(int index) => magicSlots[index];

        /// <summary>
        /// 보상으로 무기 슬롯을 강화합니다. 공격력은 기획서 스탯 범위(1~10)로 클램프합니다.
        /// </summary>
        public void EnhanceWeaponSlot(int slotIndex, int damageBoost, int usesBoost)
        {
            if (slotIndex < 0 || slotIndex >= weaponSlots.Length) return;

            AttackInfo info = weaponSlots[slotIndex];
            if (info.Key == AttackKey.None) return;

            info.MinDamage = Mathf.Clamp(info.MinDamage + damageBoost, 1, 10);
            info.MaxDamage = Mathf.Clamp(info.MaxDamage + damageBoost, 1, 10);
            info.RemainingUses += usesBoost;
            weaponSlots[slotIndex] = info;

            if (slotIndex == ActiveWeaponSlot)
            {
                attackInfo = info;
                SyncActiveWeaponInfoToHitBoxes();
            }
        }

        /// <summary>
        /// 보상으로 마법 슬롯을 강화합니다. 공격력은 기획서 스탯 범위(1~10)로 클램프합니다.
        /// </summary>
        public void EnhanceMagicSlot(int slotIndex, int damageBoost, int usesBoost)
        {
            if (slotIndex < 0 || slotIndex >= magicSlots.Length) return;

            AttackInfo info = magicSlots[slotIndex];
            if (info.Key == AttackKey.None) return;

            info.MinDamage = Mathf.Clamp(info.MinDamage + damageBoost, 1, 10);
            info.MaxDamage = Mathf.Clamp(info.MaxDamage + damageBoost, 1, 10);
            info.RemainingUses += usesBoost;
            magicSlots[slotIndex] = info;

            if (slotIndex == ActiveMagicSlot)
            {
                SyncActiveMagicInfoToHitBoxes();
            }
        }

        public override void TakeDamage(int damage)
        {
            if (Stat == null)
            {
                Stat = new PlayerStat();
            }

            Stat.ApplyDamage(damage);
        }

        public override void TakeHeal(int heal)
        {
            if (Stat == null)
            {
                Stat = new PlayerStat();
            }

            Stat.ApplyHeal(heal);
        }
    }

}
