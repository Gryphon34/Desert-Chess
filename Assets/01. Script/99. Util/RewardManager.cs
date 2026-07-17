using System;
using UnityEngine;

namespace Study_ActionPlatformer
{
    public class RewardManager : MonoBehaviour
    {
        [SerializeField] private int roundRewardHp = 10;
        [SerializeField] private int bossRewardHp = 30;
        [SerializeField] private int enhanceDamageBoost = 1;
        [SerializeField] private int enhanceUsesBoost = 3;

        // 기획서 8번 : 라운드 클리어 후 "보상 선택지"가 등장합니다.
        // 자동으로 지급하지 않고, 이 이벤트를 UI가 구독해서 선택창을 띄웁니다.
        public event Action<int> RewardChoiceRequested;

        private int pendingRoundIndex = -1;

        /// <summary>
        /// 라운드 클리어 시 호출됩니다. 즉시 보상을 주지 않고 선택을 요청합니다.
        /// 실제 지급은 ChooseHeal() 또는 ChooseEnhance()가 호출될 때 이뤄집니다.
        /// </summary>
        public void GrantRoundReward(int roundIndex)
        {
            pendingRoundIndex = roundIndex;
            RewardChoiceRequested?.Invoke(roundIndex);
        }

        // 보스 처치 보상은 선택지가 아니라 확정 보상이므로 그대로 즉시 지급합니다.
        public void GrantBossClearReward()
        {
            if (Player.LocalPlayer == null) return;
            Player.LocalPlayer.TakeHeal(bossRewardHp);
        }

        /// <summary>
        /// 보상 방향 1 : 피 회복. UI에서 플레이어가 선택했을 때 호출합니다.
        /// </summary>
        public void ChooseHeal()
        {
            if (pendingRoundIndex < 0) return;

            if (Player.LocalPlayer != null)
            {
                int healAmount = roundRewardHp + (pendingRoundIndex * 2);
                Player.LocalPlayer.TakeHeal(healAmount);
            }

            pendingRoundIndex = -1;
        }

        /// <summary>
        /// 보상 방향 2 : 능력 강화. 지정한 슬롯의 데미지 범위와 사용 횟수를 늘립니다.
        /// </summary>
        public void ChooseEnhance(bool isWeaponSlot, int slotIndex)
        {
            if (pendingRoundIndex < 0) return;

            if (Player.LocalPlayer != null)
            {
                if (isWeaponSlot)
                    Player.LocalPlayer.EnhanceWeaponSlot(slotIndex, enhanceDamageBoost, enhanceUsesBoost);
                else
                    Player.LocalPlayer.EnhanceMagicSlot(slotIndex, enhanceDamageBoost, enhanceUsesBoost);
            }

            pendingRoundIndex = -1;
        }
    }
}
