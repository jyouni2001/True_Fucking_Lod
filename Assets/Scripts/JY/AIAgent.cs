using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using JY;

public interface IRoomDetector
{
    GameObject[] GetDetectedRooms();
    void DetectRooms();
}

public class AIAgent : MonoBehaviour
{
    #region 비공개 변수
    private NavMeshAgent agent;                    // AI 이동 제어를 위한 네비메시 에이전트
    private RoomManager roomManager;               // 룸 매니저 참조
    private Transform counterPosition;             // 카운터 위치
    private static List<RoomInfo> roomList = new List<RoomInfo>();  // 동적 룸 정보 리스트
    private Transform spawnPoint;                  // AI 생성/소멸 지점
    private int currentRoomIndex = -1;            // 현재 사용 중인 방 인덱스 (-1은 미사용)
    private AISpawner spawner;                    // AI 스포너 참조
    private float arrivalDistance = 0.5f;         // 도착 판정 거리

    private bool isInQueue = false;               // 대기열에 있는지 여부
    private Vector3 targetQueuePosition;          // 대기열 목표 위치
    private bool isWaitingForService = false;     // 서비스 대기 중인지 여부

    private AIState currentState = AIState.MovingToQueue;  // 현재 AI 상태
    private float counterWaitTime = 5f;           // 카운터 처리 시간
    private string currentDestination = "대기열로 이동 중";  // 현재 목적지 (UI 표시용)
    private bool isBeingServed = false;           // 서비스 받고 있는지 여부

    private static readonly object lockObject = new object();  // 스레드 동기화용 잠금 객체
    private Coroutine wanderingCoroutine;         // 배회 코루틴 참조
    private Coroutine roomUseCoroutine;           // 방 사용 코루틴 참조
    private int maxRetries = 3;                   // 위치 찾기 최대 시도 횟수

    [SerializeField] private CounterManager counterManager; // CounterManager 참조
    #endregion

    #region 룸 정보 클래스
    private class RoomInfo
    {
        public Transform transform;               // 룸의 Transform
        public bool isOccupied;                   // 룸 사용 여부
        public float size;                        // 룸 크기
        public GameObject gameObject;             // 룸 게임 오브젝트
        public string roomId;                     // 룸 고유 ID

        public RoomInfo(GameObject roomObj)
        {
            gameObject = roomObj;
            transform = roomObj.transform;
            isOccupied = false;

            var collider = roomObj.GetComponent<Collider>();
            size = collider != null ? collider.bounds.size.magnitude * 0.3f : 2f;
            if (collider == null)
            {
                Debug.LogWarning($"룸 {roomObj.name}에 Collider가 없습니다. 기본 크기(2) 사용.");
            }

            Vector3 pos = roomObj.transform.position;
            roomId = $"Room_{pos.x:F0}_{pos.z:F0}";
            Debug.Log($"룸 ID 생성: {roomId} at {pos}");
        }
    }
    #endregion

    #region AI 상태 열거형
    private enum AIState
    {
        Wandering,           // 배회 중
        MovingToQueue,       // 대기열로 이동 중
        WaitingInQueue,      // 대기열에서 대기 중
        MovingToRoom,        // 배정된 방으로 이동 중
        UsingRoom,          // 방 사용 중
        ReportingRoom,      // 방 사용 완료 보고 중
        ReturningToSpawn    // 스폰 지점으로 돌아가는 중
    }
    #endregion

    #region 이벤트
    public delegate void RoomsUpdatedHandler(GameObject[] rooms);
    private static event RoomsUpdatedHandler OnRoomsUpdated;
    #endregion

    #region 초기화
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void InitializeStatics()
    {
        // 정적 변수 초기화
        roomList.Clear();
        OnRoomsUpdated = null;
    }

    void Start()
    {
        // 필수 컴포넌트 초기화
        if (!InitializeComponents()) return;
        // 룸 리스트 초기화
        InitializeRoomsIfEmpty();
        // 초기 행동 결정
        DetermineInitialBehavior();
    }

    private bool InitializeComponents()
    {
        // NavMeshAgent 확인
        agent = GetComponent<NavMeshAgent>();
        if (agent == null)
        {
            Debug.LogError($"AI {gameObject.name}: NavMeshAgent 컴포넌트가 없습니다.");
            Destroy(gameObject);
            return false;
        }

        // 스폰 포인트 찾기
        GameObject spawn = GameObject.FindGameObjectWithTag("Spawn");
        if (spawn == null)
        {
            Debug.LogError($"AI {gameObject.name}: Spawn 오브젝트를 찾을 수 없습니다. Spawn 태그 확인 필요.");
            Destroy(gameObject);
            return false;
        }

        // RoomManager와 스폰 포인트 설정
        roomManager = FindObjectOfType<RoomManager>();
        spawnPoint = spawn.transform;

        // 카운터 찾기 (없을 수도 있음)
        GameObject counter = GameObject.FindGameObjectWithTag("Counter");
        counterPosition = counter != null ? counter.transform : null;

        // CounterManager 확인
        if (counterManager == null)
        {
            counterManager = FindObjectOfType<CounterManager>();
            if (counterManager == null)
            {
                Debug.LogWarning($"AI {gameObject.name}: CounterManager를 찾을 수 없습니다. 배회 또는 디스폰 모드로 전환.");
                // CounterManager가 없으면 카운터 위치도 무효화
                counterPosition = null;
            }
        }

        // Ground NavMesh 영역 확인
        if (NavMesh.GetAreaFromName("Ground") == 0)
        {
            Debug.LogWarning($"AI {gameObject.name}: Ground NavMesh 영역이 설정되지 않았습니다.");
        }

        return true;
    }

    private void InitializeRoomsIfEmpty()
    {
        lock (lockObject)
        {
            if (roomList.Count == 0)
            {
                InitializeRooms();
                if (OnRoomsUpdated == null)
                {
                    OnRoomsUpdated += UpdateRoomList;
                }
            }
        }
    }

    private void DetermineInitialBehavior()
    {
        if (counterPosition == null || counterManager == null)
        {
            // 카운터 또는 CounterManager가 없으면 배회 또는 디스폰
            float randomValue = Random.value;
            if (randomValue < 0.5f)
            {
                currentState = AIState.Wandering;
                currentDestination = "배회 중";
                wanderingCoroutine = StartCoroutine(WanderingBehavior());
                Debug.Log($"AI {gameObject.name}: 카운터/CounterManager 없음, 배회 시작 (50% 확률).");
            }
            else
            {
                currentState = AIState.ReturningToSpawn;
                currentDestination = "퇴장 중";
                agent.SetDestination(spawnPoint.position);
                Debug.Log($"AI {gameObject.name}: 카운터/CounterManager 없음, 스폰 지점으로 복귀 (50% 확률).");
            }
        }
        else
        {
            // 카운터와 CounterManager가 있으면 기존 로직
            float randomValue = Random.value;
            if (randomValue < 0.4f)
            {
                currentState = AIState.Wandering;
                currentDestination = "배회 중";
                wanderingCoroutine = StartCoroutine(WanderingBehavior());
                Debug.Log($"AI {gameObject.name}: 배회 시작 (40% 확률).");
            }
            else
            {
                currentState = AIState.MovingToQueue;
                currentDestination = "대기열로 이동 중";
                StartCoroutine(QueueBehavior());
                Debug.Log($"AI {gameObject.name}: 대기열로 이동 시작 (60% 확률).");
            }
        }
    }
    #endregion

    #region 룸 관리
    private void InitializeRooms()
    {
        roomList.Clear();
        Debug.Log($"AI {gameObject.name}: 룸 초기화 시작");

        // RoomDetector를 통한 룸 찾기
        var roomDetectors = GameObject.FindObjectsByType<RoomDetector>(FindObjectsSortMode.None);
        if (roomDetectors.Length > 0)
        {
            foreach (var detector in roomDetectors)
            {
                detector.ScanForRooms();
                detector.OnRoomsUpdated += rooms =>
                {
                    if (rooms != null && rooms.Length > 0)
                    {
                        UpdateRoomList(rooms);
                    }
                };
            }
            Debug.Log($"AI {gameObject.name}: RoomDetector로 룸 감지 시작.");


        }
        else
        {
            // 태그로 룸 찾기
            GameObject[] taggedRooms = GameObject.FindGameObjectsWithTag("Room");
            foreach (GameObject room in taggedRooms)
            {
                if (!roomList.Any(r => r.gameObject == room))
                {
                    roomList.Add(new RoomInfo(room));
                }
            }
            Debug.Log($"AI {gameObject.name}: 태그로 {roomList.Count}개 룸 발견.");
        }

        if (roomList.Count == 0)
        {
            Debug.LogError($"AI {gameObject.name}: 룸을 찾을 수 없습니다! Room 태그 확인 필요.");
        }
        else
        {
            Debug.Log($"AI {gameObject.name}: {roomList.Count}개 룸 초기화 완료.");
        }
    }

    public static void UpdateRoomList(GameObject[] newRooms)
    {
        if (newRooms == null || newRooms.Length == 0) return;

        lock (lockObject)
        {
            bool isUpdated = false;
            HashSet<string> processedRoomIds = new HashSet<string>();
            List<RoomInfo> updatedRoomList = new List<RoomInfo>();

            foreach (GameObject room in newRooms)
            {
                if (room != null)
                {
                    RoomInfo newRoom = new RoomInfo(room);
                    if (!processedRoomIds.Contains(newRoom.roomId))
                    {
                        processedRoomIds.Add(newRoom.roomId);
                        var existingRoom = roomList.FirstOrDefault(r => r.roomId == newRoom.roomId);
                        if (existingRoom != null)
                        {
                            newRoom.isOccupied = existingRoom.isOccupied;
                            updatedRoomList.Add(newRoom);
                            Debug.Log($"룸 업데이트: {newRoom.roomId}");
                        }
                        else
                        {
                            updatedRoomList.Add(newRoom);
                            isUpdated = true;
                            Debug.Log($"새 룸 추가: {newRoom.roomId}");
                        }
                    }
                }
            }

            if (updatedRoomList.Count > 0)
            {
                roomList = updatedRoomList;
                Debug.Log($"룸 리스트 업데이트 완료. 총 룸 수: {roomList.Count}");
                foreach (var room in roomList)
                {
                    Debug.Log($"- 룸 ID: {room.roomId}, 사용 중: {room.isOccupied}");
                }
            }
        }
    }

    public static void NotifyRoomsUpdated(GameObject[] rooms)
    {
        OnRoomsUpdated?.Invoke(rooms);
    }
    #endregion

    #region 업데이트 및 상태 머신
    void Update()
    {
        if (!agent.isOnNavMesh)
        {
            Debug.LogWarning($"AI {gameObject.name}: NavMesh 벗어남");
            ReturnToPool();
            return;
        }

        switch (currentState)
        {
            case AIState.Wandering:
                // 배회는 코루틴에서 처리
                break;
            case AIState.MovingToQueue:
            case AIState.WaitingInQueue:
                // 대기열 동작은 코루틴에서 처리
                break;
            case AIState.MovingToRoom:
                if (!agent.pathPending && agent.remainingDistance < arrivalDistance)
                {
                    StartCoroutine(UseRoom());
                }
                break;
            case AIState.UsingRoom:
                // 방 사용은 코루틴에서 처리
                break;
            case AIState.ReportingRoom:
                // 보고는 코루틴에서 처리
                break;
            case AIState.ReturningToSpawn:
                if (!agent.pathPending && agent.remainingDistance < arrivalDistance)
                {
                    Debug.Log($"AI {gameObject.name}: 스폰 지점 도착, 풀로 반환.");
                    ReturnToPool();
                }
                break;
        }
    }
    #endregion

    #region 대기열 동작
    private IEnumerator QueueBehavior()
    {
        // CounterManager 또는 카운터가 없으면 배회 또는 디스폰
        if (counterManager == null || counterPosition == null)
        {
            float randomValue = Random.value;
            if (randomValue < 0.5f)
            {
                TransitionToState(AIState.Wandering);
                wanderingCoroutine = StartCoroutine(WanderingBehavior());
                Debug.Log($"AI {gameObject.name}: CounterManager/카운터 없음, 배회로 전환.");
            }
            else
            {
                TransitionToState(AIState.ReturningToSpawn);
                agent.SetDestination(spawnPoint.position);
                Debug.Log($"AI {gameObject.name}: CounterManager/카운터 없음, 스폰 지점으로 복귀.");
            }
            yield break;
        }

        // 기존 대기열 로직
        if (!counterManager.TryJoinQueue(this))
        {
            if (currentRoomIndex == -1)
            {
                if (Random.value < 0.5f)
                {
                    TransitionToState(AIState.Wandering);
                    wanderingCoroutine = StartCoroutine(WanderingBehavior());
                }
                else
                {
                    TransitionToState(AIState.ReturningToSpawn);
                    agent.SetDestination(spawnPoint.position);
                }
            }
            else
            {
                yield return new WaitForSeconds(Random.Range(1f, 3f));
                StartCoroutine(QueueBehavior());
            }
            yield break;
        }

        isInQueue = true;
        TransitionToState(AIState.WaitingInQueue);

        while (isInQueue)
        {
            if (!agent.pathPending && agent.remainingDistance < arrivalDistance)
            {
                if (counterManager.CanReceiveService(this))
                {
                    counterManager.StartService(this);
                    isWaitingForService = true;

                    while (isWaitingForService)
                    {
                        yield return new WaitForSeconds(0.1f);
                    }

                    if (currentRoomIndex != -1)
                    {
                        roomList[currentRoomIndex].isOccupied = false;
                        currentRoomIndex = -1;
                        TransitionToState(AIState.ReturningToSpawn);
                        agent.SetDestination(spawnPoint.position);
                    }
                    else
                    {
                        if (TryAssignRoom())
                        {
                            TransitionToState(AIState.MovingToRoom);
                            agent.SetDestination(roomList[currentRoomIndex].transform.position);
                        }
                        else
                        {
                            if (Random.value < 0.5f)
                            {
                                TransitionToState(AIState.Wandering);
                                wanderingCoroutine = StartCoroutine(WanderingBehavior());
                            }
                            else
                            {
                                TransitionToState(AIState.ReturningToSpawn);
                                agent.SetDestination(spawnPoint.position);
                            }
                        }
                    }
                    break;
                }
            }
            yield return new WaitForSeconds(0.1f);
        }
    }

    private bool TryAssignRoom()
    {
        lock (lockObject)
        {
            var availableRooms = roomList.Select((room, index) => new { room, index })
                                         .Where(r => !r.room.isOccupied)
                                         .Select(r => r.index)
                                         .ToList();

            if (availableRooms.Count == 0)
            {
                Debug.Log($"AI {gameObject.name}: 사용 가능한 룸 없음.");
                return false;
            }

            int selectedRoomIndex = availableRooms[Random.Range(0, availableRooms.Count)];
            if (!roomList[selectedRoomIndex].isOccupied)
            {
                roomList[selectedRoomIndex].isOccupied = true;
                currentRoomIndex = selectedRoomIndex;
                Debug.Log($"AI {gameObject.name}: 룸 {selectedRoomIndex + 1}번 배정됨.");
                return true;
            }

            Debug.Log($"AI {gameObject.name}: 룸 {selectedRoomIndex + 1}번 이미 사용 중.");
            return false;
        }
    }
    #endregion

    #region 상태 전환
    private void TransitionToState(AIState newState)
    {
        // 기존 코루틴 정리
        CleanupCoroutines();
        if (currentState == AIState.UsingRoom)
        {
            isBeingServed = false;
        }

        currentState = newState;
        currentDestination = GetStateDescription(newState);
        Debug.Log($"AI {gameObject.name}: 상태 변경: {newState}");

        switch (newState)
        {
            case AIState.Wandering:
                wanderingCoroutine = StartCoroutine(WanderingBehavior());
                break;
            case AIState.MovingToRoom:
                if (currentRoomIndex != -1)
                {
                    agent.SetDestination(roomList[currentRoomIndex].transform.position);
                }
                break;
            case AIState.ReturningToSpawn:
                agent.SetDestination(spawnPoint.position);
                break;
        }
    }

    private string GetStateDescription(AIState state)
    {
        return state switch
        {
            AIState.Wandering => "배회 중",
            AIState.MovingToQueue => "대기열로 이동 중",
            AIState.WaitingInQueue => "대기열에서 대기 중",
            AIState.MovingToRoom => $"룸 {currentRoomIndex + 1}번으로 이동 중",
            AIState.UsingRoom => "룸 사용 중",
            AIState.ReportingRoom => "룸 사용 완료 보고 중",
            AIState.ReturningToSpawn => "퇴장 중",
            _ => "알 수 없는 상태"
        };
    }
    #endregion

    #region 룸 사용
    private IEnumerator UseRoom()
    {
        float roomUseTime = Random.Range(25f, 35f);
        float elapsedTime = 0f;
        bool stayInRoom = Random.value < 0.5f;
        float wanderRadius = 5f;

        if (currentRoomIndex < 0 || currentRoomIndex >= roomList.Count)
        {
            Debug.LogError($"AI {gameObject.name}: 잘못된 룸 인덱스 {currentRoomIndex}.");
            StartCoroutine(ReportRoomVacancy());
            yield break;
        }

        var roomManager = FindObjectOfType<RoomManager>();
        var room = roomList[currentRoomIndex].gameObject.GetComponent<RoomContents>();
        if (roomManager != null && room != null)
        {
            roomManager.ReportRoomUsage(gameObject.name, room);
        }

        TransitionToState(AIState.UsingRoom);

        if (stayInRoom)
        {
            Debug.Log($"AI {gameObject.name}: 룸 {currentRoomIndex + 1}번 내부 배회.");
            while (elapsedTime < roomUseTime && agent.isOnNavMesh)
            {
                Vector3 roomCenter = roomList[currentRoomIndex].transform.position;
                float roomSize = roomList[currentRoomIndex].size;
                if (TryGetValidPosition(roomCenter, roomSize, NavMesh.AllAreas, out Vector3 targetPos))
                {
                    agent.SetDestination(targetPos);
                }
                else
                {
                    Debug.LogWarning($"AI {gameObject.name}: 룸 {currentRoomIndex + 1}번 내부 유효 위치 없음. 외부 배회로 전환.");
                    stayInRoom = false;
                    break;
                }

                yield return new WaitForSeconds(Random.Range(2f, 5f));
                elapsedTime += Random.Range(2f, 5f);
            }
        }

        if (!stayInRoom)
        {
            Debug.Log($"AI {gameObject.name}: 룸 {currentRoomIndex + 1}번 외부 배회.");
            while (elapsedTime < roomUseTime && agent.isOnNavMesh)
            {
                Vector3 roomCenter = roomList[currentRoomIndex].transform.position;
                Vector3 randomPoint = roomCenter + Random.insideUnitSphere * wanderRadius;
                randomPoint.y = transform.position.y;

                if (NavMesh.SamplePosition(randomPoint, out NavMeshHit hit, wanderRadius, NavMesh.AllAreas))
                {
                    agent.SetDestination(hit.position);
                }

                yield return new WaitForSeconds(Random.Range(3f, 7f));
                elapsedTime += Random.Range(3f, 7f);
            }
        }

        Debug.Log($"AI {gameObject.name}: 룸 {currentRoomIndex + 1}번 사용 완료, 보고 시작.");
        StartCoroutine(ReportRoomVacancy());
    }

    private IEnumerator ReportRoomVacancy()
    {
        TransitionToState(AIState.ReportingRoom);
        int reportingRoomIndex = currentRoomIndex;
        Debug.Log($"AI {gameObject.name}: 룸 {reportingRoomIndex + 1}번 사용 완료 보고.");

        lock (lockObject)
        {
            if (reportingRoomIndex >= 0 && reportingRoomIndex < roomList.Count)
            {
                roomList[reportingRoomIndex].isOccupied = false;
                currentRoomIndex = -1;
                Debug.Log($"룸 {reportingRoomIndex + 1}번 비워짐.");
            }
        }

        var roomManager = FindObjectOfType<RoomManager>();
        if (roomManager != null)
        {
            int amount = roomManager.ProcessRoomPayment(gameObject.name);
            Debug.Log($"AI {gameObject.name}: 룸 결제 완료, 금액: {amount}원");
        }

        StartCoroutine(QueueBehavior());

        var paymentSystem = FindObjectOfType<PaymentSystem>();
        if (paymentSystem != null && !paymentSystem.HasUnpaidPayments(gameObject.name))
        {
            Debug.Log($"AI {gameObject.name}: 모든 결제 완료.");
        }
        else
        {
            Debug.LogWarning($"AI {gameObject.name}: 미결제 금액 존재.");
        }

        yield break;
    }
    #endregion

    #region 배회 동작
    private void WanderOnGround()
    {
        Vector3 randomPoint = transform.position + Random.insideUnitSphere * 10f;
        int groundMask = NavMesh.GetAreaFromName("Ground");
        if (groundMask == 0)
        {
            Debug.LogError($"AI {gameObject.name}: Ground NavMesh 영역 설정되지 않음. 배회 중단.");
            return;
        }

        if (NavMesh.SamplePosition(randomPoint, out NavMeshHit hit, 10f, groundMask))
        {
            agent.SetDestination(hit.position);
            Debug.Log($"AI {gameObject.name}: 새로운 배회 위치로 이동.");
        }
    }

    private IEnumerator WanderingBehavior()
    {
        float wanderingTime = Random.Range(15f, 30f);
        float elapsedTime = 0f;

        while (currentState == AIState.Wandering && elapsedTime < wanderingTime)
        {
            WanderOnGround();
            float waitTime = Random.Range(3f, 7f);
            yield return new WaitForSeconds(waitTime);
            elapsedTime += waitTime;
        }

        TransitionToState(AIState.ReturningToSpawn);
        agent.SetDestination(spawnPoint.position);
        Debug.Log($"AI {gameObject.name}: 배회 시간 종료, 스폰 지점으로 복귀.");
    }
    #endregion

    #region 유틸리티 메서드
    private bool TryGetValidPosition(Vector3 center, float radius, int layerMask, out Vector3 result)
    {
        result = center;
        float searchRadius = radius * 0.8f;

        for (int i = 0; i < maxRetries; i++)
        {
            Vector2 randomCircle = Random.insideUnitCircle * searchRadius;
            Vector3 randomPoint = center + new Vector3(randomCircle.x, 0, randomCircle.y);

            if (NavMesh.SamplePosition(randomPoint, out NavMeshHit hit, searchRadius, layerMask))
            {
                if (Vector3.Distance(hit.position, center) <= searchRadius)
                {
                    result = hit.position;
                    return true;
                }
            }
        }
        return false;
    }

    public void SetSpawner(AISpawner spawnerRef)
    {
        spawner = spawnerRef;
    }

    private void ReturnToPool()
    {
        CleanupCoroutines();
        CleanupResources();

        if (spawner != null)
        {
            spawner.ReturnToPool(gameObject);
        }
        else
        {
            Debug.LogWarning($"AI {gameObject.name}: 스포너 참조 없음, 오브젝트 파괴.");
            Destroy(gameObject);
        }
    }
    #endregion

    #region 정리
    void OnDisable()
    {
        CleanupCoroutines();
        CleanupResources();
    }

    void OnDestroy()
    {
        CleanupCoroutines();
        CleanupResources();
    }

    private void CleanupCoroutines()
    {
        if (wanderingCoroutine != null)
        {
            StopCoroutine(wanderingCoroutine);
            wanderingCoroutine = null;
        }
        if (roomUseCoroutine != null)
        {
            StopCoroutine(roomUseCoroutine);
            roomUseCoroutine = null;
        }
    }

    private void CleanupResources()
    {
        if (currentRoomIndex != -1)
        {
            lock (lockObject)
            {
                if (currentRoomIndex < roomList.Count)
                {
                    roomList[currentRoomIndex].isOccupied = false;
                    Debug.Log($"AI {gameObject.name} 정리: 룸 {currentRoomIndex + 1}번 반환");
                }
                currentRoomIndex = -1;
            }
        }

        isBeingServed = false;
        isInQueue = false;
        isWaitingForService = false;

        // CounterManager가 있으면 대기열에서 제거
        if (counterManager != null)
        {
            counterManager.LeaveQueue(this);
        }
    }
    #endregion

    #region UI
    void OnGUI()
    {
        Vector3 screenPos = Camera.main.WorldToScreenPoint(transform.position);
        if (screenPos.z > 0)
        {
            Vector2 guiPosition = new Vector2(screenPos.x, Screen.height - screenPos.y);
            GUI.Label(new Rect(guiPosition.x - 50, guiPosition.y - 50, 100, 40), currentDestination);
        }
    }
    #endregion

    #region 공개 메서드
    public void InitializeAI()
    {
        currentState = AIState.MovingToQueue;
        currentDestination = "대기열로 이동 중";
        isBeingServed = false;
        isInQueue = false;
        isWaitingForService = false;
        currentRoomIndex = -1;

        if (agent != null)
        {
            agent.ResetPath();
            DetermineInitialBehavior();
        }
    }

    void OnEnable()
    {
        InitializeAI();
    }

    public void SetQueueDestination(Vector3 position)
    {
        targetQueuePosition = position;
        if (agent != null)
        {
            agent.SetDestination(position);
        }
    }

    public void OnServiceComplete()
    {
        isWaitingForService = false;
        isInQueue = false;
        if (counterManager != null)
        {
            counterManager.LeaveQueue(this);
        }
    }
    #endregion
}