using UnityEngine;
using Study_ActionPlatformer;

public class TableTest : MonoBehaviour
{
    private void Start()
    {
        // GameManager의 Awake/Start 이후에 호출되도록 주의
        AttackDef swordDef = GameManager.Instance.AttackTable.Get(WeaponId.Dagger);
        Debug.Log($"데이터 로드 테스트 - ID: {swordDef.WeaponId}, 공격력: {swordDef.MinDamage} ~ {swordDef.MaxDamage}");
    }
}