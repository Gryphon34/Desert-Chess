using System;
using System.Text;
using Study.Utilities;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Study_ActionPlatformer
{
    /// <summary>
    /// Game HUD 프리팹(Canvas)에 Player / RoundManager / MapManager 데이터를 연결합니다.
    ///
    /// 값을 매 프레임 확인하지 않고, 각 객체가 보내는 이벤트(HpChanged, SlotsChanged,
    /// RoundStarted, KillProgressChanged, MapChanged)를 받을 때만 화면을 갱신합니다.
    /// 슬롯은 기획서대로 무기 3칸 + 마법 3칸이며, 키 표시는 PlayerController의 1~6키와 같습니다.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class GameHudView : MonoBehaviour
    {
        [Serializable]
        public class SlotView
        {
            public Image frame;
            public Image icon;
            public TMP_Text keyText;
            public TMP_Text labelText;
        }

        [Header("Health")]
        [SerializeField] private RectTransform hpFill;
        [SerializeField] private TMP_Text hpText;

        [Header("Round")]
        [SerializeField] private TMP_Text levelText;

        [Header("Slots (무기 3 + 마법 3)")]
        [SerializeField] private SlotView[] weaponSlots = new SlotView[3];
        [SerializeField] private SlotView[] magicSlots = new SlotView[3];
        [SerializeField] private Color slotNormalColor = new Color(0f, 0f, 0f, 0.6f);
        [SerializeField] private Color slotActiveColor = new Color(1f, 0.84f, 0.29f, 0.9f);
        [SerializeField] private Color weaponIconColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        [SerializeField] private Color magicIconColor = new Color(0.45f, 0.7f, 1f, 1f);
        [SerializeField] private float activeSlotScale = 1.1f;

        [Header("Map Overlay")]
        [SerializeField] private GameObject mapOverlayPanel;
        [SerializeField] private TMP_Text mapTitleText;
        [SerializeField] private TMP_Text progressText;
        [SerializeField] private Key overlayToggleKey = Key.Tab;
        [SerializeField] private Color clearedRoundColor = new Color(0.55f, 0.55f, 0.55f, 1f);
        [SerializeField] private Color currentRoundColor = new Color(1f, 0.84f, 0.29f, 1f);
        [SerializeField] private Color upcomingRoundColor = Color.white;

        private Player player;
        private RoundManager roundManager;
        private MapManager mapManager;

        private float hpFillFullWidth;
        private int killed;
        private int killGoal;
        private bool currentRoundCleared;

        private readonly StringBuilder builder = new StringBuilder();

        private void Awake()
        {
            if (hpFill != null) hpFillFullWidth = hpFill.sizeDelta.x;
            if (mapOverlayPanel != null) mapOverlayPanel.SetActive(false);
        }

        private void OnEnable()
        {
            // GameManager.Start()가 1라운드를 바로 시작하므로, 매니저 이벤트는 Start보다
            // 이른 OnEnable에서 구독해야 첫 RoundStarted를 놓치지 않습니다.
            BindManagers();
            TryBindPlayer();
            RefreshAll();
        }

        private void OnDisable()
        {
            UnbindPlayer();
            UnbindManagers();
        }

        private void Update()
        {
            // Player가 이 오브젝트보다 늦게 Awake될 수 있어서, 찾을 때까지만 다시 시도합니다.
            if (player == null && TryBindPlayer())
            {
                RefreshHp();
                RefreshSlots();
            }

            if (mapOverlayPanel != null && SimpleInput.GetKeyDown(overlayToggleKey))
            {
                mapOverlayPanel.SetActive(mapOverlayPanel.activeSelf == false);
                if (mapOverlayPanel.activeSelf) RefreshProgress();
            }
        }

        #region Binding

        private void BindManagers()
        {
            if (roundManager == null) roundManager = FindAnyObjectByType<RoundManager>();
            if (mapManager == null) mapManager = FindAnyObjectByType<MapManager>();

            if (roundManager != null)
            {
                roundManager.RoundStarted += OnRoundStarted;
                roundManager.RoundCleared += OnRoundCleared;
                roundManager.KillProgressChanged += OnKillProgressChanged;

                killed = roundManager.DefeatedThisRound;
                killGoal = roundManager.MonstersPerRound;
            }

            if (mapManager != null)
            {
                mapManager.MapChanged += OnMapChanged;
            }
        }

        private void UnbindManagers()
        {
            if (roundManager != null)
            {
                roundManager.RoundStarted -= OnRoundStarted;
                roundManager.RoundCleared -= OnRoundCleared;
                roundManager.KillProgressChanged -= OnKillProgressChanged;
            }

            if (mapManager != null)
            {
                mapManager.MapChanged -= OnMapChanged;
            }
        }

        private bool TryBindPlayer()
        {
            if (player != null) return true;

            player = Player.LocalPlayer;
            if (player == null) return false;

            player.HpChanged += RefreshHp;
            player.SlotsChanged += RefreshSlots;
            return true;
        }

        private void UnbindPlayer()
        {
            if (player == null) return;

            player.HpChanged -= RefreshHp;
            player.SlotsChanged -= RefreshSlots;
            player = null;
        }

        #endregion

        #region Event Handlers

        private void OnRoundStarted(int round)
        {
            currentRoundCleared = false;
            RefreshRound();
            RefreshProgress();
        }

        private void OnRoundCleared(int round)
        {
            currentRoundCleared = true;
            RefreshProgress();
        }

        private void OnKillProgressChanged(int defeated, int goal)
        {
            killed = defeated;
            killGoal = goal;
            RefreshProgress();
        }

        private void OnMapChanged(RoundMap map)
        {
            RefreshMapTitle();
        }

        #endregion

        #region Refresh

        private void RefreshAll()
        {
            RefreshHp();
            RefreshSlots();
            RefreshRound();
            RefreshMapTitle();
            RefreshProgress();
        }

        private void RefreshHp()
        {
            if (player == null || player.BaseStat == null) return;

            int hp = Mathf.Max(0, player.BaseStat.Hp);
            int maxHp = Mathf.Max(1, player.BaseStat.MaxHp);

            // HP Fill은 왼쪽 피벗이고 스프라이트가 없어 Image.fillAmount가 동작하지 않습니다.
            // 너비를 비율만큼 줄여서 표현합니다.
            if (hpFill != null)
            {
                Vector2 size = hpFill.sizeDelta;
                size.x = hpFillFullWidth * Mathf.Clamp01((float)hp / maxHp);
                hpFill.sizeDelta = size;
            }

            if (hpText != null) hpText.text = $"{hp} / {maxHp}";
        }

        private void RefreshSlots()
        {
            if (player == null) return;

            for (int i = 0; i < weaponSlots.Length; ++i)
            {
                bool exists = i < player.WeaponSlotCount;
                AttackInfo info = exists ? player.GetWeaponSlot(i) : default;
                ApplySlot(weaponSlots[i], info, exists && i == player.ActiveWeaponSlot, i + 1, weaponIconColor);
            }

            // 마법 슬롯 키는 PlayerController 기준 4~6입니다.
            for (int i = 0; i < magicSlots.Length; ++i)
            {
                bool exists = i < player.MagicSlotCount;
                AttackInfo info = exists ? player.GetMagicSlot(i) : default;
                ApplySlot(magicSlots[i], info, exists && i == player.ActiveMagicSlot, weaponSlots.Length + i + 1, magicIconColor);
            }
        }

        private void ApplySlot(SlotView view, AttackInfo info, bool isActive, int keyNumber, Color iconColor)
        {
            if (view == null) return;

            if (view.frame != null)
            {
                view.frame.color = isActive ? slotActiveColor : slotNormalColor;
                view.frame.rectTransform.localScale = Vector3.one * (isActive ? activeSlotScale : 1f);
            }

            if (view.icon != null)
            {
                // 아직 무기 아이콘 스프라이트가 없어서, 빈 슬롯은 숨기고 종류별 색만 구분합니다.
                view.icon.enabled = info.IsEmpty == false;
                view.icon.color = iconColor;
            }

            if (view.keyText != null) view.keyText.text = keyNumber.ToString();

            if (view.labelText != null) view.labelText.text = DescribeSlot(info);
        }

        // 주먹처럼 횟수 제한이 없는 무기는 이름만 표시합니다.
        private static string DescribeSlot(AttackInfo info)
        {
            if (info.IsEmpty) return string.Empty;
            if (info.RemainingUses == Player.UNLIMITED_USES) return info.Id.ToString();
            return $"{info.Id}\nx{info.RemainingUses}";
        }

        private void RefreshRound()
        {
            if (levelText == null || roundManager == null) return;
            levelText.text = roundManager.CurrentRound.ToString();
        }

        private void RefreshMapTitle()
        {
            if (mapTitleText == null) return;

            RoundMap map = mapManager != null ? mapManager.CurrentMap : null;
            if (map != null)
            {
                mapTitleText.text = map.DisplayName;
            }
            else if (roundManager != null)
            {
                mapTitleText.text = roundManager.IsBossRound ? "BOSS" : $"ROUND {roundManager.CurrentRound}";
            }
        }

        /// <summary>
        /// 맵 오버레이의 진행도: 라운드 목록(지난/현재/남은)과 이번 라운드 처치 수를 보여줍니다.
        /// </summary>
        private void RefreshProgress()
        {
            if (progressText == null || roundManager == null) return;
            if (mapOverlayPanel != null && mapOverlayPanel.activeInHierarchy == false) return;

            int current = roundManager.CurrentRound;
            int total = GetTotalRounds();

            builder.Clear();
            for (int round = 1; round <= total; ++round)
            {
                bool cleared = round < current || (round == current && currentRoundCleared);
                bool isCurrent = round == current && cleared == false;

                Color color = cleared ? clearedRoundColor : (isCurrent ? currentRoundColor : upcomingRoundColor);
                string label = IsBossRound(round, total) ? "BOSS" : round.ToString();
                if (isCurrent) label = $"<b>[{label}]</b>";

                if (round > 1) builder.Append("   -   ");
                builder.Append("<color=#").Append(ColorUtility.ToHtmlStringRGBA(color)).Append('>')
                       .Append(label).Append("</color>");
            }

            builder.Append("\n\n");
            if (roundManager.IsBossRound)
            {
                builder.Append(currentRoundCleared ? "BOSS DEFEATED" : "DEFEAT THE BOSS");
            }
            else
            {
                builder.Append("KILLS  ").Append(killed).Append(" / ").Append(killGoal);
            }

            progressText.text = builder.ToString();
        }

        private int GetTotalRounds()
        {
            int total = GameManager.Instance != null ? GameManager.Instance.RoundCountToBoss : 0;
            if (mapManager != null) total = Mathf.Max(total, mapManager.MapCount);
            return Mathf.Max(total, roundManager != null ? roundManager.CurrentRound : 1);
        }

        private bool IsBossRound(int round, int total)
        {
            RoundMap map = mapManager != null ? mapManager.GetMap(round) : null;
            return map != null ? map.IsBossRound : round == total;
        }

        #endregion
    }
}
