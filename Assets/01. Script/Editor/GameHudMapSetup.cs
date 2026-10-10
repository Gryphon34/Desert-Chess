using System.Collections.Generic;
using System.Linq;
using LDtkUnity;
using Study.Utilities;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Study_ActionPlatformer.EditorTools
{
    /// <summary>
    /// UI–Map 연결에 필요한 프리팹/씬 설정을 메뉴 한 번으로 적용합니다.
    ///
    /// - Game HUD 프리팹: 슬롯을 6칸(무기 3 + 마법 3)으로 맞추고, 슬롯 라벨과 진행도 텍스트를
    ///   추가한 뒤 GameHudView를 붙여 참조를 연결합니다.
    /// - Game 씬: LDtk 맵과 MapManager를 배치하고 RoundManager/GameManager/카메라에 연결합니다.
    ///
    /// 여러 번 실행해도 이미 있는 것은 다시 만들지 않습니다.
    /// </summary>
    public static class GameHudMapSetup
    {
        private const string HUD_PREFAB_PATH = "Assets/01. Script/Game HUD.prefab";
        private const string GAME_SCENE_PATH = "Assets/Scenes/Game.unity";
        private const string MAP_ASSET_PATH = "Assets/98. Tools/00. LDTK/ReferenceLayout.ldtk";

        private const int WEAPON_SLOT_COUNT = 3;
        private const int MAGIC_SLOT_COUNT = 3;
        private const string SLOT_PREFIX = "Slot ";

        [MenuItem("Desert Chess/UI-Map 연결 설정 (전체)", priority = 0)]
        public static void SetupAll()
        {
            if (SetupHudPrefab()) SetupGameScene();
        }

        [MenuItem("Desert Chess/1. Game HUD 프리팹 설정", priority = 11)]
        public static bool SetupHudPrefab()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(HUD_PREFAB_PATH);
            try
            {
                Transform equipment = FindDeep(root.transform, "Equipment Group");
                Transform overlay = FindDeep(root.transform, "Map Overlay Panel");
                TMP_Text mapTitle = GetText(root.transform, "Map Title Text");

                if (equipment == null || overlay == null || mapTitle == null)
                {
                    Debug.LogError("GameHudMapSetup ::: Game HUD 프리팹 구조가 예상과 다릅니다 " +
                        "(Equipment Group / Map Overlay Panel / Map Title Text).");
                    return false;
                }

                List<Transform> slots = EnsureSlots(equipment, WEAPON_SLOT_COUNT + MAGIC_SLOT_COUNT);
                if (slots == null) return false;

                TMP_Text styleSource = GetText(slots[0], "Key Text");
                foreach (Transform slot in slots)
                {
                    EnsureSlotLabel(slot, styleSource);
                }

                TMP_Text progress = EnsureProgressText(overlay, mapTitle);
                overlay.gameObject.SetActive(false);

                GameHudView view = root.GetComponent<GameHudView>();
                if (view == null) view = root.AddComponent<GameHudView>();

                SerializedObject so = new SerializedObject(view);
                so.FindProperty("hpFill").objectReferenceValue = FindDeep(root.transform, "HP Fill") as RectTransform;
                so.FindProperty("hpText").objectReferenceValue = GetText(root.transform, "HP Text");
                so.FindProperty("levelText").objectReferenceValue = GetText(root.transform, "Level Text");
                so.FindProperty("mapOverlayPanel").objectReferenceValue = overlay.gameObject;
                so.FindProperty("mapTitleText").objectReferenceValue = mapTitle;
                so.FindProperty("progressText").objectReferenceValue = progress;

                WriteSlots(so.FindProperty("weaponSlots"), slots, 0, WEAPON_SLOT_COUNT);
                WriteSlots(so.FindProperty("magicSlots"), slots, WEAPON_SLOT_COUNT, MAGIC_SLOT_COUNT);
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, HUD_PREFAB_PATH);
                Debug.Log("GameHudMapSetup ::: Game HUD 프리팹 설정 완료 (슬롯 6칸, GameHudView 연결).");
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [MenuItem("Desert Chess/2. Game 씬에 맵 연결", priority = 12)]
        public static void SetupGameScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != GAME_SCENE_PATH)
            {
                if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo() == false) return;
                scene = EditorSceneManager.OpenScene(GAME_SCENE_PATH, OpenSceneMode.Single);
            }

            // 1) LDtk 맵
            LDtkComponentProject project = Object.FindAnyObjectByType<LDtkComponentProject>(FindObjectsInactive.Include);
            if (project == null)
            {
                GameObject mapAsset = AssetDatabase.LoadAssetAtPath<GameObject>(MAP_ASSET_PATH);
                if (mapAsset == null)
                {
                    Debug.LogError($"GameHudMapSetup ::: {MAP_ASSET_PATH}를 불러오지 못했습니다. LDtk 임포트 오류를 확인해주세요.");
                    return;
                }

                GameObject mapObject = (GameObject)PrefabUtility.InstantiatePrefab(mapAsset, scene);
                mapObject.transform.position = Vector3.zero;
                Undo.RegisterCreatedObjectUndo(mapObject, "Add LDtk Map");
                project = mapObject.GetComponent<LDtkComponentProject>();
            }

            // 2) MapManager
            MapManager mapManager = Object.FindAnyObjectByType<MapManager>(FindObjectsInactive.Include);
            if (mapManager == null)
            {
                GameObject go = new GameObject("MapManager");
                SceneManager.MoveGameObjectToScene(go, scene);
                Undo.RegisterCreatedObjectUndo(go, "Add MapManager");
                mapManager = go.AddComponent<MapManager>();
            }

            SetReference(mapManager, "mapRoot", project != null ? project.gameObject : null);
            SetReference(mapManager, "followCamera", Object.FindAnyObjectByType<SimpleFollowCamera>(FindObjectsInactive.Include));
            SetReference(Object.FindAnyObjectByType<RoundManager>(FindObjectsInactive.Include), "mapManager", mapManager);
            SetReference(Object.FindAnyObjectByType<GameManager>(FindObjectsInactive.Include), "mapManager", mapManager);

            // 3) 예전 테스트 바닥은 1라운드 맵과 겹치므로 끕니다(삭제하지 않습니다).
            GameObject oldEnvironment = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Environment");
            if (oldEnvironment != null && oldEnvironment.activeSelf)
            {
                Undo.RecordObject(oldEnvironment, "Disable old Environment");
                oldEnvironment.SetActive(false);
                Debug.Log("GameHudMapSetup ::: 예전 테스트 바닥(Environment)을 비활성화했습니다.");
            }

            // 4) HUD가 없으면 배치
            if (Object.FindAnyObjectByType<GameHudView>(FindObjectsInactive.Include) == null)
            {
                GameObject hudAsset = AssetDatabase.LoadAssetAtPath<GameObject>(HUD_PREFAB_PATH);
                if (hudAsset != null)
                {
                    GameObject hud = (GameObject)PrefabUtility.InstantiatePrefab(hudAsset, scene);
                    Undo.RegisterCreatedObjectUndo(hud, "Add Game HUD");
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            AddSceneToBuildSettings(GAME_SCENE_PATH);
            Debug.Log("GameHudMapSetup ::: Game 씬 맵 연결 완료.");
        }

        #region Prefab Helpers

        // 기존 Slot 0~4를 유지하고, 모자라면 마지막 슬롯을 복제해 채웁니다.
        private static List<Transform> EnsureSlots(Transform equipment, int count)
        {
            List<Transform> slots = new List<Transform>();
            foreach (Transform child in equipment)
            {
                if (child.name.StartsWith(SLOT_PREFIX)) slots.Add(child);
            }

            if (slots.Count == 0)
            {
                Debug.LogError("GameHudMapSetup ::: Equipment Group 아래에 Slot이 없습니다.");
                return null;
            }

            while (slots.Count < count)
            {
                GameObject clone = Object.Instantiate(slots[slots.Count - 1].gameObject, equipment);
                clone.name = SLOT_PREFIX + slots.Count;
                slots.Add(clone.transform);
            }

            // 남는 슬롯은 지우지 않고 꺼둡니다.
            for (int i = 0; i < slots.Count; ++i)
            {
                slots[i].gameObject.SetActive(i < count);
            }

            return slots.GetRange(0, count);
        }

        private static void EnsureSlotLabel(Transform slot, TMP_Text styleSource)
        {
            TextMeshProUGUI label = EnsureText(slot, "Label Text", styleSource);
            RectTransform rt = label.rectTransform;
            rt.anchorMin = new Vector2(0f, 0.35f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            label.fontSize = 13;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            label.raycastTarget = false;
            label.text = string.Empty;
        }

        private static TMP_Text EnsureProgressText(Transform overlay, TMP_Text styleSource)
        {
            TextMeshProUGUI progress = EnsureText(overlay, "Progress Text", styleSource);
            RectTransform rt = progress.rectTransform;
            rt.anchorMin = new Vector2(0.1f, 0.2f);
            rt.anchorMax = new Vector2(0.9f, 0.45f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            progress.fontSize = 36;
            progress.alignment = TextAlignmentOptions.Center;
            progress.richText = true;
            progress.raycastTarget = false;
            progress.text = "1 - 2 - 3 - 4 - 5 - BOSS";
            return progress;
        }

        private static TextMeshProUGUI EnsureText(Transform parent, string name, TMP_Text styleSource)
        {
            Transform t = parent.Find(name);
            if (t == null)
            {
                GameObject go = new GameObject(name, typeof(RectTransform));
                go.layer = parent.gameObject.layer;
                go.transform.SetParent(parent, false);
                t = go.transform;
            }

            TextMeshProUGUI text = t.GetComponent<TextMeshProUGUI>();
            if (text == null) text = t.gameObject.AddComponent<TextMeshProUGUI>();

            if (styleSource != null)
            {
                text.font = styleSource.font;
                text.fontSharedMaterial = styleSource.fontSharedMaterial;
                text.color = styleSource.color;
            }

            return text;
        }

        private static void WriteSlots(SerializedProperty array, List<Transform> slots, int start, int count)
        {
            array.arraySize = count;
            for (int i = 0; i < count; ++i)
            {
                Transform slot = slots[start + i];
                SerializedProperty element = array.GetArrayElementAtIndex(i);

                Transform icon = slot.Find("Icon");
                element.FindPropertyRelative("frame").objectReferenceValue = slot.GetComponent<Image>();
                element.FindPropertyRelative("icon").objectReferenceValue = icon != null ? icon.GetComponent<Image>() : null;
                element.FindPropertyRelative("keyText").objectReferenceValue = GetText(slot, "Key Text");
                element.FindPropertyRelative("labelText").objectReferenceValue = GetText(slot, "Label Text");
            }
        }

        #endregion

        #region Common Helpers

        private static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;

            foreach (Transform child in root)
            {
                Transform found = FindDeep(child, name);
                if (found != null) return found;
            }

            return null;
        }

        private static TMP_Text GetText(Transform root, string name)
        {
            Transform t = FindDeep(root, name);
            return t != null ? t.GetComponent<TMP_Text>() : null;
        }

        private static void SetReference(Object target, string propertyName, Object value)
        {
            if (target == null)
            {
                Debug.LogWarning($"GameHudMapSetup ::: '{propertyName}'를 연결할 대상이 씬에 없습니다.");
                return;
            }

            SerializedObject so = new SerializedObject(target);
            SerializedProperty property = so.FindProperty(propertyName);
            if (property == null)
            {
                Debug.LogWarning($"GameHudMapSetup ::: {target.GetType().Name}에 '{propertyName}' 필드가 없습니다.");
                return;
            }

            property.objectReferenceValue = value;
            so.ApplyModifiedProperties();
        }

        private static void AddSceneToBuildSettings(string path)
        {
            List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(s => s.path == path)) return;

            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log($"GameHudMapSetup ::: 빌드 설정에 {path}를 추가했습니다.");
        }

        #endregion
    }
}
