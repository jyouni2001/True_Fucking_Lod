using UnityEngine;
using System.Collections.Generic;
using TMPro;

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
        
        [Header("UI 설정")]
        [SerializeField] private TextMeshProUGUI reputationText; // Inspector에서 할당
        [SerializeField] private string textFormat = "Grade: {0} {1}"; // {0}: 명성도, {1}: 등급
        
        [Header("등급 설정")]
        [SerializeField] private int[] gradeThresholds = {0, 100, 300, 500, 1000, 2000, 3000};
        [SerializeField] private string[] gradeNames = {"Ground", "Tier1", "Tier2", "Tier3", "Tier4", "Tier5", "Tier6"};
        
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
        
        void Start()
        {
            // 시작할 때 UI 업데이트
            UpdateUI();
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
            
            // UI 업데이트
            UpdateUI();
            
            // 이벤트 발생
            OnReputationChanged?.Invoke(currentReputation);
        }
        
        /// <summary>
        /// AI가 방 사용을 완료했을 때 방 명성도를 기반으로 명성도를 증가시킵니다.
        /// </summary>
        /// <param name="aiName">AI 이름</param>
        /// <param name="roomID">사용한 방 ID</param>
        /// <param name="roomReputation">방의 총 명성도</param>
        public void AddReputation(string aiName, string roomID, int roomReputation)
        {
            // 방 명성도를 그대로 명성도 증가량으로 사용
            int reputationGain = Mathf.Max(1, roomReputation); // 최소 1 보장
            
            currentReputation += reputationGain;
            
            // 로그 기록
            string logMessage = $"{aiName}이(가) 방 {roomID} 사용 완료 - 방 명성도 기반 +{reputationGain} (총 명성도: {currentReputation})";
            reputationLogs.Add(logMessage);
            
            if (showDebugLogs)
            {
                Debug.Log($"[명성도 시스템] {logMessage}");
            }
            
            // UI 업데이트
            UpdateUI();
            
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
            
            // UI 업데이트
            UpdateUI();
            
            OnReputationChanged?.Invoke(currentReputation);
        }
        
        /// <summary>
        /// UI 텍스트를 업데이트합니다.
        /// </summary>
        private void UpdateUI()
        {
            if (reputationText != null)
            {
                string grade = GetCurrentGrade();
                reputationText.text = string.Format(textFormat, currentReputation, grade);
            }
        }
        
        /// <summary>
        /// 현재 명성도에 따른 등급을 반환합니다.
        /// </summary>
        /// <returns>현재 등급 이름</returns>
        public string GetCurrentGrade()
        {
            for (int i = gradeThresholds.Length - 1; i >= 0; i--)
            {
                if (currentReputation >= gradeThresholds[i])
                {
                    if (i < gradeNames.Length)
                    {
                        return gradeNames[i];
                    }
                    break;
                }
            }
            
            // 기본값 (첫 번째 등급)
            return gradeNames.Length > 0 ? gradeNames[0] : "등급 없음";
        }
        
        /// <summary>
        /// 다음 등급까지 필요한 명성도를 반환합니다.
        /// </summary>
        /// <returns>다음 등급까지 필요한 명성도 (최고 등급이면 -1)</returns>
        public int GetReputationToNextGrade()
        {
            for (int i = 0; i < gradeThresholds.Length; i++)
            {
                if (currentReputation < gradeThresholds[i])
                {
                    return gradeThresholds[i] - currentReputation;
                }
            }
            return -1; // 이미 최고 등급
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