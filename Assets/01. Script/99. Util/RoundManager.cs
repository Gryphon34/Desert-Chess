using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Study_ActionPlatformer
{
    public class RoundManager : MonoBehaviour
    {
        [SerializeField] private int monstersPerRound = 20;
        [SerializeField] private int bossRound = 6;
        [SerializeField] private float spawnInterval = 0.4f;
        [SerializeField] private Transform[] spawnPoints;
        [SerializeField] private Enemy[] enemyPrefabs;
        [SerializeField] private Boss bossPrefab;

        // 현재 라운드 맵의 EnemySpawn/BossSpawn을 씁니다. 맵이 없거나 스폰 지점이 비어 있으면
        // 위의 spawnPoints로 대신합니다.
        [SerializeField] private MapManager mapManager;

        private GameManager gameManager;
        private readonly List<Enemy> aliveEnemies = new List<Enemy>();

        private Boss currentBoss;
        private int currentRound = 1;
        private int spawnedThisRound = 0;
        private bool isSpawning = false;
        private bool roundCompleted = false;
        private int defeatedThisRound = 0;

        // HUD가 라운드 번호를 매 프레임 비교하지 않도록 라운드 진행을 이벤트로 알립니다.
        public event Action<int> RoundStarted;
        public event Action<int> RoundCleared;
        // (처치 수, 라운드 목표 수)
        public event Action<int, int> KillProgressChanged;

        // 기획서 6-2 : "보스 스킬 = 몬스터의 모든 스킬들 보유".
        // 1~5라운드에서 실제로 스폰된 몬스터들의 드랍 무기 종류를 모아뒀다가,
        // 보스가 스폰될 때 그 목록을 넘겨줍니다. 중복 없이 "이 판에서 실제로
        // 만난 무기 종류"만 담습니다.
        private readonly List<WeaponId> encounteredWeaponIds = new List<WeaponId>();

        public int CurrentRound => currentRound;
        public int MonstersPerRound => monstersPerRound;
        public bool IsBossRound => currentRound >= bossRound;
        public Boss CurrentBoss => currentBoss;
        public int DefeatedThisRound => defeatedThisRound;

        public void Initialize(GameManager manager)
        {
            gameManager = manager;
            if (mapManager == null) mapManager = FindAnyObjectByType<MapManager>();
        }

        public void BeginRound(int roundIndex)
        {
            currentRound = roundIndex;
            spawnedThisRound = 0;
            defeatedThisRound = 0;
            roundCompleted = false;

            aliveEnemies.Clear();
            currentBoss = null;

            // 1라운드(=새 게임 시작)일 때만 초기화합니다. 2~6라운드로 이어질 때는
            // 이전 라운드들의 기록을 계속 누적해야 보스가 "이 판 전체에서 만난 몬스터"를
            // 반영합니다.
            if (roundIndex == 1)
            {
                encounteredWeaponIds.Clear();
            }

            RoundStarted?.Invoke(currentRound);
            KillProgressChanged?.Invoke(defeatedThisRound, monstersPerRound);

            StartCoroutine(SpawnRoundCoroutine());
        }

        /// <summary>
        /// 게임 오버 등으로 진행을 강제 중단할 때 호출합니다.
        ///
        /// 지금까지는 GameManager.TriggerGameOver()가 상태값만 바꾸고 RoundManager에게는
        /// 아무 것도 알리지 않았습니다. SpawnRoundCoroutine은 자기가 멈춰야 할 이유를
        /// 모르니 spawnInterval마다 계속 몬스터를 스폰했고, 그래서 "체력이 0이 돼도
        /// 라운드가 멈추지 않는" 문제가 생겼습니다.
        /// roundCompleted를 true로 만들어 EvaluateRoundState의 후속 판정도 막습니다.
        /// </summary>
        public void StopAllProgress()
        {
            StopAllCoroutines();
            isSpawning = false;
            roundCompleted = true;
        }

        public void NotifyEnemyDefeated(EnemyController controller)
        {
            Enemy enemy = controller.GetComponentInChildren<Enemy>();
            // 목록에 있던 몬스터만 셉니다(보스가 소환한 졸개 등은 라운드 목표 수에 넣지 않습니다).
            if (enemy != null && aliveEnemies.Remove(enemy))
            {
                defeatedThisRound += 1;
                KillProgressChanged?.Invoke(defeatedThisRound, monstersPerRound);
            }

            EvaluateRoundState();
        }

        public void NotifyBossDefeated()
        {
            if (roundCompleted) return;
            roundCompleted = true;
            currentBoss = null;

            if (gameManager != null)
            {
                gameManager.NotifyBossKilled();
            }
        }

        private IEnumerator SpawnRoundCoroutine()
        {
            isSpawning = true;

            if (currentRound >= bossRound)
            {
                if (bossPrefab == null)
                {
                    Debug.LogError($"RoundManager ::: {currentRound}라운드는 보스 라운드인데 " +
                        $"Boss Prefab이 비어 있습니다. 인스펙터에서 연결해주세요.");
                    isSpawning = false;
                    NotifyBossDefeated();
                    yield break;
                }

                SpawnBoss();
                isSpawning = false;
                yield break;
            }

            while (spawnedThisRound < monstersPerRound)
            {
                SpawnEnemy();
                spawnedThisRound += 1;
                yield return new WaitForSeconds(spawnInterval);
            }

            isSpawning = false;
            EvaluateRoundState();
        }

        private void SpawnEnemy()
        {
            if (enemyPrefabs == null || enemyPrefabs.Length == 0)
                return;

            if (TryGetEnemySpawnPosition(out Vector3 spawnPosition) == false)
                return;

            Enemy prefab = enemyPrefabs[UnityEngine.Random.Range(0, enemyPrefabs.Length)];
            Enemy enemy = Instantiate(prefab, spawnPosition, Quaternion.identity);

            EnemyController controller = enemy.GetComponent<EnemyController>();
            if (controller != null)
            {
                controller.SetRoundManager(this);
            }

            // Instantiate가 끝난 시점엔 Enemy.Awake()가 이미 실행돼 있어서
            // DroppedWeaponInfo가 확정된 상태입니다(흡수 여부와는 무관하게 기록합니다).
            if (enemy.DroppedWeaponInfo.IsEmpty == false &&
                encounteredWeaponIds.Contains(enemy.DroppedWeaponInfo.Id) == false)
            {
                encounteredWeaponIds.Add(enemy.DroppedWeaponInfo.Id);
            }

            aliveEnemies.Add(enemy);
        }

        private void SpawnBoss()
        {
            if (bossPrefab == null)
                return;

            if (TryGetBossSpawnPosition(out Vector3 spawnPosition) == false)
                return;

            Boss boss = Instantiate(bossPrefab, spawnPosition, Quaternion.identity);

            // Boss.Start()가 실행되기 전에(같은 프레임 안에서) 넘겨줘야 합니다.
            // 그래야 보스가 "완전 무작위" 대신 이 판에서 실제로 만난 몬스터들의
            // 무기 종류로 스킬을 채웁니다.
            boss.SeedSkillsFromMonsters(encounteredWeaponIds);

            BossController controller = boss.GetComponent<BossController>();
            if (controller != null)
            {
                controller.SetRoundManager(this);
            }
            else
            {
                Debug.LogError($"{boss.name} : BossController가 없습니다. 보스 처치를 감지할 수 없습니다.");
            }

            currentBoss = boss;
        }

        private void EvaluateRoundState()
        {
            if (roundCompleted)
                return;

            if (currentRound >= bossRound)
                return;

            if (isSpawning)
                return;

            aliveEnemies.RemoveAll(enemy => enemy == null);

            if (aliveEnemies.Count == 0 && spawnedThisRound >= monstersPerRound)
            {
                roundCompleted = true;
                RoundCleared?.Invoke(currentRound);

                if (gameManager != null)
                {
                    gameManager.NotifyRoundCleared(currentRound);
                }
            }
        }

        private RoundMap CurrentMap => mapManager != null ? mapManager.CurrentMap : null;

        private bool TryGetEnemySpawnPosition(out Vector3 position)
        {
            RoundMap map = CurrentMap;
            if (map != null && map.EnemySpawns.Length > 0)
            {
                Transform marker = map.EnemySpawns[UnityEngine.Random.Range(0, map.EnemySpawns.Length)];
                position = mapManager.ToSpawnPosition(marker);
                return true;
            }

            return TryGetFallbackSpawnPosition(out position);
        }

        private bool TryGetBossSpawnPosition(out Vector3 position)
        {
            RoundMap map = CurrentMap;
            if (map != null && map.BossSpawn != null)
            {
                position = mapManager.ToSpawnPosition(map.BossSpawn);
                return true;
            }

            return TryGetEnemySpawnPosition(out position);
        }

        // 맵이 연결되지 않은 씬(예전 테스트 씬 등)에서는 인스펙터의 spawnPoints를 그대로 씁니다.
        private bool TryGetFallbackSpawnPosition(out Vector3 position)
        {
            if (spawnPoints == null || spawnPoints.Length == 0)
            {
                position = Vector3.zero;
                return false;
            }

            position = spawnPoints[UnityEngine.Random.Range(0, spawnPoints.Length)].position;
            return true;
        }
    }
}
