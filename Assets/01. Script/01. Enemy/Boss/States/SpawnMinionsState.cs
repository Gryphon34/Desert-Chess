using System.Collections;
using UnityEngine;

namespace Study_ActionPlatformer
{
    public class SpawnMinionsState : BossPatternState
    {
        [SerializeField] private GameObject[] minionPrefabs;
        [SerializeField] private Transform[] spawnPoints;
        [SerializeField] private float spawnInterval = 0.3f;
        [SerializeField] private float endDelay = 1.5f;

        // 기획서 9번 : 이 잡몹들을 처치하면 플레이어의 활성 무기/마법 슬롯이
        // 이만큼 충전됩니다(최대치는 Player.MAX_USES로 고정).
        [SerializeField] private int rechargeAmountPerMinion = 1;

        protected override void OnEnable()
        {
            StartCoroutine(Coroutine());
        }

        protected override void OnDisable()
        {
        }

        private IEnumerator Coroutine()
        {
            if (minionPrefabs == null || minionPrefabs.Length == 0 || spawnPoints == null || spawnPoints.Length == 0)
            {
                BossController.ChangeState(typeof(IdleState));
                yield break;
            }

            for (int i = 0; i < spawnPoints.Length; ++i)
            {
                if (minionPrefabs.Length <= i)
                    break;

                GameObject minionPrefab = minionPrefabs[i % minionPrefabs.Length];
                Transform spawnPoint = spawnPoints[i];

                if (minionPrefab != null && spawnPoint != null)
                {
                    GameObject minion = Instantiate(minionPrefab, spawnPoint.position, spawnPoint.rotation);

                    // 이 잡몹을 잡으면 충전되도록 표시합니다. EnemyController가 없는
                    // 프리팹(설정 실수)이 들어와도 조용히 넘어갑니다.
                    EnemyController controller = minion.GetComponent<EnemyController>();
                    if (controller != null)
                    {
                        controller.MarkAsChargeMinion(rechargeAmountPerMinion);
                    }
                }

                yield return new WaitForSeconds(spawnInterval);
            }

            yield return new WaitForSeconds(endDelay);
            BossController.ChangeState(typeof(IdleState));
        }
    }
}
