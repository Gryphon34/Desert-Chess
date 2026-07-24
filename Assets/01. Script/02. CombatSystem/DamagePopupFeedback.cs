using Study.Utilities;
using Study_ActionPlatformer;
using UnityEngine;

public class DamagePopupFeedback : MonoBehaviour, ICombatObserver
{
    [field: SerializeField] public FloatingText PopupText { get; private set; }
    [field: SerializeField] public Color DamageColor { get; private set; }
    [field: SerializeField] public Color CriticalColor { get; private set; }

    public int CriticalPoint = 260;

    private ComponentPool<FloatingText> Pool { get; set; }

    private void Awake()
    {
        Pool = new ComponentPool<FloatingText>(PopupText, transform);
    }

    private void OnEnable()
    {
        CombatSystem.Instance.Subscribe(this);
    }

    private void OnDisable()
    {
        // 정리 시점에는 Instance를 쓰면 안 됩니다.
        // 종료가 시작되면(OnApplicationQuit) 인스턴스가 살아있어도 Instance가 null을
        // 돌려주고, 반대로 인스턴스가 없으면 새로 만들어버리기 때문입니다.
        // InstanceOrNull은 있는 것만 돌려주고 아무것도 만들지 않습니다.
        CombatSystem combat = CombatSystem.InstanceOrNull;
        if (combat == null) return;

        combat.UnSubscribe(this);
    }

    public void OnDamageTaken(CombatEntity sender, CombatEntity receiver, CombatEvent @event)
    {
        FloatingText textItem = Pool.Get();

        Color color = DamageColor;

        if (@event.Amount > CriticalPoint) color = CriticalColor;
        textItem.Show($"{@event.Amount}", color, receiver.HeadUpPivot.position);
    }

    public void OnHealTaken(CombatEntity sender, CombatEntity receiver, CombatEvent @event)
    {

    }
}
