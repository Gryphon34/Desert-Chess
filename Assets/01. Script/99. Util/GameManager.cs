using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

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

        private int pendingNextRound = -1;

        // 게임 오버 배너를 5초 보여준 뒤 플레이를 종료하기 위한 타이머입니다.
        // Time.timeScale을 0으로 만들 것이므로, 이 카운트다운은 반드시 unscaled 시간을
        // 써야 진행됩니다(Time.deltaTime을 쓰면 timeScale이 0인 동안 영원히 0으로 멈춥니다).
        private const float GAME_OVER_DISPLAY_DURATION = 5f;
        private float gameOverTimer = 0f;

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
            if (rewardManager != null)
            {
                rewardManager.RewardResolved -= OnRewardResolved;
            }
        }

        private void Update()
        {
            if (currentState == GameFlowState.GameOver)
            {
                UpdateGameOverCountdown();
                return;
            }

            if (currentState != GameFlowState.Playing)
                return;

            if (player == null)
                player = Player.LocalPlayer;

            if (player != null && player.BaseStat != null && player.BaseStat.Hp <= 0)
            {
                TriggerGameOver();
            }
        }

        /// <summary>
        /// 게임 오버 배너가 5초 유지되도록 시간을 셉니다. Time.timeScale이 0이라
        /// 일반 Time.deltaTime은 쓸 수 없고, Time.unscaledDeltaTime을 씁니다.
        /// </summary>
        private void UpdateGameOverCountdown()
        {
            if (gameOverTimer <= 0f) return;

            gameOverTimer -= Time.unscaledDeltaTime;
            if (gameOverTimer > 0f) return;

#if UNITY_EDITOR
            // 에디터에서 테스트할 때, 5초 뒤 자동으로 Play 모드를 종료합니다.
            EditorApplication.isPlaying = false;
#else
            // 빌드에서는 Play 모드라는 개념이 없으므로, 여기서는 시간 정지 상태로
            // 남겨둡니다. 실제 배포판에서는 타이틀 화면 복귀 등으로 바꿔주세요.
#endif
        }

        public void BeginGame()
        {
            Time.timeScale = 1f;

            currentState = GameFlowState.Playing;
            if (roundManager != null)
            {
                roundManager.BeginRound(1);
            }
        }

        public void NotifyRoundCleared(int roundIndex)
        {
            if (roundIndex >= roundCountToBoss)
            {
                TriggerClear();
                return;
            }

            pendingNextRound = roundIndex + 1;

            if (rewardManager != null)
            {
                rewardManager.GrantRoundReward(roundIndex);
                return;
            }

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

            // RoundManager에게 진행 중단을 알리지 않으면, 스폰 코루틴이 자기가 멈춰야
            // 할 이유를 몰라서 spawnInterval마다 계속 몬스터를 스폰합니다.
            if (roundManager != null)
            {
                roundManager.StopAllProgress();
            }

            // 몬스터 AI, 플레이어 이동 등 거의 모든 동작이 Time.deltaTime 기반이라
            // timeScale을 0으로 만드는 것만으로 게임 전체가 얼어붙습니다.
            // (게임 오버 배너의 5초 카운트다운은 unscaled 시간으로 별도 진행됩니다)
            Time.timeScale = 0f;
            gameOverTimer = GAME_OVER_DISPLAY_DURATION;
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
