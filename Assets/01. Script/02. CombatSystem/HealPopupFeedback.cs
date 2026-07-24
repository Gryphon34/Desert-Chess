using Study.Utilities;
using Study_ActionPlatformer;
using System.Collections;
using UnityEngine;


public class HealPopupFeedback : MonoBehaviour, ICombatObserver
{
    [field: SerializeField] public FloatingText PopupText { get; private set; }
    [field: SerializeField] public Color HealColor { get; private set; }

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
        // 정리 시점에는 Instance를 쓰면 안 됩니다. (DamagePopupFeedback의 주석 참고)
        CombatSystem combat = CombatSystem.InstanceOrNull;
        if (combat == null) return;

        combat.UnSubscribe(this);
    }

    public void OnDamageTaken(CombatEntity sender, CombatEntity receiver, CombatEvent @event)
    {

    }

    public void OnHealTaken(CombatEntity sender, CombatEntity receiver, CombatEvent @event)
    {
        StartCoroutine(HealPopupCoroutine(receiver, @event));
    }

    private IEnumerator HealPopupCoroutine(CombatEntity receiver, CombatEvent @event)
    {
        int healAmount = @event.Amount;

        while (true)
        {
            if (healAmount <= 0) break;

            FloatingText textItem = Pool.Get();
            Color color = HealColor;
            int healAmountDelta = 2;
            healAmount -= healAmountDelta;

            textItem.Show($"+{healAmountDelta}", color, receiver.HeadUpPivot.position);
            yield return new WaitForSeconds(0.1f);
        }
    }
}
