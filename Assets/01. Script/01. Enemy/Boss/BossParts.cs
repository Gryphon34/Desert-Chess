using UnityEngine;

namespace Study_ActionPlatformer
{
    public class BossParts : Enemy
    {
        [field: SerializeField] private Boss Owner { get; set; }

        // 부위는 스스로 죽지도, 무기를 드랍하지도 않으므로 랜덤 드랍 지정을 건너뜁니다.
        protected override void EnsureDroppedWeapon() { }

        // 부위가 받는 데미지는 
        public override int CalculateFinalDamage(int damage)
        {
            return Owner.CalculateFinalDamage(damage);
        }

        public override void TakeDamage(int damage)
        {
            Owner.TakeDamage(this, damage);
            StartCoroutine(HitEffectCoroutine());
        }

        public override void TakeHeal(int heal)
        {

        }
    }

}
