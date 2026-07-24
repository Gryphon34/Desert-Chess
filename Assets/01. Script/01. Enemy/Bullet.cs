using UnityEngine;

namespace Study_ActionPlatformer
{
    public class Bullet : MonoBehaviour
    {
        [field: SerializeField] public float Speed { get; private set; }
        [SerializeField] private float lifeTime = 5.0f;

        private Vector3 foward = Vector3.right;

        // 이 총알을 쏜 주체와 위력. RangeController가 발사할 때 넣어줍니다.
        private CombatEntity owner;
        private int damage = 4;

        private void Start()
        {
            // 아무것도 맞히지 못한 총알이 씬에 영원히 남지 않도록 수명을 줍니다.
            Destroy(gameObject, lifeTime);
        }

        private void Update()
        {
            transform.Translate(foward * Speed * Time.deltaTime);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            CombatEntity receiver = other.GetComponentInParent<CombatEntity>();
            if (receiver == null) return;

            // 쏜 본인이나 같은 편(몬스터)에게는 맞지 않습니다.
            if (receiver == owner) return;
            if (owner is Enemy && receiver is Enemy) return;

            if (owner != null)
            {
                CombatEvent @event;
                @event.EventType = CombatEventType.DamageEvent;
                @event.Amount = damage;
                @event.Position = other.ClosestPoint(transform.position);

                CombatSystem.Instance.To(owner, receiver, @event);
            }

            Destroy(gameObject);
        }

        public void Set(Vector3 direction)
        {
            foward = direction;
        }

        public void SetShooter(CombatEntity shooter, int bulletDamage)
        {
            owner = shooter;
            damage = bulletDamage;
        }
    }
}
