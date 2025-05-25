using UnityEngine;
using System.Collections.Generic;

namespace JY
{
    /// <summary>
    /// 플레이어의 명성도를 관리하는 시스템
    /// AI가 방 사용을 완료하고 결제할 때마다 명성도가 증가합니다.
    /// </summary>
    public class ReputationSystem : MonoBehaviour
    {
        [Header("명성도 설정")]
        [SerializeField] private int currentReputation = 0;
        [SerializeField] private int minReputationGain = 25;
        [SerializeField] private int maxReputationGain = 28;
        
        [Header("디버그")]
        [SerializeField] private bool showDebugLogs = true;
        [SerializeField] private List<string> reputationLogs = new List<string>();
        
        public static ReputationSystem Instance { get; private set; }
        
        // 명성도 변경 이벤트
        public System.Action<int> OnReputationChanged;
        
        public int CurrentReputation => currentReputation;
        public int MinReputationGain => minReputationGain;
        public int MaxReputationGain => maxReputationGain;
        
        void Awake()
        {
            // 싱글톤 패턴
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }
        
        /// <summary>
        /// AI가 방 사용을 완료했을 때 명성도를 증가시킵니다.
        /// </summary>
        /// <param name="aiName">AI 이름</param>
        /// <param name="roomID">사용한 방 ID</param>
        public void AddReputation(string aiName, string roomID)
        {
            // 최소값과 최대값 사이에서 랜덤하게 명성도 증가량 결정
            int reputationGain = Random.Range(minReputationGain, maxReputationGain + 1);
            
            currentReputation += reputationGain;
            
            // 로그 기록
            string logMessage = $"{aiName}이(가) 방 {roomID} 사용 완료 - 명성도 +{reputationGain} (총 명성도: {currentReputation})";
            reputationLogs.Add(logMessage);
            
            if (showDebugLogs)
            {
                Debug.Log($"[명성도 시스템] {logMessage}");
            }
            
            // 이벤트 발생
            OnReputationChanged?.Invoke(currentReputation);
        }
        
        /// <summary>
        /// 명성도 범위를 설정합니다.
        /// </summary>
        /// <param name="min">최소 증가량</param>
        /// <param name="max">최대 증가량</param>
        public void SetReputationRange(int min, int max)
        {
            minReputationGain = Mathf.Max(0, min);
            maxReputationGain = Mathf.Max(minReputationGain, max);
            
            if (showDebugLogs)
            {
                Debug.Log($"[명성도 시스템] 명성도 범위 설정: {minReputationGain} ~ {maxReputationGain}");
            }
        }
        
        /// <summary>
        /// 현재 명성도를 직접 설정합니다. (치트나 세이브/로드용)
        /// </summary>
        /// <param name="reputation">설정할 명성도</param>
        public void SetReputation(int reputation)
        {
            int oldReputation = currentReputation;
            currentReputation = Mathf.Max(0, reputation);
            
            if (showDebugLogs)
            {
                Debug.Log($"[명성도 시스템] 명성도 직접 설정: {oldReputation} -> {currentReputation}");
            }
            
            OnReputationChanged?.Invoke(currentReputation);
        }
        
        /// <summary>
        /// 명성도 로그를 초기화합니다.
        /// </summary>
        public void ClearLogs()
        {
            reputationLogs.Clear();
        }
        
        /// <summary>
        /// 현재까지의 명성도 로그를 반환합니다.
        /// </summary>
        /// <returns>명성도 로그 리스트</returns>
        public List<string> GetReputationLogs()
        {
            return new List<string>(reputationLogs);
        }
    }
}