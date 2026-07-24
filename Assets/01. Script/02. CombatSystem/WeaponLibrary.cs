using UnityEngine;

namespace Study_ActionPlatformer
{
    /// <summary>
    /// 기획서 5-3(무기 스탯 / 마법 스탯) 표를 코드로 옮긴 "무기 도감"입니다.
    ///
    /// 왜 만들었나:
    /// 몬스터가 떨어뜨리는 무기(Enemy.droppedWeaponInfo)를 프리팹마다 손으로 채우면
    /// 하나라도 비어 있는 순간 흡수 시스템이 조용히 동작하지 않습니다.
    /// 기획서 4번의 "라운드별 몬스터 능력 = 랜덤"을 만족시키려면 어차피 표가 필요하므로,
    /// 데이터를 한곳에 모아두고 여기서 뽑아 쓰게 했습니다.
    ///
    /// 나중에 밸런싱을 기획자가 직접 만지게 하려면 이 클래스를 ScriptableObject로
    /// 옮기면 됩니다. 표의 "모양"은 그대로 두고 저장 위치만 바꾸면 되도록 짜여 있습니다.
    /// </summary>
    public static class WeaponLibrary
    {
        // 기획서 9번 : 각 무기/스킬은 5번 사용하면 소멸합니다.
        public const int DEFAULT_USES = 5;

        /// <summary>
        /// 도감 한 줄. 기획서 표의 열(공격력/공속/범위)을 그대로 옮겼습니다.
        /// 공속과 범위는 아직 전투 계산에 반영되지 않지만, 나중에 붙일 때
        /// 표를 다시 만들지 않도록 지금 같이 담아둡니다.
        /// </summary>
        public readonly struct Entry
        {
            public readonly WeaponId Id;
            public readonly AttackSlotCategory Category;
            public readonly int Power;        // 공격력 1 ~ 10
            public readonly int AttackSpeed;  // 공속   1 ~ 10
            public readonly int Range;        // 범위   1 ~ 5

            public Entry(WeaponId id, AttackSlotCategory category, int power, int attackSpeed, int range)
            {
                Id = id;
                Category = category;
                Power = power;
                AttackSpeed = attackSpeed;
                Range = range;
            }
        }

        // 기획서 5-3 표 그대로입니다. (스탯이 비어 있던 채찍/사슬낫/도리깨/석궁/표창/
        //  부메랑/슬링은 수치가 정해지면 여기에 한 줄씩 추가하면 됩니다)
        private static readonly Entry[] Entries =
        {
            //                                          공격력 공속 범위
            new Entry(WeaponId.Fist,      AttackSlotCategory.Weapon, 3, 5, 1),
            new Entry(WeaponId.Sword,     AttackSlotCategory.Weapon, 5, 5, 2),
            new Entry(WeaponId.Spear,     AttackSlotCategory.Weapon, 4, 6, 3),
            new Entry(WeaponId.Axe,       AttackSlotCategory.Weapon, 7, 3, 2),
            new Entry(WeaponId.Dagger,    AttackSlotCategory.Weapon, 3, 8, 1),
            new Entry(WeaponId.Hammer,    AttackSlotCategory.Weapon, 9, 2, 1),
            new Entry(WeaponId.Glaive,    AttackSlotCategory.Weapon, 6, 4, 3),
            new Entry(WeaponId.Trident,   AttackSlotCategory.Weapon, 5, 5, 3),
            new Entry(WeaponId.WarHammer, AttackSlotCategory.Weapon, 8, 2, 2),
            new Entry(WeaponId.Gun,       AttackSlotCategory.Weapon, 6, 4, 5),
            new Entry(WeaponId.Bow,       AttackSlotCategory.Weapon, 5, 5, 4),

            new Entry(WeaponId.Fire,      AttackSlotCategory.Magic,  6, 4, 3),
            new Entry(WeaponId.Water,     AttackSlotCategory.Magic,  4, 5, 4),
            new Entry(WeaponId.Lightning, AttackSlotCategory.Magic,  5, 6, 3),
            new Entry(WeaponId.Dark,      AttackSlotCategory.Magic,  7, 3, 3),
        };

        /// <summary>도감에서 무작위로 한 개를 뽑아 AttackInfo로 만들어 줍니다.</summary>
        public static AttackInfo CreateRandom()
        {
            Entry entry = Entries[Random.Range(0, Entries.Length)];
            return Create(entry);
        }

        /// <summary>지정한 무기를 AttackInfo로 만들어 줍니다. 도감에 없으면 주먹을 돌려줍니다.</summary>
        public static AttackInfo Create(WeaponId id)
        {
            for (int i = 0; i < Entries.Length; ++i)
            {
                if (Entries[i].Id == id) return Create(Entries[i]);
            }

            Debug.LogWarning($"WeaponLibrary ::: 도감에 없는 무기입니다 : {id}. 주먹으로 대체합니다.");
            return CreateFist();
        }

        /// <summary>기획서 5-1 : 플레이어의 시작 무기(주먹).</summary>
        public static AttackInfo CreateFist() => Create(Entries[0]);

        private static AttackInfo Create(Entry entry)
        {
            // 기획서는 "공격력" 한 값만 주므로, 그 값을 중심으로 ±1의 폭을 줘서
            // 매번 똑같은 숫자만 뜨지 않게 합니다. 범위는 기획서 5-4(1~10)를 지킵니다.
            int min = Mathf.Clamp(entry.Power - 1, 1, 10);
            int max = Mathf.Clamp(entry.Power + 1, 1, 10);

            return new AttackInfo
            {
                Id = entry.Id,
                Category = entry.Category,
                Key = (entry.Category == AttackSlotCategory.Magic) ? AttackKey.Magic1 : AttackKey.Combo1,
                MinDamage = min,
                MaxDamage = max,
                RemainingUses = DEFAULT_USES,
                damageCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f),
            };
        }
    }
}
