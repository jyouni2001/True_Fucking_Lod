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
    private Coroutine roomWanderingCoroutine;     // 방 내부 배회 코루틴 참조
    private int maxRetries = 3;                   // 위치 찾기 최대 시도 횟수

    [SerializeField] private CounterManager counterManager; // CounterManager 참조
    private TimeSystem timeSystem;                // 시간 시스템 참조
    private int lastBehaviorUpdateHour = -1;      // 마지막 행동 업데이트 시간
    #endregion

    #region 룸 정보 클래스
    private class RoomInfo
    {
        public Transform transform;               // 룸의 Transform
        public bool isOccupied;                   // 룸 사용 여부
        public float size;                        // 룸 크기
        public GameObject gameObject;             // 룸 게임 오브젝트
        public string roomId;                     // 룸 고유 ID
        public Bounds bounds;                     // 룸의 Bounds

        public RoomInfo(GameObject roomObj)
        {
            gameObject = roomObj;
            transform = roomObj.transform;
            isOccupied = false;

            var collider = roomObj.GetComponent<Collider>();
            size = collider != null ? collider.bounds.size.magnitude * 0.3f : 2f;
            var roomContents = roomObj.GetComponent<RoomContents>();
            bounds = roomContents != null ? roomContents.roomBounds : (collider != null ? collider.bounds : new Bounds(transform.position, Vector3.one * 2f));
            if (collider == null)
            {
                Debug.LogWarning($"룸 {roomObj.name}에 Collider가 없습니다. 기본 크기(2) 사용.");
            }

            Vector3 pos = roomObj.transform.position;
            roomId = $"Room_{pos.x:F0}_{pos.z:F0}";
            Debug.Log($"룸 ID 생성: {roomId} at {pos}, Bounds: {bounds}");
        }
    }
    #endregion

    #region AI 상태 열거형
    private enum AIState
    {
        Wandering,           // 외부 배회
        MovingToQueue,       // 대기열로 이동
        WaitingInQueue,      // 대기열에서 대기
        MovingToRoom,        // 배정된 방으로 이동
        UsingRoom,           // 방 사용
        ReportingRoom,       // 방 사용 완료 보고
        ReturningToSpawn,    // 스폰 지점으로 복귀 (디스폰)
        RoomWandering,       // 방 내부 배회
        ReportingRoomQueue   // 방 사용 완료 보고를 위해 대기열로 이동
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
        roomList.Clear();
        OnRoomsUpdated = null;
    }

    void Start()
    {
        if (!InitializeComponents()) return;
        InitializeRoomsIfEmpty();
        timeSystem = TimeSystem.Instance;
        DetermineInitialBehavior();
    }

    private bool InitializeComponents()
    {
        agent = GetComponent<NavMeshAgent>();
        if (agent == null)
        {
            Debug.LogError($"AI {gameObject.name}: NavMeshAgent 컴포넌트가 없습니다.");
            Destroy(gameObject);
            return false;
        }

        GameObject spawn = GameObject.FindGameObjectWithTag("Spawn");
        if (spawn == null)
        {
            Debug.LogError($"AI {gameObject.name}: Spawn 오브젝트를 찾을 수 없습니다.");
            Destroy(gameObject);
            return false;
        }

        roomManager = FindObjectOfType<RoomManager>();
        spawnPoint = spawn.transform;

        GameObject counter = GameObject.FindGameObjectWithTag("Counter");
        counterPosition = counter != null ? counter.transform : null;

        if (counterManager == null)
        {
            counterManager = FindObjectOfType<CounterManager>();
            if (counterManager == null)
            {
                Debug.LogWarning($"AI {gameObject.name}: CounterManager를 찾을 수 없습니다.");
                counterPosition = null;
            }
        }

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
        DetermineBehaviorByTime();
    }
    #endregion

    #region 룸 관리
    private void InitializeRooms()
    {
        roomList.Clear();
        Debug.Log($"AI {gameObject.name}: 룸 초기화 시작");

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
            Debug.LogWarning($"AI {gameObject.name}: 룸을 찾을 수 없습니다!");
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
                        }
                        else
                        {
                            updatedRoomList.Add(newRoom);
                            isUpdated = true;
                        }
                    }
                }
            }

            if (updatedRoomList.Count > 0)
            {
                roomList = updatedRoomList;
                Debug.Log($"룸 리스트 업데이트 완료. 총 룸 수: {roomList.Count}");
            }
        }
    }

    public static void NotifyRoomsUpdated(GameObject[] rooms)
    {
        OnRoomsUpdated?.Invoke(rooms);
    }
    #endregion

    #region 시간 기반 행동 결정
    private void DetermineBehaviorByTime()
    {
        if (timeSystem == null)
        {
            Debug.LogWarning($"AI {gameObject.name}: TimeSystem이 없습니다. 기본 행동으로 전환.");
            FallbackBehavior();
            return;
        }

        int hour = timeSystem.CurrentHour;
        int minute = timeSystem.CurrentMinute;

        // 17:00에 방 사용 중이 아닌 에이전트 디스폰
        if (hour == 17 && minute == 0 && currentState != AIState.UsingRoom)
        {
            TransitionToState(AIState.ReturningToSpawn);
            Debug.Log($"AI {gameObject.name}: 17:00, 방 사용 중 아님, 디스폰.");
            return;
        }

        if (hour >= 0 && hour < 9)
        {
            // 0:00 ~ 9:00
            if (currentRoomIndex != -1)
            {
                TransitionToState(AIState.RoomWandering);
                Debug.Log($"AI {gameObject.name}: 0~9시, 방 내부 배회.");
            }
            else
            {
                FallbackBehavior();
            }
        }
        else if (hour >= 9 && hour < 11)
        {
            // 9:00 ~ 11:00
            if (currentRoomIndex != -1)
            {
                TransitionToState(AIState.ReportingRoomQueue);
                Debug.Log($"AI {gameObject.name}: 9~11시, 방 사용 완료 보고 대기열로 이동.");
            }
            else
            {
                FallbackBehavior();
            }
        }
        else if (hour >= 11 && hour < 17)
        {
            // 11:00 ~ 17:00
            if (currentRoomIndex == -1)
            {
                float randomValue = Random.value;
                if (randomValue < 0.2f)
                {
                    TransitionToState(AIState.MovingToQueue);
                    Debug.Log($"AI {gameObject.name}: 11~17시, 방 없음, 대기열로 이동 (20%).");
                }
                else if (randomValue < 0.8f)
                {
                    TransitionToState(AIState.Wandering);
                    Debug.Log($"AI {gameObject.name}: 11~17시, 방 없음, 외부 배회 (60%).");
                }
                else
                {
                    TransitionToState(AIState.ReturningToSpawn);
                    Debug.Log($"AI {gameObject.name}: 11~17시, 방 없음, 디스폰 (20%).");
                }
            }
            else
            {
                float randomValue = Random.value;
                if (randomValue < 0.5f)
                {
                    TransitionToState(AIState.Wandering);
                    Debug.Log($"AI {gameObject.name}: 11~17시, 방 있음, 외부 배회 (50%).");
                }
                else
                {
                    TransitionToState(AIState.RoomWandering);
                    Debug.Log($"AI {gameObject.name}: 11~17시, 방 있음, 방 내부 배회 (50%).");
                }
            }
        }
        else
        {
            // 17:00 ~ 0:00
            if (currentRoomIndex != -1)
            {
                float randomValue = Random.value;
                if (randomValue < 0.5f)
                {
                    TransitionToState(AIState.Wandering);
                    Debug.Log($"AI {gameObject.name}: 17~0시, 방 있음, 외부 배회 (50%).");
                }
                else
                {
                    TransitionToState(AIState.RoomWandering);
                    Debug.Log($"AI {gameObject.name}: 17~0시, 방 있음, 방 내부 배회 (50%).");
                }
            }
            else
            {
                FallbackBehavior();
            }
        }

        lastBehaviorUpdateHour = hour;
    }

    private void FallbackBehavior()
    {
        if (counterPosition == null || counterManager == null)
        {
            float randomValue = Random.value;
            if (randomValue < 0.5f)
            {
                TransitionToState(AIState.Wandering);
                Debug.Log($"AI {gameObject.name}: 카운터 없음, 배회 (50%).");
            }
            else
            {
                TransitionToState(AIState.ReturningToSpawn);
                Debug.Log($"AI {gameObject.name}: 카운터 없음, 디스폰 (50%).");
            }
        }
        else
        {
            float randomValue = Random.value;
            if (randomValue < 0.4f)
            {
                TransitionToState(AIState.Wandering);
                Debug.Log($"AI {gameObject.name}: 기본 행동, 배회 (40%).");
            }
            else
            {
                TransitionToState(AIState.MovingToQueue);
                Debug.Log($"AI {gameObject.name}: 기본 행동, 대기열로 이동 (60%).");
            }
        }
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

        // 시간 기반 행동 갱신
        if (timeSystem != null)
        {
            int hour = timeSystem.CurrentHour;
            int minute = timeSystem.CurrentMinute;

            // 17:00에 방 사용 중이 아닌 에이전트 디스폰
            if (hour == 17 && minute == 0 && currentState != AIState.UsingRoom && currentState != AIState.ReturningToSpawn)
            {
                TransitionToState(AIState.ReturningToSpawn);
                Debug.Log($"AI {gameObject.name}: 17:00, 방 사용 중 아님, 강제 디스폰.");
                return;
            }

            // 매 정시 행동 초기화 (11:00~16:00)
            if (hour >= 11 && hour < 17 && minute == 0 && hour != lastBehaviorUpdateHour &&
                currentState != AIState.UsingRoom && currentState != AIState.WaitingInQueue &&
                currentState != AIState.MovingToRoom && currentState != AIState.ReportingRoom)
            {
                DetermineBehaviorByTime();
            }

            // 시간대 전환 시 행동 재결정
            if ((hour == 0 || hour == 9 || hour == 11 || hour == 17) && minute == 0 &&
                currentState != AIState.UsingRoom && currentState != AIState.WaitingInQueue &&
                currentState != AIState.MovingToRoom && currentState != AIState.ReportingRoom)
            {
                DetermineBehaviorByTime();
            }
        }

        switch (currentState)
        {
            case AIState.Wandering:
                break;
            case AIState.MovingToQueue:
            case AIState.WaitingInQueue:
            case AIState.ReportingRoomQueue:
                break;
            case AIState.MovingToRoom:
                if (currentRoomIndex != -1 && currentRoomIndex < roomList.Count)
                {
                    Bounds roomBounds = roomList[currentRoomIndex].bounds;
                    if (!agent.pathPending && agent.remainingDistance < arrivalDistance && roomBounds.Contains(transform.position))
                    {
                        Debug.Log($"AI {gameObject.name}: 룸 {currentRoomIndex + 1}번 도착.");
                        StartCoroutine(UseRoom());
                    }
                }
                break;
            case AIState.UsingRoom:
            case AIState.RoomWandering:
                break;
            case AIState.ReportingRoom:
                break;
            case AIState.ReturningToSpawn:
                if (!agent.pathPending && agent.remainingDistance < arrivalDistance)
                {
                    Debug.Log($"AI {gameObject.name}: 스폰 지점 도착, 디스폰.");
                    ReturnToPool();
                }
                break;
        }
    }
    #endregion

    #region 대기열 동작
    private IEnumerator QueueBehavior()
    {
        if (counterManager == null || counterPosition == null)
        {
            float randomValue = Random.value;
            if (randomValue < 0.5f)
            {
                TransitionToState(AIState.Wandering);
                wanderingCoroutine = StartCoroutine(WanderingBehavior());
            }
            else
            {
                TransitionToState(AIState.ReturningToSpawn);
                agent.SetDestination(spawnPoint.position);
            }
            yield break;
        }

        if (!counterManager.TryJoinQueue(this))
        {
            if (currentRoomIndex == -1)
            {
                float randomValue = Random.value;
                if (randomValue < 0.5f)
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
        TransitionToState(currentState == AIState.ReportingRoomQueue ? AIState.ReportingRoomQueue : AIState.WaitingInQueue);

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

                    if (currentState == AIState.ReportingRoomQueue)
                    {
                        StartCoroutine(ReportRoomVacancy());
                    }
                    else if (currentRoomIndex != -1)
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
                            float randomValue = Random.value;
                            if (randomValue < 0.5f)
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
            case AIState.MovingToQueue:
            case AIState.ReportingRoomQueue:
                StartCoroutine(QueueBehavior());
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
            case AIState.RoomWandering:
                roomWanderingCoroutine = StartCoroutine(RoomWanderingBehavior());
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
            AIState.RoomWandering => $"룸 {currentRoomIndex + 1}번 내부 배회 중",
            AIState.ReportingRoomQueue => "사용 완료 보고 대기열로 이동 중",
            _ => "알 수 없는 상태"
        };
    }
    #endregion

    #region 룸 사용
    private IEnumerator UseRoom()
    {
        float roomUseTime = Random.Range(25f, 35f);
        float elapsedTime = 0f;

        if (currentRoomIndex < 0 || currentRoomIndex >= roomList.Count)
        {
            Debug.LogError($"AI {gameObject.name}: 잘못된 룸 인덱스 {currentRoomIndex}.");
            StartCoroutine(ReportRoomVacancy());
            yield break;
        }

        var room = roomList[currentRoomIndex].gameObject.GetComponent<RoomContents>();
        if (roomManager != null && room != null)
        {
            roomManager.ReportRoomUsage(gameObject.name, room);
        }

        TransitionToState(AIState.UsingRoom);

        Debug.Log($"AI {gameObject.name}: 룸 {currentRoomIndex + 1}번 사용 시작.");
        while (elapsedTime < roomUseTime && agent.isOnNavMesh)
        {
            yield return new WaitForSeconds(Random.Range(2f, 5f));
            elapsedTime += Random.Range(2f, 5f);
        }

        Debug.Log($"AI {gameObject.name}: 룸 {currentRoomIndex + 1}번 사용 완료.");
        DetermineBehaviorByTime();
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

        if (timeSystem.CurrentHour >= 9 && timeSystem.CurrentHour < 11)
        {
            if (counterManager != null && counterPosition != null)
            {
                TransitionToState(AIState.Wandering);
                wanderingCoroutine = StartCoroutine(WanderingBehavior());
                yield return new WaitForSeconds(Random.Range(5f, 10f));
                TransitionToState(AIState.MovingToQueue);
            }
            else
            {
                TransitionToState(AIState.ReturningToSpawn);
            }
        }
        else
        {
            DetermineBehaviorByTime();
        }

        yield break;
    }
    #endregion

    #region 배회 동작
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

        DetermineBehaviorByTime();
    }

    private IEnumerator RoomWanderingBehavior()
    {
        if (currentRoomIndex < 0 || currentRoomIndex >= roomList.Count)
        {
            Debug.LogError($"AI {gameObject.name}: 잘못된 룸 인덱스 {currentRoomIndex}.");
            DetermineBehaviorByTime();
            yield break;
        }

        float wanderingTime = Random.Range(15f, 30f);
        float elapsedTime = 0f;

        while (currentState == AIState.RoomWandering && elapsedTime < wanderingTime && agent.isOnNavMesh)
        {
            Vector3 roomCenter = roomList[currentRoomIndex].transform.position;
            float roomSize = roomList[currentRoomIndex].size;
            if (TryGetValidPosition(roomCenter, roomSize, NavMesh.AllAreas, out Vector3 targetPos))
            {
                agent.SetDestination(targetPos);
            }

            float waitTime = Random.Range(2f, 5f);
            yield return new WaitForSeconds(waitTime);
            elapsedTime += waitTime;
        }

        DetermineBehaviorByTime();
    }

    private void WanderOnGround()
    {
        Vector3 randomPoint = transform.position + Random.insideUnitSphere * 10f;
        int groundMask = NavMesh.GetAreaFromName("Ground");
        if (groundMask == 0)
        {
            Debug.LogError($"AI {gameObject.name}: Ground NavMesh 영역 설정되지 않음.");
            return;
        }

        if (NavMesh.SamplePosition(randomPoint, out NavMeshHit hit, 10f, groundMask))
        {
            agent.SetDestination(hit.position);
        }
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
        if (roomWanderingCoroutine != null)
        {
            StopCoroutine(roomWanderingCoroutine);
            roomWanderingCoroutine = null;
        }
    }

    private void CleanupResources()
    {
        if (currentRoomIndex != -1)
        {
            lock (lockObject)
            {
                if (currentRoomIndex >= 0 && currentRoomIndex < roomList.Count)
                {
                    roomList[currentRoomIndex].isOccupied = false;
                    Debug.Log($"AI {gameObject.name} 정리: 룸 {currentRoomIndex + 1}번 반환.");
                }
                currentRoomIndex = -1;
            }
        }

        isBeingServed = false;
        isInQueue = false;
        isWaitingForService = false;

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
        lastBehaviorUpdateHour = -1;

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