using UnityEngine;

namespace Study_ActionPlatformer
{
    // 플레이어 전용 스탯.
    // Player.Stat은 인스펙터에 노출되지 않고 코드에서 new로 생성되기 때문에,
    // 최대 체력을 여기(코드)에서 책임져야 합니다.
    public class PlayerStat : BaseStat
    {
        // 기획서 5-1 : 플레이어 체력 최대 100
        public const int DEFAULT_MAX_HP = 100;

        public PlayerStat() : base(DEFAULT_MAX_HP)
        {
        }
    }
}
