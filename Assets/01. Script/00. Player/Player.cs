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
        // 주먹처럼 "사라지면 안 되는" 기본 무기를 표시하는 값입니다.
        // 5회 제한은 흡수해서 얻은 무기/마법에만 적용됩니다.
        public const int UNLIMITED_USES = -1;

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

        // 현재 사용 중인 무기 데이터의 캐시입니다. weaponSlots[ActiveWeaponSlot]에서
        // 파생되는 값이므로 인스펙터에서 따로 채우면 안 됩니다.
        // (예전에는 public 직렬화 필드라 프리팹에 450~800 같은 값이 남아 있었는데,
        //  Awake에서 어차피 덮어써서 쓰이지 않는 죽은 데이터였습니다)
        private AttackInfo attackInfo;

        public AttackInfo CurrentAttackInfo => attackInfo;

        private void Awake()
        {
            LocalPlayer = this;
            Stat ??= new PlayerStat();
            Stat.EnsureMaxHp(PlayerStat.DEFAULT_MAX_HP);
            Stat.ResetToFull();

            InitializeSlotDefaults();
            HitBoxes = GetComponentsInChildren<HitBox>(true);

            if (weaponSlots.Length > 0)
            {
                attackInfo = weaponSlots[ActiveWeaponSlot];
            }

            SyncActiveWeaponInfoToHitBoxes();

            // 마법 히트박스도 시작 시 한 번 동기화해 둡니다.
            // 시작 시엔 마법 슬롯이 비어 있어서 당장은 티가 안 나지만, 이걸 빼두면
            // 무기/마법 초기화가 비대칭이 되어 나중에 "마법만 첫 발이 이상하다" 같은
            // 추적하기 어려운 버그의 씨앗이 됩니다.
            SyncActiveMagicInfoToHitBoxes();
        }

        private void OnDestroy()
        {
            // 정적 참조를 그대로 두면 파괴된 플레이어를 가리키는 값이 남습니다.
            // 에디터에서 "Enter Play Mode Options"로 도메인 리로드를 꺼두면 이 값이
            // 다음 실행까지 살아남아, 몬스터들이 존재하지 않는 플레이어를 쫓는
            // 기묘한 버그가 생깁니다. 자기 자신일 때만 비웁니다.
            if (LocalPlayer == this) LocalPlayer = null;
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

            // 주먹(무제한)은 차감하지 않고 항상 사용 가능합니다.
            if (activeWeapon.RemainingUses == UNLIMITED_USES) return true;

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
                // 소진된 무기는 그 자리에서 기본 무기(주먹)로 되돌립니다.
                // 여기서 ActiveWeaponSlot을 0으로 바꾸면 "활성 슬롯 번호"와 "실제 사용 중인
                // 무기"가 서로 다른 슬롯을 가리키게 되므로 인덱스는 건드리지 않습니다.
                weaponSlots[ActiveWeaponSlot] = CreateDefaultWeaponInfo();
                attackInfo = weaponSlots[ActiveWeaponSlot];
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
                // 소진된 마법은 슬롯을 비웁니다(기본 마법은 존재하지 않음).
                // 무기와 같은 이유로 ActiveMagicSlot은 그대로 둡니다.
                magicSlots[ActiveMagicSlot] = CreateDefaultMagicInfo();
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

        // 기획서 5-1 : 시작 무기는 주먹(기본 공격력 3). 수치는 WeaponLibrary가 관리합니다.
        // 주먹은 "무기가 없을 때 돌아오는 자리"이므로 횟수 제한이 없습니다.
        private AttackInfo CreateDefaultWeaponInfo()
        {
            AttackInfo fist = WeaponLibrary.CreateFist();
            fist.RemainingUses = UNLIMITED_USES;
            return fist;
        }

        // 마법은 시작 시 보유한 것이 없으므로 "빈 슬롯"을 뜻하는 값을 돌려줍니다.
        private AttackInfo CreateDefaultMagicInfo()
        {
            return new AttackInfo
            {
                Id = WeaponId.None,
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
