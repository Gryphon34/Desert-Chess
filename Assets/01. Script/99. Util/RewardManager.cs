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

        // 플레이어가 보상을 실제로 고른 뒤에 발행됩니다.
        // GameManager가 이걸 기다렸다가 다음 라운드를 시작합니다.
        // (이 이벤트가 없던 시절에는 팝업을 띄우자마자 다음 라운드가 스폰됐습니다)
        public event Action RewardResolved;

        private int pendingRoundIndex = -1;

        /// <summary>보상 선택이 진행 중인지 여부.</summary>
        public bool HasPendingChoice => pendingRoundIndex >= 0;

        // 선택이 끝났을 때 공통으로 거치는 마무리 지점입니다.
        // 여러 선택지가 각자 pendingRoundIndex를 초기화하고 이벤트를 쏘면
        // 한쪽만 빠뜨리기 쉬우므로 한곳으로 모읍니다.
        private void ResolveChoice()
        {
            pendingRoundIndex = -1;
            RewardResolved?.Invoke();
        }

        /// <summary>
        /// 라운드 클리어 시 호출됩니다. 즉시 보상을 주지 않고 선택을 요청합니다.
        /// 실제 지급은 ChooseHeal() 또는 ChooseEnhance()가 호출될 때 이뤄집니다.
        /// </summary>
        public void GrantRoundReward(int roundIndex)
        {
            pendingRoundIndex = roundIndex;

            // 선택 UI(CombatHud)가 씬에 없으면 아무도 이 이벤트를 듣지 않습니다.
            // 그러면 선택이 영원히 끝나지 않고, GameManager도 RewardResolved를
            // 기다리느라 다음 라운드를 시작하지 못해 게임이 멈춥니다.
            // 구독자가 없을 때는 기본 보상(체력 회복)으로 자동 진행합니다.
            if (RewardChoiceRequested == null)
            {
                Debug.LogWarning("RewardManager ::: 보상 선택 UI가 없어 체력 회복으로 자동 진행합니다. " +
                    "씬에 CombatHud를 추가해주세요.");
                ChooseHeal();
                return;
            }

            RewardChoiceRequested.Invoke(roundIndex);
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

            ResolveChoice();
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

            ResolveChoice();
        }
    }
}
