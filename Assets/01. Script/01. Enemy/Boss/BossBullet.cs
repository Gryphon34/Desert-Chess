using UnityEngine;

namespace Study_ActionPlatformer
{
    public class BossBullet : MonoBehaviour
    {
        // 1. 특정 방향으로 운동
        // 2. 플레이어와 충돌하면 데미지를 줌(전투이벤트 생성 후 전달, Pass) 
        // 3. 재사용이 가능해야함

        [SerializeField] private Vector3 startLocalPosition;
        [SerializeField] private Vector3 moveVector = Vector3.up;
        [SerializeField] private float speed = 1.0f;

        // 못맞췄을 경우 영원히 날아가는것을 막기위해 lifeTime을 설정합니다
        [SerializeField] private float lifeTime = 5.0f;
        private float currentTime = 0.0f;
        private bool isFired = false;

        // 이 탄환의 위력. Set()으로 주입받습니다.
        // 예전에는 Set(int damage)가 매개변수를 그냥 버려서, 패턴이 스킬 데미지를
        // 계산해 넘겨줘도 전달되지 않았습니다.
        private int damage = 0;

        // 이 탄환을 쏜 주체(보스). 데미지를 "누가 보냈는지" 알아야
        // CombatSystem으로 전투 이벤트를 보낼 수 있습니다.
        private CombatEntity Owner { get; set; }

        protected virtual void Awake()
        {
            // 탄환은 보스 계층 아래에 있으므로 부모를 거슬러 올라가면 Boss를 찾습니다.
            // (풀에서 복제된 탄환도 같은 계층에 생성되므로 동일하게 동작합니다)
            Owner = GetComponentInParent<CombatEntity>();

            if (Owner == null)
                Debug.LogWarning($"{name} : 부모 계층에서 CombatEntity를 찾지 못했습니다. 데미지가 전달되지 않습니다.");
        }

        // Bullet 객체를 초기화 합니다.
        public void Set(int bulletDamage)
        {
            damage = bulletDamage;
            isFired = false;
            gameObject.SetActive(true);
            transform.localPosition = startLocalPosition;
        }

        public void Fire()
        {
            isFired = true;
            currentTime = 0.0f;
        }

        private void Update()
        {
            if (isFired == false) return;

            currentTime += Time.deltaTime;

            if (currentTime >= lifeTime)
            {
                gameObject.SetActive(false);
                return;
            }

            // 아래부터는 운동로직
            transform.Translate(moveVector * (speed * Time.deltaTime), Space.Self);
            // Translate(이동량, Space.Self) 내 기준으로 이동
            // Translate(이동량, Space.World) 월드를 기준으로 이동
        }

        protected virtual void OnTriggerEnter2D(Collider2D collision)
        {
            // 발사되기 전(조준 중)에는 판정하지 않습니다.
            // Pattern1/Pattern2는 탄환을 켜둔 채로 몇 초간 조준하기 때문에,
            // 이 검사가 없으면 조준 도중에 스쳐도 맞아버립니다.
            if (isFired == false) return;

            // 예전에는 CompareTag("Player")로 판정했는데, 플레이어는 Untagged이고
            // 프로젝트에 "Player" 태그 자체가 정의돼 있지 않아서 절대 참이 되지 않았습니다.
            // 몬스터 총알(Bullet.cs)과 같은 방식으로 CombatEntity를 직접 찾습니다.
            CombatEntity receiver = collision.GetComponentInParent<CombatEntity>();
            if (receiver == null) return;

            // 자기 자신과 같은 편(보스 부위 포함)은 때리지 않습니다.
            if (receiver == Owner) return;
            if (Owner is Enemy && receiver is Enemy) return;

            if (Owner != null)
            {
                CombatEvent @event;
                @event.EventType = CombatEventType.DamageEvent;
                @event.Amount = damage;
                @event.Position = collision.ClosestPoint(transform.position);

                CombatSystem.Instance.To(Owner, receiver, @event);
            }

            gameObject.SetActive(false);
        }
    }

}
