using System.Collections.Generic;
using LDtkUnity;
using UnityEngine;

namespace Study_ActionPlatformer
{
    /// <summary>
    /// LDtk 레벨 하나를 "라운드 맵"으로 읽어낸 결과입니다.
    ///
    /// LDtk에서 정한 값(레벨 필드 RoundIndex/DisplayName/IsBossRound, Entities 레이어의
    /// PlayerStart/EnemySpawn/BossSpawn)을 임포트된 컴포넌트에서 한 번만 읽어 두고,
    /// MapManager·RoundManager·HUD는 LDtk 타입을 몰라도 이 값만 쓰면 되게 합니다.
    /// </summary>
    public class RoundMap
    {
        public const string FIELD_ROUND_INDEX = "RoundIndex";
        public const string FIELD_DISPLAY_NAME = "DisplayName";
        public const string FIELD_IS_BOSS_ROUND = "IsBossRound";

        public const string ENTITY_PLAYER_START = "PlayerStart";
        public const string ENTITY_ENEMY_SPAWN = "EnemySpawn";
        public const string ENTITY_BOSS_SPAWN = "BossSpawn";

        public LDtkComponentLevel Level { get; }
        public GameObject Root => Level.gameObject;

        public int RoundIndex { get; }
        public string DisplayName { get; }
        public bool IsBossRound { get; }

        public Transform PlayerStart { get; }
        public Transform BossSpawn { get; }
        public Transform[] EnemySpawns { get; }

        // LDtk 레벨의 테두리(월드 좌표). 카메라가 이 밖을 비추지 않도록 제한할 때 씁니다.
        // Level.BorderBounds는 임포트 시점 위치로 저장된 값이라, 맵 프리팹을 씬에서 옮기면
        // 어긋납니다. 레벨 원점(왼쪽 아래)과 크기로 매번 다시 계산합니다.
        public Bounds Bounds
        {
            get
            {
                Vector3 size = Level.Size;
                return new Bounds(Level.transform.position + size * 0.5f, size);
            }
        }

        /// <param name="fallbackRoundIndex">
        /// 레벨에 RoundIndex 필드가 없을 때 쓸 번호입니다(월드 배치 순서 기준).
        /// </param>
        public RoundMap(LDtkComponentLevel level, int fallbackRoundIndex)
        {
            Level = level;

            LDtkFields fields = level.FieldInstances;

            RoundIndex = (fields != null && fields.TryGetInt(FIELD_ROUND_INDEX, out int round))
                ? round
                : fallbackRoundIndex;

            DisplayName = (fields != null && fields.TryGetString(FIELD_DISPLAY_NAME, out string displayName)
                           && string.IsNullOrEmpty(displayName) == false)
                ? displayName
                : level.Identifier;

            IsBossRound = fields != null && fields.TryGetBool(FIELD_IS_BOSS_ROUND, out bool isBoss) && isBoss;

            List<Transform> enemySpawns = new List<Transform>();
            LDtkComponentEntity[] entities = level.GetComponentsInChildren<LDtkComponentEntity>(true);

            for (int i = 0; i < entities.Length; ++i)
            {
                LDtkComponentEntity entity = entities[i];
                switch (entity.Identifier)
                {
                    case ENTITY_PLAYER_START:
                        if (PlayerStart == null) PlayerStart = entity.transform;
                        break;
                    case ENTITY_BOSS_SPAWN:
                        if (BossSpawn == null) BossSpawn = entity.transform;
                        break;
                    case ENTITY_ENEMY_SPAWN:
                        enemySpawns.Add(entity.transform);
                        break;
                }
            }

            EnemySpawns = enemySpawns.ToArray();

            if (PlayerStart == null)
            {
                Debug.LogWarning($"RoundMap ::: {level.Identifier}에 {ENTITY_PLAYER_START} Entity가 없습니다. " +
                    "플레이어 위치를 옮기지 않습니다.");
            }

            if (EnemySpawns.Length == 0)
            {
                Debug.LogWarning($"RoundMap ::: {level.Identifier}에 {ENTITY_ENEMY_SPAWN} Entity가 없습니다. " +
                    "RoundManager의 기본 스폰 지점을 사용합니다.");
            }
        }
    }
}
