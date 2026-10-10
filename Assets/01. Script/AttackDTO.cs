using UnityEngine;

namespace Study_ActionPlatformer
{
    public class AttackDTO
    {
        public int ID;
        public string Category;
        public string WeaponId;
        public string AttackKey;
        public int MinDamage;
        public int MaxDamage;
        public int Speed;
        public int Range;
        public string Family;
    }

    [System.Serializable]
    public class AttackDef
    {
        public int ID;
        public AttackSlotCategory Category;
        public WeaponId WeaponId;
        public AttackKey Key;
        public int MinDamage;
        public int MaxDamage;
        public int Speed;
        public int Range;
        public WeaponFamily Family;

        public AttackInfo ToAttackInfo(int remainingUses)
        {
            return new AttackInfo
            {
                Id = this.WeaponId,
                Category = this.Category,
                Key = this.Key,
                MinDamage = this.MinDamage,
                MaxDamage = this.MaxDamage,
                RemainingUses = remainingUses,
                damageCurve = null,
                Speed = this.Speed,
                Range = this.Range,
            };
        }
    }
}