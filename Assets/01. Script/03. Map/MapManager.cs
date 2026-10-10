using System;
using System.Collections.Generic;
using LDtkUnity;
using Study.Utilities;
using UnityEngine;

namespace Study_ActionPlatformer
{
    /// <summary>
    /// 라운드 번호에 맞는 LDtk 맵을 켜고, 플레이어와 카메라를 그 맵으로 옮깁니다.
    ///
    /// LDtk 레벨들은 월드 좌표에서 서로 겹치지 않게 놓여 있어서, 씬을 바꾸지 않고
    /// 한 씬 안에서 "현재 라운드의 레벨만 켜는" 방식으로 전환합니다.
    /// (GameManager가 DontDestroyOnLoad이고 다른 매니저를 FindAnyObjectByType으로 찾기 때문에,
    ///  씬을 바꾸면 참조가 끊어지기 쉽습니다)
    /// </summary>
    [DefaultExecutionOrder(-90)]
    public class MapManager : MonoBehaviour
    {
        // 비워두면 씬 안의 모든 LDtk 레벨을 찾습니다.
        [SerializeField] private GameObject mapRoot;
        [SerializeField] private SimpleFollowCamera followCamera;

        // LDtk Entity의 피벗이 발밑(아래 가운데)이라, 그대로 스폰하면 캐릭터가
        // 바닥에 반쯤 묻힐 수 있습니다. 스폰 위치를 이만큼 위로 올립니다.
        [SerializeField] private float spawnHeightOffset = 0.5f;

        private readonly List<RoundMap> maps = new List<RoundMap>();

        public event Action<RoundMap> MapChanged;

        public RoundMap CurrentMap { get; private set; }
        public int MapCount => maps.Count;
        public float SpawnHeightOffset => spawnHeightOffset;

        private void Awake()
        {
            CollectMaps();
        }

        private void CollectMaps()
        {
            maps.Clear();

            LDtkComponentLevel[] levels = mapRoot != null
                ? mapRoot.GetComponentsInChildren<LDtkComponentLevel>(true)
                : FindObjectsByType<LDtkComponentLevel>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            // RoundIndex 필드가 없는 레벨을 위해 월드 배치 순서(왼쪽→오른쪽)로 기본 번호를 매깁니다.
            Array.Sort(levels, (a, b) => a.transform.position.x.CompareTo(b.transform.position.x));

            for (int i = 0; i < levels.Length; ++i)
            {
                maps.Add(new RoundMap(levels[i], i + 1));
            }

            maps.Sort((a, b) => a.RoundIndex.CompareTo(b.RoundIndex));

            if (maps.Count == 0)
            {
                Debug.LogWarning("MapManager ::: 씬에서 LDtk 레벨을 찾지 못했습니다. 맵 전환 없이 진행합니다.");
            }
        }

        public RoundMap GetMap(int roundIndex)
        {
            for (int i = 0; i < maps.Count; ++i)
            {
                if (maps[i].RoundIndex == roundIndex) return maps[i];
            }

            return null;
        }

        /// <summary>
        /// 해당 라운드의 맵만 켜고 플레이어를 시작 지점으로, 카메라 범위를 맵으로 맞춥니다.
        /// 맵이 없으면 false를 돌려주고 아무것도 바꾸지 않습니다.
        /// </summary>
        public bool LoadRound(int roundIndex)
        {
            RoundMap next = GetMap(roundIndex);
            if (next == null)
            {
                if (maps.Count > 0)
                {
                    Debug.LogWarning($"MapManager ::: {roundIndex}라운드에 해당하는 맵이 없습니다.");
                }
                return false;
            }

            for (int i = 0; i < maps.Count; ++i)
            {
                maps[i].Root.SetActive(maps[i] == next);
            }

            CurrentMap = next;

            MovePlayerToStart(next);
            ApplyCameraBounds(next);

            MapChanged?.Invoke(next);
            return true;
        }

        public Vector3 ToSpawnPosition(Transform marker)
        {
            return marker.position + Vector3.up * spawnHeightOffset;
        }

        private void MovePlayerToStart(RoundMap map)
        {
            if (map.PlayerStart == null) return;

            Player player = Player.LocalPlayer;
            if (player == null) return;

            Vector3 target = ToSpawnPosition(map.PlayerStart);

            // 실제로 움직이는 건 Player가 아니라 CharacterController2D(Rigidbody2D)가 붙은 루트입니다.
            CharacterController2D controller = player.GetComponentInParent<CharacterController2D>();
            if (controller != null)
            {
                controller.Teleport(target);
                return;
            }

            player.transform.position = target;
        }

        private void ApplyCameraBounds(RoundMap map)
        {
            if (followCamera == null) followCamera = FindAnyObjectByType<SimpleFollowCamera>();
            if (followCamera == null) return;

            followCamera.SetBounds(map.Bounds);
            followCamera.SnapToTarget();
        }
    }
}
