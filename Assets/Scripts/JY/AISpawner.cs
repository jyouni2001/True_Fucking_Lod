using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class AISpawner : MonoBehaviour 
{
    [Header("AI 프리팹 설정")]
    public GameObject aiPrefab;
    public int poolSize = 200;
    
    [Header("시간 기반 스폰 설정")]
    [SerializeField] private int minSpawner = 1;   // 최소 스폰 개수
    [SerializeField] private int maxSpawner = 5;   // 최대 스폰 개수
    [SerializeField] private int startHour = 9;    // 스폰 시작 시간 (9시)
    [SerializeField] private int endHour = 17;     // 스폰 종료 시간 (17시)
    [SerializeField] private int spawnInterval = 2; // 스폰 간격 (2시간)
    
    [Header("디버그 정보")]
    [SerializeField] private bool showDebugInfo = true;
    [SerializeField] private int currentActiveAIs = 0;
    
    private Queue<GameObject> aiPool;
    private List<GameObject> activeAIs;
    private TimeSystem timeSystem;
    
    // 다음 스폰 시간을 추적하기 위한 변수
    private List<int> spawnTimes = new List<int>();
    private int lastSpawnHour = -1;
    
    // 싱글톤 인스턴스
    public static AISpawner Instance { get; private set; }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        InitializePool();
        InitializeTimeSystem();
        SetupSpawnTimes();
    }
    
    void Update()
    {
        // 인스펙터에서 실시간으로 활성화된 AI 수 확인
        if (showDebugInfo)
        {
            currentActiveAIs = activeAIs.Count;
        }
    }
    
    /// <summary>
    /// TimeSystem 초기화 및 이벤트 구독
    /// </summary>
    private void InitializeTimeSystem()
    {
        timeSystem = TimeSystem.Instance;
        
        if (timeSystem != null)
        {
            // 시간이 변경될 때마다 스폰 체크
            timeSystem.OnMinuteChanged += CheckForSpawn;
        }
        else
        {
            Debug.LogError("TimeSystem을 찾을 수 없습니다!");
        }
    }
    
    /// <summary>
    /// 스폰 시간 리스트 설정 (9시, 11시, 13시, 15시, 17시)
    /// </summary>
    private void SetupSpawnTimes()
    {
        spawnTimes.Clear();
        
        for (int hour = startHour; hour <= endHour; hour += spawnInterval)
        {
            spawnTimes.Add(hour);
        }
        
        if (showDebugInfo)
        {
            Debug.Log($"스폰 시간 설정: {string.Join(", ", spawnTimes)}시");
        }
    }

    void InitializePool()
    {
        aiPool = new Queue<GameObject>();
        activeAIs = new List<GameObject>();

        // 풀에 AI 오브젝트들을 미리 생성
        for (int i = 0; i < poolSize; i++)
        {
            GameObject ai = Instantiate(aiPrefab, transform.position, Quaternion.identity);
            ai.name = $"AI_{i}";
            ai.SetActive(false);
            ai.transform.parent = transform;
            aiPool.Enqueue(ai);

            // AIAgent 컴포넌트에 spawner 참조 설정
            AIAgent aiAgent = ai.GetComponent<AIAgent>();
            if (aiAgent != null)
            {
                aiAgent.SetSpawner(this);
            }
        }
        
        if (showDebugInfo)
        {
            Debug.Log($"AI 풀 초기화 완료: {poolSize}개 생성");
        }
    }
    
    /// <summary>
    /// 매 분마다 호출되어 스폰 시간인지 확인
    /// </summary>
    private void CheckForSpawn(int hour, int minute)
    {
        // 정각(0분)일 때만 체크하고, 이미 이 시간에 스폰했다면 건너뛰기
        if (minute == 0 && spawnTimes.Contains(hour) && lastSpawnHour != hour)
        {
            int spawnCount = Random.Range(minSpawner, maxSpawner + 1);
            StartCoroutine(SpawnMultipleAIs(spawnCount));
            lastSpawnHour = hour;
            
            if (showDebugInfo)
            {
                Debug.Log($"[AI 스폰] {hour}시 정각: {spawnCount}명의 AI 스폰 (범위: {minSpawner}~{maxSpawner})");
            }
        }
    }
    
    /// <summary>
    /// 여러 AI를 연속으로 스폰 (약간의 딜레이를 두고)
    /// </summary>
    private IEnumerator SpawnMultipleAIs(int count)
    {
        for (int i = 0; i < count; i++)
        {
            SpawnAI();
            
            // 각 스폰 사이에 짧은 딜레이 (선택사항)
            if (i < count - 1)
            {
                yield return new WaitForSeconds(0.1f);
            }
        }
    }

    void SpawnAI()
    {
        if (aiPool.Count <= 0)
        {
            Debug.LogWarning("풀에 사용 가능한 AI가 없습니다!");
            return;
        }

        GameObject ai = aiPool.Dequeue();
        ai.transform.position = transform.position;
        ai.transform.rotation = Quaternion.identity;

        // AI 컴포넌트 초기화
        AIAgent aiAgent = ai.GetComponent<AIAgent>();
        if (aiAgent != null)
        {
            aiAgent.SetSpawner(this);
        }

        ai.SetActive(true);
        activeAIs.Add(ai);
        
        if (showDebugInfo)
        {
            Debug.Log($"{ai.name} 활성화됨 (현재 활성화된 AI: {activeAIs.Count}개)");
        }
    }

    // AI 오브젝트를 풀로 반환
    public void ReturnToPool(GameObject ai)
    {
        if (ai == null) return;

        ai.SetActive(false);
        activeAIs.Remove(ai);
        aiPool.Enqueue(ai);
        ai.transform.position = transform.position;
        
        if (showDebugInfo)
        {
            Debug.Log($"{ai.name} 비활성화됨 (남은 풀 개수: {aiPool.Count}개)");
        }
    }

    // 모든 활성화된 AI를 풀로 반환
    public void ReturnAllToPool()
    {
        foreach (var ai in activeAIs.ToArray())
        {
            ReturnToPool(ai);
        }
        activeAIs.Clear();
        
        if (showDebugInfo)
        {
            Debug.Log("모든 AI가 풀로 반환되었습니다.");
        }
    }
    
    /// <summary>
    /// 수동으로 AI 스폰 (테스트용) 안써도됨
    /// </summary>
    /// <param name="count">스폰할 개수 (-1이면 랜덤)</param>
    public void ManualSpawn(int count = -1)
    {
        if (count < 0)
        {
            count = Random.Range(minSpawner, maxSpawner + 1);
        }
        
        StartCoroutine(SpawnMultipleAIs(count));
        
        if (showDebugInfo)
        {
            Debug.Log($"수동 스폰: {count}명의 AI 생성");
        }
    }
    
    /// <summary>
    /// 스폰 설정을 런타임에서 변경
    /// </summary>
    public void SetSpawnSettings(int minSpawn, int maxSpawn, int startH, int endH, int intervalH)
    {
        minSpawner = minSpawn;
        maxSpawner = maxSpawn;
        startHour = startH;
        endHour = endH;
        spawnInterval = intervalH;
        
        SetupSpawnTimes();
        lastSpawnHour = -1; // 스폰 시간 리셋
        
        if (showDebugInfo)
        {
            Debug.Log($"스폰 설정 변경 - 시간: {startHour}~{endHour}시, 간격: {spawnInterval}시간, AI 수: {minSpawner}~{maxSpawner}명");
        }
    }
    
    /// <summary>
    /// 현재 활성화된 AI 수 반환
    /// </summary>
    public int GetActiveAICount()
    {
        return activeAIs.Count;
    }
    
    /// <summary>
    /// 풀에 남은 AI 수 반환
    /// </summary>
    public int GetPooledAICount()
    {
        return aiPool.Count;
    }
    
    void OnDestroy()
    {
        // 이벤트 구독 해제
        if (timeSystem != null)
        {
            timeSystem.OnMinuteChanged -= CheckForSpawn;
        }
    }
    
    void OnValidate()
    {
        // 인스펙터에서 값 변경 시 유효성 검사
        minSpawner = Mathf.Max(1, minSpawner);
        maxSpawner = Mathf.Max(minSpawner, maxSpawner);
        
        // 인스펙터에서 값 변경 시 스폰 시간 재설정
        if (Application.isPlaying)
        {
            SetupSpawnTimes();
        }
    }
}