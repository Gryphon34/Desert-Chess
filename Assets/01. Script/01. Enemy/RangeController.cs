using UnityEngine;

namespace Study_ActionPlatformer
{
    public class RangeController : EnemyController
    {
        [field: SerializeField] public Bullet BulletPrefab { get; set; }
        [field: SerializeField] public Transform FirePoint { get; private set; }

        protected override void ProcessAttack()
        {
            // 원거리 몬스터는 근접 타격(base) 대신 총알을 발사합니다.
            if (Target == null || BulletPrefab == null || FirePoint == null) return;

            UpdateDirection(Target.position);
            Bullet bullet = Instantiate(BulletPrefab, FirePoint.position, Quaternion.identity);

            float dir = Target.position.x - transform.position.x;
            Vector3 right = Vector3.right * Mathf.Sign(dir);
            bullet.Set(right);

            // 총알이 "누가 쏜 몇 데미지짜리인지"를 알아야 CombatSystem으로 보낼 수 있습니다.
            bullet.SetShooter(Enemy, RollAttackDamage());
        }
    }
}
