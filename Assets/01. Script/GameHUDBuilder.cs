#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;

public class GameHUDBuilder : MonoBehaviour
{
    [SerializeField] private TMP_FontAsset font;

    private const string RootName = "Game HUD";
    private const int RefWidth = 1920;
    private const int RefHeight = 1080;

    [ContextMenu("1. Build UI")]
    public void BuildUI()
    {
        if (GameObject.Find(RootName) != null)
        {
            Debug.LogWarning("UI already exists. Please clear it first to avoid overlapping.");
            return;
        }

        if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            Debug.LogWarning("No EventSystem found in the scene. UI interactions may not work.");
        }

        GameObject root = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(root, "Create UI");

        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(RefWidth, RefHeight);
        scaler.matchWidthOrHeight = 0.5f;

        root.AddComponent<GraphicRaycaster>();

        CreateBottomLeftArea(root.transform);
        CreateBottomRightArea(root.transform);
        CreateHiddenMapOverlay(root.transform);

        EditorSceneManager.MarkSceneDirty(root.scene);
        Debug.Log("UI Build Complete.");
    }

    [ContextMenu("2. Clear UI")]
    public void ClearUI()
    {
        GameObject root = GameObject.Find(RootName);
        if (root != null)
        {
            Undo.DestroyObjectImmediate(root);
            Debug.Log($"Cleared UI: {RootName}");
        }
        else
        {
            Debug.Log("No UI found to clear.");
        }
    }

    #region UI Sections

    private void CreateBottomLeftArea(Transform canvas)
    {
        GameObject area = NewUI("Bottom Left Area", canvas);
        RectTransform areaRect = area.AddComponent<RectTransform>();
        areaRect.anchorMin = new Vector2(0, 0);
        areaRect.anchorMax = new Vector2(0, 0);
        areaRect.pivot = new Vector2(0, 0);
        areaRect.anchoredPosition = new Vector2(40, 40);
        areaRect.sizeDelta = new Vector2(600, 300);

        // Resources Group
        GameObject resGroup = NewUI("Resources Group", area.transform);
        RectTransform resRect = resGroup.AddComponent<RectTransform>();
        resRect.anchorMin = new Vector2(0, 1);
        resRect.anchorMax = new Vector2(0, 1);
        resRect.pivot = new Vector2(0, 1);
        resRect.anchoredPosition = new Vector2(0, -20);
        resRect.sizeDelta = new Vector2(300, 80);

        GameObject cellsIcon = NewPanel("Cells Icon", resGroup.transform, new Vector2(30, 30));
        cellsIcon.GetComponent<Image>().color = new Color(0.2f, 0.6f, 1f);
        RectTransform cIconRect = cellsIcon.GetComponent<RectTransform>();
        cIconRect.anchorMin = new Vector2(0, 1);
        cIconRect.anchorMax = new Vector2(0, 1);
        cIconRect.pivot = new Vector2(0, 1);
        cIconRect.anchoredPosition = new Vector2(0, 0);

        TMP_Text cellsText = NewText("Cells Text", resGroup.transform, "0", 24, TextAlignmentOptions.MidlineLeft);
        cellsText.rectTransform.anchorMin = new Vector2(0, 1);
        cellsText.rectTransform.anchorMax = new Vector2(0, 1);
        cellsText.rectTransform.pivot = new Vector2(0, 1);
        cellsText.rectTransform.anchoredPosition = new Vector2(40, 0);
        cellsText.rectTransform.sizeDelta = new Vector2(100, 30);

        GameObject goldIcon = NewPanel("Gold Icon", resGroup.transform, new Vector2(30, 30));
        goldIcon.GetComponent<Image>().color = new Color(1f, 0.8f, 0f);
        RectTransform gIconRect = goldIcon.GetComponent<RectTransform>();
        gIconRect.anchorMin = new Vector2(0, 1);
        gIconRect.anchorMax = new Vector2(0, 1);
        gIconRect.pivot = new Vector2(0, 1);
        gIconRect.anchoredPosition = new Vector2(0, -40);

        TMP_Text goldText = NewText("Gold Text", resGroup.transform, "2144", 24, TextAlignmentOptions.MidlineLeft);
        goldText.rectTransform.anchorMin = new Vector2(0, 1);
        goldText.rectTransform.anchorMax = new Vector2(0, 1);
        goldText.rectTransform.pivot = new Vector2(0, 1);
        goldText.rectTransform.anchoredPosition = new Vector2(40, -40);
        goldText.rectTransform.sizeDelta = new Vector2(100, 30);

        // Equipment Group
        GameObject equipGroup = NewUI("Equipment Group", area.transform);
        RectTransform equipRect = equipGroup.AddComponent<RectTransform>();
        equipRect.anchorMin = new Vector2(0, 0.5f);
        equipRect.anchorMax = new Vector2(0, 0.5f);
        equipRect.pivot = new Vector2(0, 0.5f);
        equipRect.anchoredPosition = new Vector2(0, 0);
        equipRect.sizeDelta = new Vector2(500, 90);

        HorizontalLayoutGroup hlg = equipGroup.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.spacing = 15;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;

        string[] slotLabels = { "Lb", "X", "Y", "LT", "RT" };
        Color[] slotColors = { 
            new Color(0.8f, 0.4f, 0.1f), 
            new Color(0.3f, 0.8f, 0.3f), 
            new Color(0.8f, 0.3f, 0.3f), 
            new Color(0.3f, 0.3f, 0.8f), 
            new Color(0.3f, 0.3f, 0.8f) 
        };

        for (int i = 0; i < 5; i++)
        {
            GameObject slot = NewPanel($"Slot {i}", equipGroup.transform, new Vector2(70, 70));
            slot.GetComponent<Image>().color = new Color(0.1f, 0.1f, 0.15f, 0.9f);

            GameObject icon = NewPanel("Icon", slot.transform, new Vector2(40, 40));
            icon.GetComponent<Image>().color = slotColors[i];
            icon.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 5);

            TMP_Text keyText = NewText("Key Text", slot.transform, slotLabels[i], 16, TextAlignmentOptions.Bottom);
            keyText.rectTransform.anchorMin = new Vector2(0, 0);
            keyText.rectTransform.anchorMax = new Vector2(1, 0);
            keyText.rectTransform.pivot = new Vector2(0.5f, 0);
            keyText.rectTransform.anchoredPosition = new Vector2(0, -20);
            keyText.rectTransform.sizeDelta = new Vector2(0, 30);
        }

        // Health Group
        GameObject hpGroup = NewUI("Health Group", area.transform);
        RectTransform hpRect = hpGroup.AddComponent<RectTransform>();
        hpRect.anchorMin = new Vector2(0, 0);
        hpRect.anchorMax = new Vector2(0, 0);
        hpRect.pivot = new Vector2(0, 0);
        hpRect.anchoredPosition = new Vector2(0, 0);
        hpRect.sizeDelta = new Vector2(500, 40);

        GameObject lvlBadge = NewPanel("Level Badge", hpGroup.transform, new Vector2(40, 40));
        lvlBadge.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.3f);
        RectTransform lvlRect = lvlBadge.GetComponent<RectTransform>();
        lvlRect.anchorMin = new Vector2(0, 0.5f);
        lvlRect.anchorMax = new Vector2(0, 0.5f);
        lvlRect.pivot = new Vector2(0, 0.5f);
        lvlRect.anchoredPosition = new Vector2(20, 0);

        TMP_Text lvlText = NewText("Level Text", lvlBadge.transform, "1", 20, TextAlignmentOptions.Center);
        Stretch(lvlText.rectTransform, 0);

        GameObject hpBg = NewPanel("HP Background", hpGroup.transform, new Vector2(440, 25));
        hpBg.GetComponent<Image>().color = new Color(0.1f, 0.1f, 0.1f, 0.8f);
        RectTransform bgRect = hpBg.GetComponent<RectTransform>();
        bgRect.anchorMin = new Vector2(0, 0.5f);
        bgRect.anchorMax = new Vector2(0, 0.5f);
        bgRect.pivot = new Vector2(0, 0.5f);
        bgRect.anchoredPosition = new Vector2(50, 0);

        GameObject hpFill = NewPanel("HP Fill", hpBg.transform, new Vector2(440, 25));
        hpFill.GetComponent<Image>().color = new Color(0.2f, 0.9f, 0.3f, 1f);
        RectTransform fillRect = hpFill.GetComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0, 0.5f);
        fillRect.anchorMax = new Vector2(0, 0.5f);
        fillRect.pivot = new Vector2(0, 0.5f);
        fillRect.anchoredPosition = new Vector2(0, 0);

        TMP_Text hpText = NewText("HP Text", hpBg.transform, "100 / 100", 18, TextAlignmentOptions.Center);
        Stretch(hpText.rectTransform, 0);
    }

    private void CreateBottomRightArea(Transform canvas)
    {
        GameObject area = NewUI("Bottom Right Area", canvas);
        RectTransform areaRect = area.AddComponent<RectTransform>();
        areaRect.anchorMin = new Vector2(1, 0);
        areaRect.anchorMax = new Vector2(1, 0);
        areaRect.pivot = new Vector2(1, 0);
        areaRect.anchoredPosition = new Vector2(-40, 40);
        areaRect.sizeDelta = new Vector2(300, 200);

        // Stats Group
        GameObject statsGroup = NewUI("Stats Group", area.transform);
        RectTransform statsRect = statsGroup.AddComponent<RectTransform>();
        statsRect.anchorMin = new Vector2(1, 1);
        statsRect.anchorMax = new Vector2(1, 1);
        statsRect.pivot = new Vector2(1, 1);
        statsRect.anchoredPosition = new Vector2(0, 0);
        statsRect.sizeDelta = new Vector2(150, 60);

        VerticalLayoutGroup vlg = statsGroup.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.MiddleRight;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;

        NewText("Score Text", statsGroup.transform, "890609", 20, TextAlignmentOptions.MidlineRight).rectTransform.sizeDelta = new Vector2(0, 25);
        NewText("Time Text", statsGroup.transform, "32s", 20, TextAlignmentOptions.MidlineRight).rectTransform.sizeDelta = new Vector2(0, 25);

        // Minimap Panel
        GameObject minimap = NewPanel("Minimap Panel", area.transform, new Vector2(250, 120));
        minimap.GetComponent<Image>().color = new Color(0.1f, 0.1f, 0.25f, 0.9f);
        RectTransform mapRect = minimap.GetComponent<RectTransform>();
        mapRect.anchorMin = new Vector2(1, 0);
        mapRect.anchorMax = new Vector2(1, 0);
        mapRect.pivot = new Vector2(1, 0);
        mapRect.anchoredPosition = new Vector2(0, 0);
        
        GameObject mapContent = NewPanel("Map Content", minimap.transform, new Vector2(100, 20));
        mapContent.GetComponent<Image>().color = new Color(0.4f, 0.5f, 0.8f, 1f);
        mapContent.GetComponent<RectTransform>().anchoredPosition = new Vector2(10, 10);
    }

    private void CreateHiddenMapOverlay(Transform canvas)
    {
        GameObject mapOverlay = NewPanel("Map Overlay Panel", canvas, Vector2.zero);
        mapOverlay.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.9f);
        Stretch(mapOverlay.GetComponent<RectTransform>(), 50);

        TMP_Text titleText = NewText("Map Title Text", mapOverlay.transform, "FULLSCREEN MAP OVERLAY", 60, TextAlignmentOptions.Center);
        Stretch(titleText.rectTransform, 0);

        mapOverlay.SetActive(false);
    }

    #endregion

    #region Helper Methods

    private GameObject NewUI(string name, Transform parent)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go;
    }

    private GameObject NewPanel(string name, Transform parent, Vector2 size)
    {
        GameObject go = NewUI(name, parent);
        go.AddComponent<Image>().color = new Color(1f, 1f, 1f, 1f);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = size;
        return go;
    }

    private TMP_Text NewText(string name, Transform parent, string text, float size, TextAlignmentOptions align)
    {
        GameObject go = NewUI(name, parent);
        TMP_Text tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.alignment = align;
        if (font != null) tmp.font = font;
        return tmp;
    }

    private Button NewButton(string name, Transform parent, string label, out TMP_Text labelText)
    {
        GameObject go = NewPanel(name, parent, new Vector2(160, 60));
        Button btn = go.AddComponent<Button>();
        labelText = NewText("Label Text", go.transform, label, 24, TextAlignmentOptions.Center);
        Stretch(labelText.rectTransform, 0);
        return btn;
    }

    private void Stretch(RectTransform rect, float padding)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(padding, padding);
        rect.offsetMax = new Vector2(-padding, -padding);
    }

    #endregion
}
#endif