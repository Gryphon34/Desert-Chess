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
                    Instantiate(minionPrefab, spawnPoint.position, spawnPoint.rotation);
                }

                yield return new WaitForSeconds(spawnInterval);
            }

            yield return new WaitForSeconds(endDelay);
            BossController.ChangeState(typeof(IdleState));
        }
    }
}
