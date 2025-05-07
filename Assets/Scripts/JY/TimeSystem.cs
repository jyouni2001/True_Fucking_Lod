using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 24시간 순환 시간 시스템을 관리하는 클래스
/// 싱글톤 패턴으로 구현되어 전체 게임에서 접근 가능
/// </summary>
public class TimeSystem : MonoBehaviour
{
    #region Enums

    /// <summary>
    /// 하루의 단계 열거형
    /// </summary>
    public enum DayPhase
    {
        Morning,    // 아침 (06:00 - 11:59)
        Afternoon,  // 오후 (12:00 - 17:59)
        Evening,    // 저녁 (18:00 - 21:59)
        Night       // 밤 (22:00 - 05:59)
    }

    #endregion

    #region Fields & Properties

    [Header("시간 설정")]
    [Tooltip("1초당 게임 내 시간 흐름 배수")]
    public float timeMultiplier = 60f; // 기본값: 1초에 60초(1분)가 흐름
    
    [Tooltip("게임 시작 시 설정할 시간 (0-24)")]
    [Range(0, 24)] public float startingHour = 6f; // 게임 시작 시간 (06:00)
    
    [Header("시간 정보")]
    [SerializeField] private float currentTime; // 현재 시간 (초 단위)
    
    [Header("이벤트 설정")]
    [Tooltip("특정 시간에 이벤트 발생 여부")]
    public bool useTimeEvents = false;
    
    [Tooltip("이벤트를 발생시킬 시간 목록 (24시간제)")]
    public List<float> eventTimes = new List<float>() { 6f, 12f, 18f, 0f }; // 기본 이벤트 시간
    
    // 시간 관련 속성들
    public int CurrentHour { get; private set; }
    public int CurrentMinute { get; private set; }
    public int CurrentSecond { get; private set; }
    public string CurrentTimeString { get; private set; }
    public DayPhase CurrentDayPhase { get; private set; }

    // 싱글톤 인스턴스
    private static TimeSystem _instance;

    #endregion

    #region Events & Delegates

    /// <summary>
    /// 시간 변경 이벤트 델리게이트
    /// </summary>
    public delegate void TimeChangeHandler(int hour, int minute);
    
    /// <summary>
    /// 하루 단계 변경 이벤트 델리게이트
    /// </summary>
    public delegate void DayPhaseChangeHandler(DayPhase newPhase);
    
    /// <summary>
    /// 특정 시간 이벤트 델리게이트
    /// </summary>
    public delegate void TimeEventHandler(float eventTime);
    
    // 이벤트 선언
    public event TimeChangeHandler OnHourChanged;
    public event TimeChangeHandler OnMinuteChanged; 
    public event DayPhaseChangeHandler OnDayPhaseChanged;
    public event TimeEventHandler OnTimeEvent;

    #endregion

    #region Singleton Implementation
    
    /// <summary>
    /// 싱글톤 인스턴스 접근자
    /// </summary>
    public static TimeSystem Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<TimeSystem>();
                if (_instance == null)
                {
                    GameObject obj = new GameObject("TimeSystem");
                    _instance = obj.AddComponent<TimeSystem>();
                }
            }
            return _instance;
        }
    }

    #endregion

    #region Unity Lifecycle Methods
    
    private void Awake()
    {
        InitializeSingleton();
    }
    
    private void Start()
    {
        // 초기 상태 설정
        UpdateDayPhase();
    }
    
    private void Update()
    {
        UpdateTime();
    }

    #endregion

    #region Initialization

    /// <summary>
    /// 싱글톤 패턴 초기화
    /// </summary>
    private void InitializeSingleton()
    {
        // 싱글톤 패턴 - 중복 인스턴스 방지
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        _instance = this;
        DontDestroyOnLoad(gameObject); // 씬 전환 시에도 유지
        
        // 초기 시간 설정
        currentTime = startingHour * 3600f; // 시간을 초 단위로 변환
        UpdateTimeValues(); // 시간 값 초기화
    }

    #endregion

    #region Time Update Methods

    /// <summary>
    /// 매 프레임 시간 업데이트
    /// </summary>
    private void UpdateTime()
    {
        // 이전 시간 값 저장
        int prevHour = CurrentHour;
        int prevMinute = CurrentMinute;
        DayPhase prevPhase = CurrentDayPhase;
        
        // 시간 업데이트
        currentTime += Time.deltaTime * timeMultiplier;
        if (currentTime >= 86400f) // 하루는 86400초 (24시간)
        {
            currentTime -= 86400f;
        }
        
        // 시간 값 업데이트
        UpdateTimeValues();
        
        // 시간 이벤트 검사
        if (useTimeEvents)
        {
            CheckTimeEvents();
        }
        
        // 시간 변경 이벤트 발생
        if (CurrentHour != prevHour)
        {
            OnHourChanged?.Invoke(CurrentHour, CurrentMinute);
        }
        
        if (CurrentMinute != prevMinute)
        {
            OnMinuteChanged?.Invoke(CurrentHour, CurrentMinute);
        }
        
        // 하루 단계 업데이트
        if (CurrentDayPhase != prevPhase)
        {
            OnDayPhaseChanged?.Invoke(CurrentDayPhase);
        }
    }
    
    /// <summary>
    /// 시간 값 업데이트 메서드
    /// </summary>
    private void UpdateTimeValues()
    {
        float hourTime = currentTime / 3600f;
        CurrentHour = Mathf.FloorToInt(hourTime) % 24;
        CurrentMinute = Mathf.FloorToInt((hourTime * 60) % 60);
        CurrentSecond = Mathf.FloorToInt((hourTime * 3600) % 60);
        
        CurrentTimeString = string.Format("{0:00}:{1:00}", CurrentHour, CurrentMinute);
        
        UpdateDayPhase();
    }
    
    /// <summary>
    /// 하루 단계 업데이트
    /// </summary>
    private void UpdateDayPhase()
    {
        if (CurrentHour >= 6 && CurrentHour < 12)
        {
            CurrentDayPhase = DayPhase.Morning;
        }
        else if (CurrentHour >= 12 && CurrentHour < 18)
        {
            CurrentDayPhase = DayPhase.Afternoon;
        }
        else if (CurrentHour >= 18 && CurrentHour < 22)
        {
            CurrentDayPhase = DayPhase.Evening;
        }
        else
        {
            CurrentDayPhase = DayPhase.Night;
        }
    }
    
    /// <summary>
    /// 시간 이벤트 체크
    /// </summary>
    private void CheckTimeEvents()
    {
        if (eventTimes == null || eventTimes.Count == 0)
            return;
            
        float currentHourFloat = currentTime / 3600f % 24f;
        
        foreach (float eventTime in eventTimes)
        {
            // 분 단위로 약간의 여유를 두고 이벤트 발생 체크 (정확한 시간 ±0.5분)
            if (Mathf.Abs(currentHourFloat - eventTime) < (0.5f / 60f))
            {
                OnTimeEvent?.Invoke(eventTime);
                break;
            }
        }
    }

    #endregion

    #region Public Time Control Methods
    
    /// <summary>
    /// 시간 설정 메서드
    /// </summary>
    /// <param name="hour">시 (0-23)</param>
    /// <param name="minute">분 (0-59)</param>
    public void SetTime(int hour, int minute = 0)
    {
        currentTime = (hour * 3600f) + (minute * 60f);
        UpdateTimeValues();
    }
    
    /// <summary>
    /// 시간 배속 설정
    /// </summary>
    /// <param name="multiplier">시간 배속 (0 이상)</param>
    public void SetTimeMultiplier(float multiplier)
    {
        timeMultiplier = Mathf.Max(0, multiplier);
    }
    
    /// <summary>
    /// 시간 일시 정지
    /// </summary>
    public void PauseTime()
    {
        timeMultiplier = 0;
    }
    
    /// <summary>
    /// 시간 흐름 재개
    /// </summary>
    /// <param name="multiplier">재개할 시간 배속 (-1: 기본값)</param>
    public void ResumeTime(float multiplier = -1)
    {
        if (multiplier < 0)
        {
            // 기본값으로 돌아가기
            timeMultiplier = 60f;
        }
        else
        {
            timeMultiplier = multiplier;
        }
    }

    #endregion
}