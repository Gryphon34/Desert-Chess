using System;
using System.Collections.Generic;
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

        // 기획서 9번 : 흡수한 무기/마법은 5회를 쓰면 소멸합니다.
        // 잡몹 처치로 충전할 때도 이 값을 넘지 않습니다.
        // (예전에는 WeaponLibrary.DEFAULT_USES가 이 역할을 했지만, 스탯이
        //  weapons.tsv로 옮겨가면서 도감 클래스가 사라져 여기로 옮겼습니다)
        public const int MAX_USES = 5;

        public static Player LocalPlayer { get; set; }

        public override BaseStat BaseStat => Stat;

        public PlayerStat Stat { get; private set; }

        [SerializeField] private AttackInfo[] weaponSlots = new AttackInfo[3];
        [SerializeField] private AttackInfo[] magicSlots = new AttackInfo[3];

        public event Action<AttackInfo> MonsterAbsorbed;
        public event Action<AttackInfo> AbsorptionChoiceRequested;

        // HUD가 매 프레임 값을 확인하지 않도록, 체력/슬롯이 바뀔 때마다 알립니다.
        public event Action HpChanged;
        public event Action SlotsChanged;
        // 발동 중인 계열 시너지가 바뀌었을 때
        public event Action SynergiesChanged;

        // 지금 발동 중인 시너지(synergies.tsv 행)
        private readonly List<SynergyDef> activeSynergies = new List<SynergyDef>();
        public IReadOnlyList<SynergyDef> ActiveSynergies => activeSynergies;

        // 흡수 선택을 기다리는 무기들. 한 번에 여러 마리가 죽어도 하나씩 차례로 묻습니다.
        private readonly Queue<AttackInfo> pendingAbsorptions = new Queue<AttackInfo>();

        public bool HasPendingAbsorption => pendingAbsorptions.Count > 0;
        public AttackInfo PendingAbsorption => pendingAbsorptions.Peek();

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
            NotifySlotsChanged();
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
                NotifySlotsChanged();
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
            NotifySlotsChanged();
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
                NotifySlotsChanged();
                return false;
            }

            activeMagic.RemainingUses -= 1;

            // 이번 공격이 실제로 쓸 데미지 값(activeMagic)을 먼저 히트박스에 동기화하고,
            // 그 다음에야 슬롯을 소진 처리합니다. 예전에는 순서가 반대라 마지막 한 발이
            // 나가기도 전에 슬롯이 빈 마법(MinDamage=0)으로 바뀌어 버렸고, 그 빈 값이
            // 히트박스에 들어가 "마지막 발은 애니메이션만 나가고 데미지가 없는" 결과였습니다.
            magicSlots[ActiveMagicSlot] = activeMagic;
            SyncActiveMagicInfoToHitBoxes();

            if (activeMagic.RemainingUses <= 0)
            {
                // 소진된 마법은 슬롯을 비웁니다(기본 마법은 존재하지 않음).
                // 무기와 같은 이유로 ActiveMagicSlot은 그대로 둡니다.
                // 방금 동기화한 값은 이미 히트박스로 넘어갔으므로, 여기서 슬롯을
                // 비워도 이번 공격에는 영향이 없습니다.
                magicSlots[ActiveMagicSlot] = CreateDefaultMagicInfo();
            }

            NotifySlotsChanged();
            return true;
        }

        public void SelectMagicSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= magicSlots.Length) return;

            ActiveMagicSlot = slotIndex;
            SyncActiveMagicInfoToHitBoxes();
            NotifySlotsChanged();
        }

        public bool TryFireActiveMagic()
        {
            if (ActiveMagicInfo.Key == AttackKey.None)
                return false;

            return TryConsumeActiveMagicUse();
        }

        /// <summary>
        /// 몬스터 처치로 무기를 획득했을 때 호출하는 진입점입니다.
        /// 기획서 6-2 : 처치 후 흡수 여부를 선택합니다(빈 슬롯이 있어도 묻습니다).
        /// 선택은 UI가 AcceptAbsorption / ConfirmAbsorption / DeclineAbsorption으로 알려줍니다.
        /// </summary>
        public void HandleMonsterDrop(AttackInfo droppedWeapon)
        {
            if (droppedWeapon.Key == AttackKey.None) return;

            // 선택 UI가 없으면 선택이 영원히 끝나지 않으므로 예전 규칙(빈 슬롯이면 흡수, 아니면 포기)으로 처리합니다.
            if (AbsorptionChoiceRequested == null)
            {
                int emptySlot = FindEmptySlot(droppedWeapon.Category);
                if (emptySlot >= 0) PlaceAbsorbed(droppedWeapon, emptySlot);
                return;
            }

            pendingAbsorptions.Enqueue(droppedWeapon);

            // 이미 다른 선택을 기다리는 중이면 그 선택이 끝난 뒤 차례로 묻습니다.
            if (pendingAbsorptions.Count == 1)
            {
                AbsorptionChoiceRequested.Invoke(droppedWeapon);
            }
        }

        /// <summary>대기 중인 무기의 카테고리에 빈 슬롯이 있는지(있으면 교체 없이 흡수 가능).</summary>
        public bool HasEmptySlotFor(AttackSlotCategory category) => FindEmptySlot(category) >= 0;

        /// <summary>
        /// 흡수 선택 : 빈 슬롯에 넣습니다. 빈 슬롯이 없으면 false(ConfirmAbsorption으로 교체할 슬롯을 골라야 함).
        /// </summary>
        public bool AcceptAbsorption()
        {
            if (HasPendingAbsorption == false) return false;

            AttackInfo dropped = pendingAbsorptions.Peek();
            int emptySlot = FindEmptySlot(dropped.Category);
            if (emptySlot < 0) return false;

            PlaceAbsorbed(dropped, emptySlot);
            ResolveAbsorption();
            return true;
        }

        /// <summary>
        /// 슬롯 가득 참 : 교체할 슬롯을 골랐을 때 UI가 호출합니다.
        /// </summary>
        public void ConfirmAbsorption(int slotIndexToReplace)
        {
            if (HasPendingAbsorption == false) return;

            AttackInfo dropped = pendingAbsorptions.Peek();
            int slotCount = dropped.Category == AttackSlotCategory.Magic ? magicSlots.Length : weaponSlots.Length;
            if (slotIndexToReplace < 0 || slotIndexToReplace >= slotCount) return;

            PlaceAbsorbed(dropped, slotIndexToReplace);
            ResolveAbsorption();
        }

        /// <summary>
        /// 흡수 포기 : 몬스터 처치와 라운드 진행에는 영향이 없습니다.
        /// </summary>
        public void DeclineAbsorption()
        {
            if (HasPendingAbsorption == false) return;
            ResolveAbsorption();
        }

        /// <summary>게임 오버 등으로 대기 중인 흡수 선택을 모두 버립니다.</summary>
        public void ClearPendingAbsorptions()
        {
            pendingAbsorptions.Clear();
        }

        // 현재 선택을 끝내고, 기다리는 무기가 더 있으면 다음 선택을 요청합니다.
        private void ResolveAbsorption()
        {
            pendingAbsorptions.Dequeue();

            if (pendingAbsorptions.Count > 0)
            {
                AbsorptionChoiceRequested?.Invoke(pendingAbsorptions.Peek());
            }
        }

        private void PlaceAbsorbed(AttackInfo dropped, int slotIndex)
        {
            if (dropped.Category == AttackSlotCategory.Magic)
            {
                magicSlots[slotIndex] = dropped;
                if (slotIndex == ActiveMagicSlot)
                {
                    SyncActiveMagicInfoToHitBoxes();
                }
            }
            else
            {
                weaponSlots[slotIndex] = dropped;
                if (slotIndex == ActiveWeaponSlot)
                {
                    attackInfo = dropped;
                    SyncActiveWeaponInfoToHitBoxes();
                }
            }

            NotifySlotsChanged();
            MonsterAbsorbed?.Invoke(dropped);
        }

        private int FindEmptySlot(AttackSlotCategory category)
        {
            return category == AttackSlotCategory.Magic ? FindEmptyMagicSlot() : FindEmptyWeaponSlot();
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

        // 기획서 5-1 : 시작 무기는 주먹(기본 공격력 3). 수치는 weapons.tsv(AttackTable)가 관리합니다.
        // 주먹은 "무기가 없을 때 돌아오는 자리"이므로 횟수 제한이 없습니다.
        private AttackInfo CreateDefaultWeaponInfo()
        {
            // AttackTable에서 주먹 데이터를 가져와 AttackInfo로 변환합니다.
            // GameManager.Instance.AttackTable.Load()가 게임 시작 시 선행되어야 합니다.
            AttackInfo fist = GameManager.Instance.AttackTable.Get(WeaponId.Fist).ToAttackInfo(UNLIMITED_USES);
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

        /// <summary>
        /// 무기 공속(Speed)에 플레이어 공속 보정을 적용한 공격 애니메이션 배속.
        /// </summary>
        public float GetFinalAnimationSpeed(AttackInfo info)
        {
            if (info.Speed <= 0) return 1f;

            float speed = Stat != null ? Stat.GetFinal(StatType.AttackSpeed, (float)info.Speed) : info.Speed;
            return AttackInfo.ToAnimationSpeed(speed);
        }
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

            NotifySlotsChanged();
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

            NotifySlotsChanged();
        }

        /// <summary>
        /// 기획서 9번 : "잡몹 등장 시간에 스킬을 충전...할 수 있음".
        /// 보스 라운드의 잡몹을 처치했을 때, 지금 쓰고 있는 무기/마법 슬롯의 사용
        /// 횟수를 회복합니다. 활성 슬롯 하나씩만 대상으로 삼아서, "지금 쓰는 무기가
        /// 자연스럽게 채워진다"는 느낌을 주고 UI 팝업 없이 실시간으로 적용됩니다.
        /// </summary>
        public void RechargeActiveSlots(int amount)
        {
            RechargeWeaponSlot(ActiveWeaponSlot, amount);
            RechargeMagicSlot(ActiveMagicSlot, amount);
        }

        private void RechargeWeaponSlot(int slotIndex, int amount)
        {
            if (slotIndex < 0 || slotIndex >= weaponSlots.Length) return;

            AttackInfo info = weaponSlots[slotIndex];
            if (info.IsEmpty) return;
            // 주먹처럼 무제한(UNLIMITED_USES)인 무기는 충전할 대상이 아닙니다.
            if (info.RemainingUses == UNLIMITED_USES) return;

            info.RemainingUses = Mathf.Min(info.RemainingUses + amount, MAX_USES);
            weaponSlots[slotIndex] = info;

            if (slotIndex == ActiveWeaponSlot)
            {
                attackInfo = info;
                SyncActiveWeaponInfoToHitBoxes();
            }

            NotifySlotsChanged();
        }

        private void RechargeMagicSlot(int slotIndex, int amount)
        {
            if (slotIndex < 0 || slotIndex >= magicSlots.Length) return;

            AttackInfo info = magicSlots[slotIndex];
            if (info.IsEmpty) return;

            info.RemainingUses = Mathf.Min(info.RemainingUses + amount, MAX_USES);
            magicSlots[slotIndex] = info;

            if (slotIndex == ActiveMagicSlot)
            {
                SyncActiveMagicInfoToHitBoxes();
            }

            NotifySlotsChanged();
        }

        public override void TakeDamage(int damage)
        {
            if (Stat == null)
            {
                Stat = new PlayerStat();
            }

            Stat.ApplyDamage(damage);
            HpChanged?.Invoke();
        }

        public override void TakeHeal(int heal)
        {
            if (Stat == null)
            {
                Stat = new PlayerStat();
            }

            Stat.ApplyHeal(heal);
            HpChanged?.Invoke();
        }

        private void NotifySlotsChanged()
        {
            UpdateSynergies();
            SlotsChanged?.Invoke();
        }

        /// <summary>
        /// 계열 시너지 : 무기 · 마법 슬롯의 계열별 개수를 세어, 조건을 채운 시너지의 보정을 붙이고
        /// 조건이 깨진 시너지의 보정은 뗍니다. 슬롯이 바뀔 때마다(NotifySlotsChanged) 호출됩니다.
        /// </summary>
        private void UpdateSynergies()
        {
            if (Stat == null || GameManager.Instance == null) return;

            Dictionary<WeaponFamily, int> counts = new Dictionary<WeaponFamily, int>();
            CountFamilies(weaponSlots, counts);
            CountFamilies(magicSlots, counts);

            bool changed = false;
            foreach (SynergyDef synergy in GameManager.Instance.SynergyTable.Defs)
            {
                counts.TryGetValue(synergy.Family, out int count);
                bool shouldBeActive = count >= synergy.Count;
                bool isActive = activeSynergies.Contains(synergy);

                if (shouldBeActive && isActive == false)
                {
                    Stat.AddModifier(new StatModifier(synergy.StatType, synergy.Op, synergy.Value, synergy.Source));
                    activeSynergies.Add(synergy);
                    changed = true;
                }
                else if (shouldBeActive == false && isActive)
                {
                    Stat.RemoveModifiersFrom(synergy.Source);
                    activeSynergies.Remove(synergy);
                    changed = true;
                }
            }

            if (changed) SynergiesChanged?.Invoke();
        }

        private static void CountFamilies(AttackInfo[] slots, Dictionary<WeaponFamily, int> counts)
        {
            foreach (AttackInfo info in slots)
            {
                if (info.IsEmpty) continue;
                if (GameManager.Instance.AttackTable.TryGet(info.Id, out AttackDef def) == false) continue;
                if (def.Family == WeaponFamily.None) continue;

                counts.TryGetValue(def.Family, out int count);
                counts[def.Family] = count + 1;
            }
        }
    }

}
