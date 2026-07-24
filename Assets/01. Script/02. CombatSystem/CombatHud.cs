using UnityEngine;

namespace Study_ActionPlatformer
{
    /// <summary>
    /// 임시(placeholder) UI 입니다.
    /// Canvas/이미지 에셋 없이 legacy OnGUI만으로 동작해서, 씬에 빈 GameObject
    /// 하나 만들어 이 스크립트만 붙이면 바로 테스트할 수 있습니다.
    ///
    /// 담당 범위 (기획서 8, MVP "기본 UI" 항목):
    /// - 체력바
    /// - 무기/마법 슬롯 상태 표시
    /// - 몬스터 흡수 선택 팝업 (Player.AbsorptionChoiceRequested)
    /// - 라운드 보상 선택 팝업 (RewardManager.RewardChoiceRequested)
    ///
    /// 나중에 실제 그래픽 UI(Canvas + Image + TMP 등)로 교체될 때까지의
    /// 최소 기능 버전이므로, 디자인이 잡히면 이 스크립트는 걷어내면 됩니다.
    /// </summary>
    public class CombatHud : MonoBehaviour
    {
        [SerializeField] private RewardManager rewardManager;

        private bool subscribedToPlayer = false;
        private bool subscribedToReward = false;

        private AttackInfo? pendingAbsorption;
        private int pendingRewardRound = -1;

        private void Update()
        {
            // Player/RewardManager가 이 오브젝트보다 늦게 초기화될 수 있으므로
            // 매 프레임 구독 여부를 확인합니다(스크립트 실행 순서 문제 회피).
            if (subscribedToPlayer == false && Player.LocalPlayer != null)
            {
                Player.LocalPlayer.AbsorptionChoiceRequested += OnAbsorptionChoiceRequested;
                subscribedToPlayer = true;
            }

            if (subscribedToReward == false)
            {
                if (rewardManager == null) rewardManager = FindAnyObjectByType<RewardManager>();

                if (rewardManager != null)
                {
                    rewardManager.RewardChoiceRequested += OnRewardChoiceRequested;
                    subscribedToReward = true;
                }
            }
        }

        private void OnDisable()
        {
            if (subscribedToPlayer && Player.LocalPlayer != null)
            {
                Player.LocalPlayer.AbsorptionChoiceRequested -= OnAbsorptionChoiceRequested;
            }

            if (subscribedToReward && rewardManager != null)
            {
                rewardManager.RewardChoiceRequested -= OnRewardChoiceRequested;
            }

            subscribedToPlayer = false;
            subscribedToReward = false;

            // 팝업이 떠 있는 동안 이 오브젝트가 꺼지면(씬 전환, 비활성화 등)
            // timeScale이 0인 채로 남아 게임 전체가 멈춰버립니다.
            // 팝업을 띄운 쪽이 끝까지 책임지고 원복합니다.
            ClosePopups();
        }

        /// <summary>열려 있던 팝업을 닫고 시간을 되돌립니다.</summary>
        private void ClosePopups()
        {
            bool hadPopup = (pendingAbsorption != null) || (pendingRewardRound >= 0);

            pendingAbsorption = null;
            pendingRewardRound = -1;

            // 다른 곳에서 의도적으로 멈춰둔 경우까지 되살리지 않도록,
            // 내가 멈춘 경우에만 원복합니다.
            if (hadPopup) Time.timeScale = 1f;
        }

        private void OnAbsorptionChoiceRequested(AttackInfo dropped)
        {
            pendingAbsorption = dropped;
            Time.timeScale = 0f;
        }

        private void OnRewardChoiceRequested(int roundIndex)
        {
            pendingRewardRound = roundIndex;
            Time.timeScale = 0f;
        }

        private void OnGUI()
        {
            DrawHealthBar();
            DrawSlotBar();

            // 선택 팝업이 떠 있는 동안은 그것만 그린다(동시에 두 개가 뜨는 상황은 없다고 가정).
            if (pendingAbsorption != null) DrawAbsorptionPopup();
            else if (pendingRewardRound >= 0) DrawRewardPopup();
        }

        private void DrawHealthBar()
        {
            Player player = Player.LocalPlayer;
            if (player == null || player.BaseStat == null) return;

            int hp = player.BaseStat.Hp;
            int maxHp = Mathf.Max(1, player.BaseStat.MaxHp);
            float ratio = (float)hp / maxHp;

            GUI.Box(new Rect(20, 20, 220, 26), string.Empty);
            GUI.Box(new Rect(20, 20, 220 * ratio, 26), $"HP {hp}/{maxHp}");
        }

        private void DrawSlotBar()
        {
            Player player = Player.LocalPlayer;
            if (player == null) return;

            GUILayout.BeginArea(new Rect(20, 54, 460, 60));

            GUILayout.BeginHorizontal();
            GUILayout.Label("무기:", GUILayout.Width(40));
            for (int i = 0; i < player.WeaponSlotCount; ++i)
            {
                string label = DescribeSlot(player.GetWeaponSlot(i));
                if (i == player.ActiveWeaponSlot) label = "▶" + label;
                GUILayout.Label(label, GUILayout.Width(110));
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("마법:", GUILayout.Width(40));
            for (int i = 0; i < player.MagicSlotCount; ++i)
            {
                string label = DescribeSlot(player.GetMagicSlot(i));
                if (i == player.ActiveMagicSlot) label = "▶" + label;
                GUILayout.Label(label, GUILayout.Width(110));
            }
            GUILayout.EndHorizontal();

            GUILayout.EndArea();
        }

        // 슬롯 한 칸을 문자열로 표현합니다. 주먹처럼 횟수 제한이 없는 무기는 ∞로 표시합니다.
        private static string DescribeSlot(AttackInfo info)
        {
            if (info.IsEmpty) return "[빈 슬롯]";

            string uses = (info.RemainingUses == Player.UNLIMITED_USES)
                ? "∞"
                : info.RemainingUses.ToString();

            return $"[{info.Id} x{uses}]";
        }

        private void DrawAbsorptionPopup()
        {
            Player player = Player.LocalPlayer;
            if (player == null)
            {
                pendingAbsorption = null;
                Time.timeScale = 1f;
                return;
            }

            AttackInfo dropped = pendingAbsorption.Value;
            bool isMagic = dropped.Category == AttackSlotCategory.Magic;
            string categoryLabel = isMagic ? "마법" : "무기";

            Rect area = new Rect(Screen.width / 2 - 160, Screen.height / 2 - 120, 320, 260);
            GUILayout.BeginArea(area, GUI.skin.box);

            GUILayout.Label($"슬롯이 가득 찼습니다.\n{dropped.Id}({categoryLabel})을(를) 흡수할까요?");

            int slotCount = isMagic ? player.MagicSlotCount : player.WeaponSlotCount;
            for (int i = 0; i < slotCount; ++i)
            {
                AttackInfo current = isMagic ? player.GetMagicSlot(i) : player.GetWeaponSlot(i);

                if (GUILayout.Button($"{i + 1}번 슬롯 교체 (현재: {current.Id})"))
                {
                    player.ConfirmAbsorption(i);
                    pendingAbsorption = null;
                    Time.timeScale = 1f;
                }
            }

            if (GUILayout.Button("포기"))
            {
                player.DeclineAbsorption();
                pendingAbsorption = null;
                Time.timeScale = 1f;
            }

            GUILayout.EndArea();
        }

        private void DrawRewardPopup()
        {
            if (rewardManager == null)
            {
                pendingRewardRound = -1;
                Time.timeScale = 1f;
                return;
            }

            Player player = Player.LocalPlayer;

            Rect area = new Rect(Screen.width / 2 - 160, Screen.height / 2 - 140, 320, 300);
            GUILayout.BeginArea(area, GUI.skin.box);

            GUILayout.Label($"{pendingRewardRound}라운드 클리어!\n보상을 선택하세요.");

            if (GUILayout.Button("체력 회복"))
            {
                rewardManager.ChooseHeal();
                pendingRewardRound = -1;
                Time.timeScale = 1f;
            }

            if (player != null)
            {
                for (int i = 0; i < player.WeaponSlotCount; ++i)
                {
                    AttackInfo info = player.GetWeaponSlot(i);
                    if (info.IsEmpty) continue;

                    if (GUILayout.Button($"무기 {i + 1}번({info.Id}) 강화"))
                    {
                        rewardManager.ChooseEnhance(true, i);
                        pendingRewardRound = -1;
                        Time.timeScale = 1f;
                    }
                }

                for (int i = 0; i < player.MagicSlotCount; ++i)
                {
                    AttackInfo info = player.GetMagicSlot(i);
                    if (info.IsEmpty) continue;

                    if (GUILayout.Button($"마법 {i + 1}번({info.Id}) 강화"))
                    {
                        rewardManager.ChooseEnhance(false, i);
                        pendingRewardRound = -1;
                        Time.timeScale = 1f;
                    }
                }
            }

            GUILayout.EndArea();
        }
    }
}
