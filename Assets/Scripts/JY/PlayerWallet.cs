using UnityEngine;

namespace JY
{
    /// <summary>
    /// 플레이어 지갑 시스템
    /// 플레이어의 소지금 관리
    /// </summary>
    public class PlayerWallet : MonoBehaviour
    {
        public static PlayerWallet Instance { get; private set; }
        
        [Header("소지금 정보")]
        [SerializeField] private int money = 0;
        
        [Header("디버그 설정")]
        [Tooltip("디버그 로그 표시 여부")]
        [SerializeField] private bool showDebugLogs = false;
        
        [Tooltip("중요한 이벤트만 로그 표시")]
        [SerializeField] private bool showImportantLogsOnly = true;

        public int Money => money;

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                DebugLog("플레이어 지갑 시스템 초기화 완료", true);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// 소지금 추가
        /// </summary>
        public void AddMoney(int amount)
        {
            money += amount;
            DebugLog($"소지금 증가: {amount}원, 현재 소지금: {money}원", true);
        }

        /// <summary>
        /// 소지금 사용
        /// </summary>
        public void SpendMoney(int amount)
        {
            if (money >= amount)
            {
                money -= amount;
                DebugLog($"소지금 감소: {amount}원, 현재 소지금: {money}원", true);
            }
            else
            {
                DebugLog($"소지금 부족: 필요 {amount}원, 현재 {money}원", true);
            }
        }
        
        #region 디버그 메서드
        
        /// <summary>
        /// 디버그 로그 출력
        /// </summary>
        private void DebugLog(string message, bool isImportant = false)
        {
            if (!showDebugLogs) return;
            
            if (showImportantLogsOnly && !isImportant) return;
            
            Debug.Log($"[PlayerWallet] {message}");
        }
        
        #endregion
    }
}