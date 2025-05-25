using UnityEngine;
using UnityEngine.UI;
using TMPro;
using JY;

/// <summary>
/// 명성도를 UI에 표시하는 컴포넌트
/// </summary>
public class ReputationUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI reputationText;
    [SerializeField] private Text reputationTextLegacy; // 기존 Text 컴포넌트 지원
    [SerializeField] private Slider reputationSlider; // 진행바로 표시하고 싶은 경우
    
    [Header("Display Settings")]
    [SerializeField] private string reputationPrefix = "명성도: ";
    [SerializeField] private bool showReputationGainAnimation = true;
    [SerializeField] private Color gainColor = Color.green;
    [SerializeField] private float animationDuration = 1f;
    
    [Header("Milestone Settings")]
    [SerializeField] private int[] reputationMilestones = {0, 100, 300, 500, 1000, 2000, 3000}; // 명성도 단계
    [SerializeField] private string[] milestoneNames = {"길바닥", "1성", "2성", "3성", "4성", "5성", "6성"}; // 단계별 이름
    
    private ReputationSystem reputationSystem;
    private int lastDisplayedReputation = 0;
    private Coroutine gainAnimationCoroutine;
    
    void Start()
    {
        // 명성도 시스템 찾기
        reputationSystem = ReputationSystem.Instance;
        if (reputationSystem == null)
        {
            reputationSystem = FindObjectOfType<ReputationSystem>();
        }
        
        if (reputationSystem != null)
        {
            // 이벤트 구독
            reputationSystem.OnReputationChanged += OnReputationChanged;
            // 초기값 설정
            UpdateReputationDisplay(reputationSystem.CurrentReputation);
        }
        else
        {
            Debug.LogWarning("ReputationSystem을 찾을 수 없습니다!");
        }
    }
    
    void OnDestroy()
    {
        // 이벤트 구독 해제
        if (reputationSystem != null)
        {
            reputationSystem.OnReputationChanged -= OnReputationChanged;
        }
    }
    
    private void OnReputationChanged(int newReputation)
    {
        if (showReputationGainAnimation && newReputation > lastDisplayedReputation)
        {
            // 애니메이션으로 증가 표시
            if (gainAnimationCoroutine != null)
            {
                StopCoroutine(gainAnimationCoroutine);
            }
            gainAnimationCoroutine = StartCoroutine(AnimateReputationGain(lastDisplayedReputation, newReputation));
        }
        else
        {
            // 즉시 업데이트
            UpdateReputationDisplay(newReputation);
        }
        
        lastDisplayedReputation = newReputation;
    }
    
    private void UpdateReputationDisplay(int reputation)
    {
        string displayText = reputationPrefix + reputation.ToString();
        
        // 마일스톤 정보 추가
        string milestoneInfo = GetMilestoneInfo(reputation);
        if (!string.IsNullOrEmpty(milestoneInfo))
        {
            displayText += $" ({milestoneInfo})";
        }
        
        // TextMeshPro 업데이트
        if (reputationText != null)
        {
            reputationText.text = displayText;
        }
        
        // 기존 Text 컴포넌트 업데이트
        if (reputationTextLegacy != null)
        {
            reputationTextLegacy.text = displayText;
        }
        
        // 슬라이더 업데이트
        if (reputationSlider != null)
        {
            int currentMilestone = GetCurrentMilestoneIndex(reputation);
            if (currentMilestone < reputationMilestones.Length)
            {
                int milestoneStart = currentMilestone > 0 ? reputationMilestones[currentMilestone - 1] : 0;
                int milestoneEnd = reputationMilestones[currentMilestone];
                float progress = (float)(reputation - milestoneStart) / (milestoneEnd - milestoneStart);
                reputationSlider.value = progress;
            }
            else
            {
                reputationSlider.value = 1f; // 최고 단계 달성
            }
        }
    }
    
    private string GetMilestoneInfo(int reputation)
    {
        int milestoneIndex = GetCurrentMilestoneIndex(reputation);
        
        if (milestoneIndex < milestoneNames.Length)
        {
            return milestoneNames[milestoneIndex];
        }
        else if (milestoneNames.Length > 0)
        {
            return milestoneNames[milestoneNames.Length - 1] + "+"; // 최고 등급 이상
        }
        
        return "";
    }
    
    private int GetCurrentMilestoneIndex(int reputation)
    {
        for (int i = 0; i < reputationMilestones.Length; i++)
        {
            if (reputation < reputationMilestones[i])
            {
                return i;
            }
        }
        return reputationMilestones.Length; // 모든 마일스톤을 넘어선 경우
    }
    
    private System.Collections.IEnumerator AnimateReputationGain(int fromReputation, int toReputation)
    {
        float elapsedTime = 0f;
        Color originalColor = Color.white;
        
        // 원래 색상 저장
        if (reputationText != null)
        {
            originalColor = reputationText.color;
        }
        else if (reputationTextLegacy != null)
        {
            originalColor = reputationTextLegacy.color;
        }
        
        while (elapsedTime < animationDuration)
        {
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / animationDuration;
            
            // 숫자 애니메이션
            int currentReputation = Mathf.RoundToInt(Mathf.Lerp(fromReputation, toReputation, progress));
            UpdateReputationDisplay(currentReputation);
            
            // 색상 애니메이션
            Color currentColor = Color.Lerp(gainColor, originalColor, progress);
            if (reputationText != null)
            {
                reputationText.color = currentColor;
            }
            if (reputationTextLegacy != null)
            {
                reputationTextLegacy.color = currentColor;
            }
            
            yield return null;
        }
        
        // 최종값으로 설정
        UpdateReputationDisplay(toReputation);
        
        // 원래 색상으로 복원
        if (reputationText != null)
        {
            reputationText.color = originalColor;
        }
        if (reputationTextLegacy != null)
        {
            reputationTextLegacy.color = originalColor;
        }
    }
    
    // 디버그용 - 인스펙터에서 테스트할 수 있도록
    [System.Serializable]
    public class DebugSettings
    {
        [Header("디버그 테스트")]
        public bool enableDebugButtons = true;     // private → public 변경
        public int testReputationAmount = 50;      // private → public 변경
    }
    
    [SerializeField] private DebugSettings debugSettings;
    
    void OnGUI()
    {
        if (debugSettings.enableDebugButtons && Application.isPlaying)
        {
            GUILayout.BeginArea(new Rect(10, 10, 200, 100));
            GUILayout.Label("명성도 디버그");
            
            if (GUILayout.Button($"명성도 +{debugSettings.testReputationAmount}"))
            {
                if (reputationSystem != null)
                {
                    reputationSystem.AddReputation("테스트", "테스트방");
                }
            }
            
            if (GUILayout.Button("명성도 초기화"))
            {
                if (reputationSystem != null)
                {
                    reputationSystem.SetReputation(0);
                }
            }
            
            GUILayout.EndArea();
        }
    }
}