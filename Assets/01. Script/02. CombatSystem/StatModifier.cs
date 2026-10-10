namespace Study_ActionPlatformer
{
    public enum StatType
    {
        Attack,         // 공격력 (HitBox 데미지 계산에 반영)
        AttackSpeed,    // 공속 (무기 Speed가 실제 동작에 연결된 뒤 반영)
        Range,          // 범위 (무기 Range가 실제 동작에 연결된 뒤 반영)
    }

    public enum ModifierOp
    {
        Add,        // 기준값에 더함 (예: +2)
        Multiply,   // 배율로 곱함 (예: 1.1 = +10%)
    }

    /// <summary>
    /// 스탯 보정 하나. 시너지 · 보상 · 보스 스킬이 PlayerStat에 붙였다 뗐다 합니다.
    /// 수치(Value)는 코드에 적지 않고 각 기능의 tsv에서 읽어 넣습니다.
    /// </summary>
    public readonly struct StatModifier
    {
        public readonly StatType Type;
        public readonly ModifierOp Op;
        public readonly float Value;

        // 누가 붙인 보정인지. 같은 Source로 붙인 보정을 한 번에 뗄 때 씁니다.
        // 예: "Synergy.Melee", "Reward.Round3"
        public readonly string Source;

        public StatModifier(StatType type, ModifierOp op, float value, string source)
        {
            Type = type;
            Op = op;
            Value = value;
            Source = source;
        }
    }
}
