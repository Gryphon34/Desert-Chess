using Study_ActionPlatformer;
using UnityEngine;

public static class CombatEntityExtension
{
    public static Enemy GetEnemy(this CombatSystem combatSystem, HurtBox hurtBox)
    {
        if (hurtBox == null)
            return null;

        if (hurtBox.Owner is Enemy enemy)
            return enemy;

        return null;
    }
}