using UnityEngine;

namespace Study_ActionPlatformer
{
    public class CombatHud : MonoBehaviour
    {
        [SerializeField] private RewardManager rewardManager;

        // RoundManager는 라운드가 바뀌었다는 이벤트를 발행하지 않으므로,
        // "마지막으로 본 라운드 번호"를 직접 들고 있다가 값이 달라지는 순간을
        // 우리가 스스로 감지해서 배너를 띄웁니다.
        private RoundManager roundManager;
        private int lastShownRound = -1;
        private float roundLabelTimer = 0f;
        private const float ROUND_LABEL_DURATION = 5f;

        private bool subscribedToPlayer = false;
        private bool subscribedToReward = false;

        private AttackInfo? pendingAbsorption;
        private int pendingRewardRound = -1;

        // Canvas HUD(GameHudView)가 씬에 있으면 체력바/슬롯은 그쪽이 그립니다.
        // 여기서는 아직 옮기지 않은 라운드 배너, 게임 오버 배너, 선택 팝업만 그립니다.
        private bool hasCanvasHud = false;

        private void Start()
        {
            hasCanvasHud = FindAnyObjectByType<GameHudView>() != null;
        }

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

            // RoundManager는 씬 로드 타이밍에 따라 이 오브젝트보다 늦게 준비될 수 있으므로
            // 못 찾았으면 다음 프레임에 다시 시도합니다.
            if (roundManager == null) roundManager = FindAnyObjectByType<RoundManager>();

            UpdateRoundLabel();
        }

        /// <summary>
        /// 라운드 번호가 바뀐 순간을 감지해서 표시 타이머를 리셋합니다.
        /// lastShownRound의 초기값이 -1이라, 게임 시작 직후 1라운드가 되는 첫 순간에도
        /// "값이 바뀌었다"고 인식해서 자연스럽게 배너가 뜹니다.
        /// </summary>
        private void UpdateRoundLabel()
        {
            if (roundManager != null && roundManager.CurrentRound != lastShownRound)
            {
                lastShownRound = roundManager.CurrentRound;
                roundLabelTimer = ROUND_LABEL_DURATION;
            }

            if (roundLabelTimer > 0f)
            {
                roundLabelTimer -= Time.deltaTime;
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
            if (hasCanvasHud == false)
            {
                DrawHealthBar();
                DrawSlotBar();
            }

            DrawRoundLabel();
            DrawGameOverBanner();

            // 선택 팝업이 떠 있는 동안은 그것만 그린다(동시에 두 개가 뜨는 상황은 없다고 가정).
            if (pendingAbsorption != null) DrawAbsorptionPopup();
            else if (pendingRewardRound >= 0) DrawRewardPopup();
        }

        // GameManager가 GameOver 상태가 되면(체력 0) 화면 중앙에 표시합니다.
        // 이 상태는 되돌아가지 않는 종료 상태이므로, 별도 타이머 없이 CurrentState를
        // 그대로 반영하면 됩니다 — GameManager가 5초 뒤 Play를 멈추기 전까지 계속 보입니다.
        private void DrawGameOverBanner()
        {
            if (GameManager.Instance == null) return;
            if (GameManager.Instance.CurrentState != GameFlowState.GameOver) return;

            GUIStyle style = new GUIStyle(GUI.skin.box)
            {
                fontSize = 40,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.red },
            };

            Rect area = new Rect(Screen.width / 2 - 200, Screen.height / 2 - 60, 400, 80);
            GUI.Box(area, "GAME OVER", style);
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

        // 라운드가 바뀐 직후 화면 중앙에 잠깐 떴다가 사라지는 배너입니다.
        // 타이머(roundLabelTimer)는 Update()에서 매 프레임 감소합니다.
        private void DrawRoundLabel()
        {
            if (roundManager == null || roundLabelTimer <= 0f) return;

            string label = roundManager.IsBossRound
                ? $"BOSS ROUND {roundManager.CurrentRound}"
                : $"Round {roundManager.CurrentRound}";

            GUIStyle style = new GUIStyle(GUI.skin.box)
            {
                fontSize = 32,
                alignment = TextAnchor.MiddleCenter,
            };

            Rect area = new Rect(Screen.width / 2 - 160, Screen.height / 2 - 100, 320, 60);
            GUI.Box(area, label, style);
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
