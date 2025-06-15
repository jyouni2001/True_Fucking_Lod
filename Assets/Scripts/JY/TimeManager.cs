using UnityEngine;
using UnityEngine.UI;
using TMPro;
using JY;

namespace JY
{
    /// <summary>
    /// 게임 내 시간 UI 및 이벤트를 관리하는 클래스
    /// </summary>
    public class TimeManager : MonoBehaviour
    {
        #region Fields & Properties

        [Header("UI 요소")]
        [SerializeField] private TextMeshProUGUI timeText;
        [SerializeField] private TextMeshProUGUI phaseText;
        
        [Header("하루 단계 색상")]
        [SerializeField] private Color morningColor = new Color(1f, 0.9f, 0.7f);
        [SerializeField] private Color afternoonColor = new Color(1f, 1f, 1f);
        [SerializeField] private Color eveningColor = new Color(0.9f, 0.7f, 0.4f);
        [SerializeField] private Color nightColor = new Color(0.1f, 0.1f, 0.3f);
        
        private TimeSystem timeSystem;

        #endregion

        #region Unity Lifecycle Methods
        
        private void Start()
        {
            InitializeTimeSystem();
        }
        
        private void OnDestroy()
        {
            UnsubscribeEvents();
        }

        #endregion

        #region Initialization

        /// <summary>
        /// TimeSystem 초기화 및 이벤트 구독
        /// </summary>
        private void InitializeTimeSystem()
        {
            // TimeSystem 참조 가져오기
            timeSystem = TimeSystem.Instance;
            
            // 이벤트 구독
            SubscribeEvents();
            
            // UI 초기화
            UpdateTimeUI(timeSystem.CurrentHour, timeSystem.CurrentMinute);
            UpdatePhaseUI(timeSystem.CurrentDayPhase);
        }

        /// <summary>
        /// 모든 이벤트 구독
        /// </summary>
        private void SubscribeEvents()
        {
            if (timeSystem != null)
            {
                timeSystem.OnMinuteChanged += UpdateTimeUI;
                timeSystem.OnDayPhaseChanged += UpdatePhaseUI;
                timeSystem.OnTimeEvent += HandleTimeEvent;
            }
        }

        /// <summary>
        /// 모든 이벤트 구독 해제
        /// </summary>
        private void UnsubscribeEvents()
        {
            if (timeSystem != null)
            {
                timeSystem.OnMinuteChanged -= UpdateTimeUI;
                timeSystem.OnDayPhaseChanged -= UpdatePhaseUI;
                timeSystem.OnTimeEvent -= HandleTimeEvent;
            }
        }

        #endregion

        #region UI Update Methods
        
        /// <summary>
        /// 시간 UI 업데이트
        /// </summary>
        private void UpdateTimeUI(int hour, int minute)
        {
            if (timeText != null)
            {
                timeText.text = string.Format("{0:00}:{1:00}", hour, minute);
            }
        }
        
        /// <summary>
        /// 하루 단계 UI 업데이트
        /// </summary>
        private void UpdatePhaseUI(TimeSystem.DayPhase newPhase)
        {
            if (phaseText != null)
            {
                switch (newPhase)
                {
                    case TimeSystem.DayPhase.Morning:
                        phaseText.text = "아침";
                        break;
                    case TimeSystem.DayPhase.Afternoon:
                        phaseText.text = "오후";
                        break;
                    case TimeSystem.DayPhase.Evening:
                        phaseText.text = "저녁";
                        break;
                    case TimeSystem.DayPhase.Night:
                        phaseText.text = "밤";
                        break;
                }
            }
        }

        #endregion
        
        #region Event Handlers
        
        /// <summary>
        /// 특정 시간 이벤트 처리
        /// </summary>
        private void HandleTimeEvent(float eventTime)
        {
            Debug.Log($"시간 이벤트 발생: {eventTime}시");
            
            // 각 시간대별 이벤트 처리
            if (eventTime == 6f)
            {
                Debug.Log("아침 이벤트: 일과 시작");
                // 아침 관련 이벤트 처리
            }
            else if (eventTime == 12f)
            {
                Debug.Log("정오 이벤트: 점심 시간");
                // 정오 관련 이벤트 처리
            }
            else if (eventTime == 18f)
            {
                Debug.Log("저녁 이벤트: 저녁 시간");
                // 저녁 관련 이벤트 처리
            }
            else if (eventTime == 0f)
            {
                Debug.Log("자정 이벤트: 하루 종료");
                // 자정 관련 이벤트 처리
            }
        }

        #endregion
        
        #region Public Time Control Methods
        
        /// <summary>
        /// 아침 시간(07:00)으로 설정
        /// </summary>
        public void SetMorningTime()
        {
            timeSystem.SetTime(7, 0);
        }
        
        /// <summary>
        /// 정오 시간(12:00)으로 설정
        /// </summary>
        public void SetNoonTime()
        {
            timeSystem.SetTime(12, 0);
        }
        
        /// <summary>
        /// 저녁 시간(19:00)으로 설정
        /// </summary>
        public void SetEveningTime()
        {
            timeSystem.SetTime(19, 0);
        }
        
        /// <summary>
        /// 밤 시간(23:00)으로 설정
        /// </summary>
        public void SetNightTime()
        {
            timeSystem.SetTime(23, 0);
        }
        
        /// <summary>
        /// 기본 시간 흐름 속도 설정 (1초당 1분)
        /// </summary>
        public void SetNormalSpeed()
        {
            timeSystem.SetTimeMultiplier(3600f);
        }
        
        /// <summary>
        /// 빠른 시간 흐름 속도 설정 (1초당 10분)
        /// </summary>
        public void SetFastSpeed()
        {
            timeSystem.SetTimeMultiplier(360f);
        }
        
        /// <summary>
        /// 시간 일시정지
        /// </summary>
        public void PauseTime()
        {
            timeSystem.PauseTime();
        }
        
        /// <summary>
        /// 시간 재개
        /// </summary>
        public void ResumeTime()
        {
            timeSystem.ResumeTime();
        }

        #endregion
    }
}