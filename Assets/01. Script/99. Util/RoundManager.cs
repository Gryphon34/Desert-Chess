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
        private int currentRound = 1;
        private int spawnedThisRound = 0;
        private bool isSpawning = false;
        private bool roundCompleted = false;

        public int CurrentRound => currentRound;
        public int MonstersPerRound => monstersPerRound;

        public void Initialize(GameManager manager)
        {
            gameManager = manager;
        }

        public void BeginRound(int roundIndex)
        {
            currentRound = roundIndex;
            spawnedThisRound = 0;
            roundCompleted = false;
            StartCoroutine(SpawnRoundCoroutine());
        }

        public void NotifyEnemyDefeated(EnemyController controller)
        {
            Enemy enemy = controller.GetComponent<Enemy>();
            if (enemy == null)
                return;

            if (aliveEnemies.Contains(enemy))
            {
                aliveEnemies.Remove(enemy);
            }

            EvaluateRoundState();
        }

        public void NotifyBossDefeated()
        {
            if (gameManager != null)
            {
                gameManager.NotifyBossKilled();
            }
        }

        private IEnumerator SpawnRoundCoroutine()
        {
            isSpawning = true;

            if (currentRound >= bossRound && bossPrefab != null)
            {
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
            EnemyController controller = boss.GetComponent<EnemyController>();
            if (controller != null)
            {
                controller.SetRoundManager(this);
            }

            aliveEnemies.Add(boss);
        }

        private void EvaluateRoundState()
        {
            if (roundCompleted)
                return;

            if (currentRound >= bossRound)
            {
                if (aliveEnemies.Count == 0)
                {
                    roundCompleted = true;
                    if (gameManager != null)
                    {
                        gameManager.NotifyBossKilled();
                    }
                }
                return;
            }

            if (isSpawning)
                return;

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
