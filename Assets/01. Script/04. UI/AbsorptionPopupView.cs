using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Study_ActionPlatformer
{
    /// <summary>
    /// 기획서 6-2 몬스터 흡수 선택 팝업 (Canvas).
    ///
    /// Player.AbsorptionChoiceRequested를 받으면 게임을 멈추고 선택지를 보여줍니다.
    /// - 빈 슬롯이 있으면 : [Absorb] [Skip]
    /// - 슬롯이 가득 차면 : 교체할 슬롯 버튼들 + [Skip]
    /// 여러 마리가 한꺼번에 죽으면 Player가 차례로 다시 요청하므로, 대기 중인 선택이 없어질 때까지 열려 있습니다.
    ///
    /// UI는 GameHudView가 붙어 있는 Canvas 아래에 코드로 만듭니다.
    /// 한글 TMP 폰트가 아직 없어서 문구는 영어입니다.
    /// </summary>
    public class AbsorptionPopupView : MonoBehaviour
    {
        private static readonly Color DimColor = new Color(0f, 0f, 0f, 0.6f);
        private static readonly Color PanelColor = new Color(0.1f, 0.1f, 0.12f, 0.95f);
        private static readonly Color ButtonColor = new Color(0.25f, 0.25f, 0.3f, 1f);
        private static readonly Color AcceptButtonColor = new Color(0.85f, 0.65f, 0.2f, 1f);

        private GameObject root;
        private TMP_Text titleText;
        private TMP_Text bodyText;
        private RectTransform buttonArea;
        private readonly List<GameObject> buttons = new List<GameObject>();
        private TMP_FontAsset font;

        private Player player;
        private bool pausedByPopup;

        public bool IsOpen => root != null && root.activeSelf;

        private void Awake()
        {
            // HUD와 같은 폰트를 씁니다(없으면 TMP 기본 폰트).
            TMP_Text sample = GetComponentInChildren<TMP_Text>(true);
            font = sample != null ? sample.font : null;

            BuildUI();
            root.SetActive(false);
        }

        private void Update()
        {
            // Player가 이 오브젝트보다 늦게 생길 수 있어 준비될 때까지 구독을 다시 시도합니다.
            if (player == null && Player.LocalPlayer != null)
            {
                player = Player.LocalPlayer;
                player.AbsorptionChoiceRequested += OnAbsorptionChoiceRequested;

                if (player.HasPendingAbsorption) Show(player.PendingAbsorption);
            }
        }

        private void OnDisable()
        {
            if (player != null)
            {
                player.AbsorptionChoiceRequested -= OnAbsorptionChoiceRequested;
                player = null;
            }

            // 팝업이 열린 채로 꺼지면 timeScale이 0으로 남으므로 여기서 되돌립니다.
            Close();
        }

        private void OnAbsorptionChoiceRequested(AttackInfo dropped)
        {
            Show(dropped);
        }

        private void Show(AttackInfo dropped)
        {
            if (player == null) return;

            EnsureEventSystem();
            ClearButtons();

            bool isMagic = dropped.Category == AttackSlotCategory.Magic;
            bool hasEmptySlot = player.HasEmptySlotFor(dropped.Category);

            titleText.text = $"ABSORB {dropped.Id.ToString().ToUpper()}?";
            bodyText.text =
                $"{(isMagic ? "Magic" : "Weapon")}   DMG {dropped.MinDamage}-{dropped.MaxDamage}   " +
                $"SPD {dropped.Speed}   RNG {dropped.Range}   USES {dropped.RemainingUses}" +
                (hasEmptySlot ? "" : $"\n\n{(isMagic ? "Magic" : "Weapon")} slots are full. Choose a slot to replace.");

            if (hasEmptySlot)
            {
                AddButton("Absorb", AcceptButtonColor, () => Resolve(() => player.AcceptAbsorption()));
            }
            else
            {
                int slotCount = isMagic ? player.MagicSlotCount : player.WeaponSlotCount;
                for (int i = 0; i < slotCount; ++i)
                {
                    int slotIndex = i;
                    AttackInfo current = isMagic ? player.GetMagicSlot(i) : player.GetWeaponSlot(i);
                    AddButton($"Replace {i + 1}: {FormatSlot(current)}", ButtonColor,
                        () => Resolve(() => player.ConfirmAbsorption(slotIndex)));
                }
            }

            AddButton("Skip", ButtonColor, () => Resolve(() => player.DeclineAbsorption()));

            root.SetActive(true);
            root.transform.SetAsLastSibling();

            if (pausedByPopup == false)
            {
                pausedByPopup = true;
                Time.timeScale = 0f;
            }
        }

        // 선택을 Player에 전달한 뒤, 기다리는 선택이 남아 있으면 다음 것을 보여주고 아니면 닫습니다.
        private void Resolve(System.Action choice)
        {
            if (player == null)
            {
                Close();
                return;
            }

            choice();

            if (player.HasPendingAbsorption) Show(player.PendingAbsorption);
            else Close();
        }

        private void Close()
        {
            if (root != null) root.SetActive(false);

            // 다른 곳(게임 오버 등)에서 멈춘 경우까지 되살리지 않도록, 내가 멈춘 경우에만 원복합니다.
            if (pausedByPopup)
            {
                pausedByPopup = false;
                Time.timeScale = 1f;
            }
        }

        private static string FormatSlot(AttackInfo info)
        {
            if (info.IsEmpty) return "Empty";

            string uses = info.RemainingUses == Player.UNLIMITED_USES ? "inf" : info.RemainingUses.ToString();
            return $"{info.Id} x{uses}";
        }

        // 씬에 EventSystem이 없으면 버튼 클릭이 동작하지 않으므로 처음 열 때 만들어 둡니다.
        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null || FindAnyObjectByType<EventSystem>() != null) return;

            GameObject go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            DontDestroyOnLoad(go);
        }

        #region UI 생성

        private void BuildUI()
        {
            // 화면 전체를 어둡게 덮어 뒤쪽 클릭을 막습니다.
            root = CreateRect("Absorption Popup", transform);
            Stretch(root.GetComponent<RectTransform>());
            root.AddComponent<Image>().color = DimColor;

            GameObject panel = CreateRect("Panel", root.transform);
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(560f, 0f);
            panel.AddComponent<Image>().color = PanelColor;

            VerticalLayoutGroup layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(28, 28, 24, 24);
            layout.spacing = 14f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            ContentSizeFitter fitter = panel.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            titleText = CreateText("Title", panel.transform, 34f, FontStyles.Bold);
            bodyText = CreateText("Body", panel.transform, 22f, FontStyles.Normal);

            GameObject area = CreateRect("Buttons", panel.transform);
            buttonArea = area.GetComponent<RectTransform>();
            VerticalLayoutGroup buttonLayout = area.AddComponent<VerticalLayoutGroup>();
            buttonLayout.spacing = 8f;
            buttonLayout.childControlWidth = true;
            buttonLayout.childControlHeight = true;
            buttonLayout.childForceExpandWidth = true;
            buttonLayout.childForceExpandHeight = false;
        }

        private void AddButton(string label, Color color, UnityEngine.Events.UnityAction onClick)
        {
            GameObject go = CreateRect($"Button ({label})", buttonArea);
            go.AddComponent<Image>().color = color;
            go.AddComponent<LayoutElement>().minHeight = 52f;

            Button button = go.AddComponent<Button>();
            button.onClick.AddListener(onClick);

            TMP_Text text = CreateText("Label", go.transform, 24f, FontStyles.Bold);
            Stretch(text.rectTransform);

            buttons.Add(go);
            text.text = label;
        }

        private void ClearButtons()
        {
            foreach (GameObject go in buttons)
            {
                // 같은 프레임에 다시 만들므로 레이아웃에서 바로 빠지도록 먼저 끕니다.
                go.SetActive(false);
                Destroy(go);
            }
            buttons.Clear();
        }

        private TMP_Text CreateText(string name, Transform parent, float size, FontStyles style)
        {
            GameObject go = CreateRect(name, parent);
            TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
            if (font != null) text.font = font;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }

        private static GameObject CreateRect(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            return go;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        #endregion
    }
}
