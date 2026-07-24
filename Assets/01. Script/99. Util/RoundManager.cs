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

        // 보스 라운드의 종료 조건은 "남은 적이 없다"가 아니라 "이 보스가 죽었다"입니다.
        // 보스가 소환한 잡몹(SpawnMinionsState)은 목록에 등록되지 않기 때문에,
        // 개수만 세면 보스가 멀쩡히 살아있는데 클리어가 나버립니다.
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

            // 이전 라운드의 잔재를 반드시 비웁니다.
            // 이걸 안 하면, 처치 통보 없이 사라진 몬스터(파괴됐지만 NotifyEnemyDefeated가
            // 호출되지 않은 경우)가 목록에 계속 남아서 Count가 0이 되지 않고,
            // 결과적으로 라운드가 영원히 끝나지 않습니다.
            aliveEnemies.Clear();
            currentBoss = null;

            StartCoroutine(SpawnRoundCoroutine());
        }

        public void NotifyEnemyDefeated(EnemyController controller)
        {
            // Enemy 컴포넌트를 못 찾아도 그냥 return하면 안 됩니다.
            // 그 몬스터는 이미 죽어서 사라지는데 목록에서 빠지지 않으면
            // 라운드가 영원히 끝나지 않습니다. 판정은 반드시 다시 돌립니다.
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
                // 보스 라운드인데 프리팹이 비어 있으면, 예전 코드는 그냥 아래로 흘러가
                // 잡몹을 스폰했습니다. 그런데 EvaluateRoundState는 보스 라운드를
                // 개수로 판정하지 않으므로 라운드가 영원히 끝나지 않습니다.
                // 조용히 멈추는 대신 원인을 알리고 클리어로 처리합니다.
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

            // 보스에는 EnemyController가 아니라 BossController가 붙어 있습니다.
            // 예전 코드는 GetComponent<EnemyController>()를 찾다가 항상 null을 받아
            // roundManager 연결에 실패했고, 그래서 보스를 잡아도 NotifyBossDefeated가
            // 호출되지 않아 게임이 클리어되지 않았습니다.
            BossController controller = boss.GetComponent<BossController>();
            if (controller != null)
            {
                controller.SetRoundManager(this);
            }
            else
            {
                Debug.LogError($"{boss.name} : BossController가 없습니다. 보스 처치를 감지할 수 없습니다.");
            }

            // 보스는 개수 목록이 아니라 전용 필드로 추적합니다(EvaluateRoundState 참고).
            currentBoss = boss;
        }

        private void EvaluateRoundState()
        {
            if (roundCompleted)
                return;

            // 보스 라운드는 개수로 판정하지 않습니다. 보스의 죽음(NotifyBossDefeated)
            // 만이 유일한 클리어 조건입니다.
            if (currentRound >= bossRound)
                return;

            if (isSpawning)
                return;

            // 처치 통보 없이 파괴된 몬스터가 남아 있으면 Count가 0이 되지 않습니다.
            // 유니티에서 파괴된 오브젝트는 == null 비교가 true가 되므로 이걸로 걸러냅니다.
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
