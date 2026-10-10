using System;
using System.Collections.Generic;
using UnityEngine;

namespace Study_ActionPlatformer
{
    // 플레이어 전용 스탯.
    // Player.Stat은 인스펙터에 노출되지 않고 코드에서 new로 생성되기 때문에,
    // 최대 체력을 여기(코드)에서 책임져야 합니다.
    public class PlayerStat : BaseStat
    {
        // 기획서 5-1 : 플레이어 체력 최대 100
        public const int DEFAULT_MAX_HP = 100;

        private readonly List<StatModifier> modifiers = new List<StatModifier>();

        // 보정이 붙거나 떨어졌을 때. HUD 등이 스탯 표시를 갱신할 때 씁니다.
        public event Action ModifiersChanged;

        public IReadOnlyList<StatModifier> Modifiers => modifiers;

        public PlayerStat() : base(DEFAULT_MAX_HP)
        {
        }

        public void AddModifier(StatModifier modifier)
        {
            modifiers.Add(modifier);
            ModifiersChanged?.Invoke();
        }

        /// <summary>
        /// 같은 Source로 붙인 보정을 모두 뗍니다. 뗀 개수를 돌려줍니다.
        /// (예: 시너지가 꺼질 때 그 시너지가 붙인 보정만 정리)
        /// </summary>
        public int RemoveModifiersFrom(string source)
        {
            int removed = modifiers.RemoveAll(m => m.Source == source);
            if (removed > 0) ModifiersChanged?.Invoke();
            return removed;
        }

        /// <summary>
        /// 기준값(무기 수치 등)에 보정을 적용한 최종 수치.
        /// 계산 순서 : (기준값 + 모든 Add) × 모든 Multiply
        /// 보정이 없으면 기준값이 그대로 나옵니다.
        /// </summary>
        public float GetFinal(StatType type, float baseValue)
        {
            float add = 0f;
            float multiply = 1f;

            for (int i = 0; i < modifiers.Count; ++i)
            {
                StatModifier m = modifiers[i];
                if (m.Type != type) continue;

                if (m.Op == ModifierOp.Add) add += m.Value;
                else multiply *= m.Value;
            }

            return (baseValue + add) * multiply;
        }

        public int GetFinal(StatType type, int baseValue)
        {
            return Mathf.RoundToInt(GetFinal(type, (float)baseValue));
        }
    }
}
