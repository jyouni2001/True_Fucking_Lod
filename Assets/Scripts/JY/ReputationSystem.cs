using UnityEngine;
using System.Collections.Generic;
using TMPro;

namespace JY
{
    public class ReputationSystem : MonoBehaviour
    {
        [Header("명성도 설정")]
        [SerializeField] private int currentReputation = 0;

        [Header("UI 설정")]
        [SerializeField] private TextMeshProUGUI reputationText;
        [SerializeField] private string textFormat = "Grade: {0} {1}";

        [Header("등급 설정")]
        [SerializeField] private int[] gradeThresholds = { 0, 100, 300, 500, 1000, 2000, 3000 };
        [SerializeField] private string[] gradeNames = { "Ground", "Tier1", "Tier2", "Tier3", "Tier4", "Tier5", "Tier6" };

        [Header("디버그")]
        [SerializeField] private bool showDebugLogs = true;
        [SerializeField] private List<string> reputationLogs = new List<string>();

        public static ReputationSystem Instance { get; private set; }

        public System.Action<int> OnReputationChanged;

        public int CurrentReputation => currentReputation;

        void Awake()
        {
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
            UpdateUI();
        }

        public void AddReputation(string aiName, int reputation, List<string> roomIDs)
        {
            currentReputation += reputation;

            string roomList = string.Join(", ", roomIDs);
            string logMessage = $"{aiName}이(가) 방 {roomList} 사용 완료 - 명성도 +{reputation} (총 명성도: {currentReputation})";
            reputationLogs.Add(logMessage);

            if (showDebugLogs)
            {
                Debug.Log($"[명성도 시스템] {logMessage}");
            }

            UpdateUI();
            OnReputationChanged?.Invoke(currentReputation);
        }

        public void SetReputation(int reputation)
        {
            int oldReputation = currentReputation;
            currentReputation = Mathf.Max(0, reputation);

            if (showDebugLogs)
            {
                Debug.Log($"[명성도 시스템] 명성도 직접 설정: {oldReputation} -> {currentReputation}");
            }

            UpdateUI();
            OnReputationChanged?.Invoke(currentReputation);
        }

        private void UpdateUI()
        {
            if (reputationText != null)
            {
                string grade = GetCurrentGrade();
                reputationText.text = string.Format(textFormat, currentReputation, grade);
            }
        }

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
            return gradeNames.Length > 0 ? gradeNames[0] : "등급 없음";
        }

        public int GetReputationToNextGrade()
        {
            for (int i = 0; i < gradeThresholds.Length; i++)
            {
                if (currentReputation < gradeThresholds[i])
                {
                    return gradeThresholds[i] - currentReputation;
                }
            }
            return -1;
        }

        public void ClearLogs()
        {
            reputationLogs.Clear();
        }

        public List<string> GetReputationLogs()
        {
            return new List<string>(reputationLogs);
        }
    }
}