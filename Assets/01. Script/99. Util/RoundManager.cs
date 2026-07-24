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

        private GameManager gameManager;
        private readonly List<Enemy> aliveEnemies = new List<Enemy>();

        private Boss currentBoss;
        private int currentRound = 1;
        private int spawnedThisRound = 0;
        private bool isSpawning = false;
        private bool roundCompleted = false;

        public int CurrentRound => currentRound;
        public int MonstersPerRound => monstersPerRound;
        public bool IsBossRound => currentRound >= bossRound;
        public Boss CurrentBoss => currentBoss;

        public void Initialize(GameManager manager)
        {
            gameManager = manager;
        }

        public void BeginRound(int roundIndex)
        {
            currentRound = roundIndex;
            spawnedThisRound = 0;
            roundCompleted = false;

            aliveEnemies.Clear();
            currentBoss = null;

            StartCoroutine(SpawnRoundCoroutine());
        }

        public void NotifyEnemyDefeated(EnemyController controller)
        {
            Enemy enemy = controller.GetComponentInChildren<Enemy>();
            if (enemy != null)
            {
                aliveEnemies.Remove(enemy);
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
            if (spawnPoints == null || spawnPoints.Length == 0)
                return;

            if (enemyPrefabs == null || enemyPrefabs.Length == 0)
                return;

            Transform spawnPoint = spawnPoints[Random.Range(0, spawnPoints.Length)];
            Enemy prefab = enemyPrefabs[Random.Range(0, enemyPrefabs.Length)];
            Enemy enemy = Instantiate(prefab, spawnPoint.position, Quaternion.identity);

            EnemyController controller = enemy.GetComponent<EnemyController>();
            if (controller != null)
            {
                controller.SetRoundManager(this);
            }

            aliveEnemies.Add(enemy);
        }

        private void SpawnBoss()
        {
            if (spawnPoints == null || spawnPoints.Length == 0 || bossPrefab == null)
                return;

            Transform spawnPoint = spawnPoints[Random.Range(0, spawnPoints.Length)];
            Boss boss = Instantiate(bossPrefab, spawnPoint.position, Quaternion.identity);

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
                if (gameManager != null)
                {
                    gameManager.NotifyRoundCleared(currentRound);
                }
            }
        }
    }
}
