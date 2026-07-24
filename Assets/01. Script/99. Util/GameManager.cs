using UnityEngine;

namespace Study_ActionPlatformer
{
    public enum GameFlowState
    {
        Ready,
        Playing,
        GameOver,
        Clear,
    }

    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [SerializeField] private int roundCountToBoss = 6;
        [SerializeField] private RewardManager rewardManager;

        private RoundManager roundManager;
        private Player player;
        private GameFlowState currentState = GameFlowState.Ready;

        // 보상 선택이 끝나면 진입할 라운드. -1이면 대기 중인 라운드가 없다는 뜻입니다.
        private int pendingNextRound = -1;

        public GameFlowState CurrentState => currentState;
        public int RoundCountToBoss => roundCountToBoss;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            player = Player.LocalPlayer;
            roundManager = FindAnyObjectByType<RoundManager>();

            // 인스펙터에서 지정해 둔 참조가 있으면 그것을 우선합니다.
            // (예전에는 무조건 FindAnyObjectByType로 덮어써서 지정이 무시됐습니다)
            if (rewardManager == null) rewardManager = FindAnyObjectByType<RewardManager>();

            if (roundManager != null)
            {
                roundManager.Initialize(this);
            }

            if (rewardManager != null)
            {
                rewardManager.RewardResolved += OnRewardResolved;
            }

            BeginGame();
        }

        private void OnDestroy()
        {
            // 구독한 이벤트는 반드시 해제합니다.
            // GameManager는 DontDestroyOnLoad라 씬을 다시 로드해도 살아남는데,
            // 해제를 빼먹으면 파괴된 옛 인스턴스가 계속 호출되어 추적하기 힘든
            // 버그(라운드가 두 번 시작되는 등)가 생깁니다.
            if (rewardManager != null)
            {
                rewardManager.RewardResolved -= OnRewardResolved;
            }
        }

        private void Update()
        {
            if (currentState != GameFlowState.Playing)
                return;

            if (player == null)
                player = Player.LocalPlayer;

            if (player != null && player.BaseStat != null && player.BaseStat.Hp <= 0)
            {
                TriggerGameOver();
            }
        }

        public void BeginGame()
        {
            // 게임을 시작하는데 시간이 멈춰 있으면 안 됩니다.
            //
            // 에디터에서 Time.timeScale은 플레이 종료 후에도 값이 유지됩니다.
            // 팝업(CombatHud)이 떠 있는 상태로 플레이를 중지하면 0인 채로 남아,
            // 다음 실행에서 FixedUpdate가 아예 돌지 않아 캐릭터가 움직이지 않습니다.
            // 에러 로그도 없어서 원인을 찾기가 매우 어렵습니다.
            Time.timeScale = 1f;

            currentState = GameFlowState.Playing;
            if (roundManager != null)
            {
                roundManager.BeginRound(1);
            }
        }

        /// <summary>
        /// 일반 라운드를 클리어했을 때 호출됩니다.
        ///
        /// 기획서 3번의 진행 순서는 "라운드 클리어 → 보상 선택 → 다음 라운드"입니다.
        /// 예전 코드는 보상 팝업을 띄우자마자 곧바로 다음 라운드를 시작해서,
        /// 플레이어가 보상을 고르는 동안 이미 몬스터가 스폰되고 있었습니다.
        /// 이제는 RewardResolved 이벤트를 받은 뒤에 다음 라운드를 시작합니다.
        /// </summary>
        public void NotifyRoundCleared(int roundIndex)
        {
            // 보스 라운드의 클리어는 NotifyBossKilled로 들어오므로 여기 오지 않습니다.
            // 혹시 들어온다면 라운드 설정이 어긋난 것이니 그대로 클리어 처리합니다.
            if (roundIndex >= roundCountToBoss)
            {
                TriggerClear();
                return;
            }

            pendingNextRound = roundIndex + 1;

            if (rewardManager != null)
            {
                // 실제 다음 라운드 진입은 OnRewardResolved()에서 일어납니다.
                rewardManager.GrantRoundReward(roundIndex);
                return;
            }

            // 보상 시스템이 없는 씬(테스트용 등)에서는 곧바로 진행합니다.
            BeginPendingRound();
        }

        private void OnRewardResolved()
        {
            BeginPendingRound();
        }

        private void BeginPendingRound()
        {
            if (pendingNextRound < 0) return;

            int next = pendingNextRound;
            pendingNextRound = -1;

            if (roundManager != null)
            {
                roundManager.BeginRound(next);
            }
        }

        public void NotifyBossKilled()
        {
            TriggerClear();
        }

        public void TriggerGameOver()
        {
            if (currentState == GameFlowState.GameOver)
                return;

            currentState = GameFlowState.GameOver;
            Debug.Log("Game Over");
        }

        public void TriggerClear()
        {
            if (currentState == GameFlowState.Clear)
                return;

            currentState = GameFlowState.Clear;
            if (rewardManager != null)
            {
                rewardManager.GrantBossClearReward();
            }

            Debug.Log("Clear");
        }
    }
}
