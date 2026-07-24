using UnityEngine;

namespace Study_ActionPlatformer
{
    [RequireComponent(typeof(Collider2D))]
    public class HurtBox : MonoBehaviour
    {
        [field: SerializeField] public CombatEntity Owner { get; private set; }
        private Collider2D col2D;

        private void Awake()
        {
            Owner = GetComponent<CombatEntity>();
            if (Owner == null) Owner = GetComponentInParent<CombatEntity>();

            if (Owner == null)
            {
                Debug.LogError($"{name} : CombatEntity를 찾지 못했습니다.");
            }

            col2D = GetComponent<Collider2D>();
        }

        // 아래의 Start와 OnDestroy보다 OnEnable이랑 OnDisable

        private void Start()
        {
            CombatSystem.Instance.AddHurtBox(col2D, this);
        }

        private void OnDestroy()
        {
            // 플레이 종료(또는 씬 언로드) 시 오브젝트 파괴 순서는 보장되지 않습니다.
            // 정리 시점에는 Instance 대신 InstanceOrNull을 씁니다.
            // (Instance는 종료 중에 null을 돌려주거나, 없으면 새로 만들어버립니다)
            CombatSystem combat = CombatSystem.InstanceOrNull;
            if (combat == null) return;

            combat.RemoveHurtBox(col2D);
        }
    }
}
