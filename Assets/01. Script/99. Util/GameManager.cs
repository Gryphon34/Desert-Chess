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
            rewardManager = FindAnyObjectByType<RewardManager>();

            if (roundManager != null)
            {
                roundManager.Initialize(this);
            }

            BeginGame();
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
            currentState = GameFlowState.Playing;
            if (roundManager != null)
            {
                roundManager.BeginRound(1);
            }
        }

        public void NotifyRoundCleared(int roundIndex)
        {
            if (rewardManager != null)
            {
                rewardManager.GrantRoundReward(roundIndex);
            }

            if (roundIndex >= roundCountToBoss)
            {
                TriggerClear();
                return;
            }

            if (roundManager != null)
            {
                roundManager.BeginRound(roundIndex + 1);
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
