using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace JY
{
    /// <summary>
    /// 배 시스템 메인 매니저
    /// 시간 시스템과 연동하여 배의 스폰, 이동, 정박을 관리
    /// </summary>
    public class ShipSystem : MonoBehaviour
    {
        [Header("Ship System Settings")]
        [SerializeField] private bool enableShipSystem = true;
        [SerializeField] private GameObject shipPrefab;
        [SerializeField] private int maxShipCount = 5; // 최대 배 개수
        
        [Header("Timing Settings")]
        [SerializeField] private float spawnTimeBeforeArrival = 5f; // 도착 5분 전 스폰 (분)
        [SerializeField] private float dockingDuration = 30f; // 정박 시간 (분)
        
        [Header("Route Settings")]
        [SerializeField] private List<ShipRoute> shipRoutes = new List<ShipRoute>();
        
        [Header("Debug")]
        [SerializeField] private bool showDebugLogs = true;
        [SerializeField] private bool showGizmos = true;
        
        // 시스템 참조
        private TimeSystem timeSystem;
        private ShipObjectPool shipPool;
        private AISpawner aiSpawner;
        
        // 활성 배 관리
        private List<ShipController> activeShips = new List<ShipController>();
        private Dictionary<string, ShipSchedule> shipSchedules = new Dictionary<string, ShipSchedule>();
        
        // 이벤트
        public event Action<ShipController> OnShipSpawned;
        public event Action<ShipController> OnShipDocked;
        public event Action<ShipController> OnShipDeparted;
        
        private void Awake()
        {
            InitializeSystem();
        }
        
        private void Start()
        {
            SetupTimeSystemConnection();
            SetupAISpawnerConnection();
            GenerateShipSchedules();
        }
        
        private void Update()
        {
            if (!enableShipSystem) return;
            
            CheckShipSchedules();
            UpdateActiveShips();
        }
        
        private void InitializeSystem()
        {
            // 오브젝트 풀 초기화
            shipPool = GetComponent<ShipObjectPool>();
            if (shipPool == null)
            {
                shipPool = gameObject.AddComponent<ShipObjectPool>();
            }
            shipPool.Initialize(shipPrefab, maxShipCount);
            
            DebugLog("Ship System 초기화 완료");
        }
        
        private void SetupTimeSystemConnection()
        {
            // TimeSystem 찾기
            timeSystem = FindObjectOfType<TimeSystem>();
            if (timeSystem == null)
            {
                Debug.LogError("[ShipSystem] TimeSystem을 찾을 수 없습니다!");
                enableShipSystem = false;
                return;
            }
            
            DebugLog("TimeSystem 연결 완료");
        }

        private void SetupAISpawnerConnection()
        {
            // AISpawner 찾기
            aiSpawner = FindObjectOfType<AISpawner>();
            if (aiSpawner == null)
            {
                Debug.LogError("[ShipSystem] AISpawner를 찾을 수 없습니다!");
                enableShipSystem = false;
                return;
            }
            
            DebugLog("AISpawner 연결 완료");
        }
        
        private void GenerateShipSchedules()
        {
            if (shipRoutes.Count == 0)
            {
                DebugLog("설정된 배 루트가 없습니다.");
                return;
            }
            
            shipSchedules.Clear();
            
            // AI 스폰 시간에 맞춰 배 스케줄 생성
            foreach (var route in shipRoutes)
            {
                if (route.IsValid())
                {
                    // AI 스폰 시간에 맞춰 배 도착 시간 설정
                    float aiSpawnTime = aiSpawner.GetNextSpawnTime();
                    route.arrivalTime = aiSpawnTime;
                    
                    var schedule = new ShipSchedule(route);
                    shipSchedules[route.routeId] = schedule;
                    DebugLog($"배 스케줄 생성: {route.routeId} (도착 시간: {aiSpawnTime}분)");
                }
            }
            
            DebugLog($"총 {shipSchedules.Count}개의 배 스케줄 생성됨");
        }
        
        private void CheckShipSchedules()
        {
            if (timeSystem == null) return;
            
            float currentGameTime = timeSystem.GetCurrentTimeInMinutes();
            
            foreach (var schedule in shipSchedules.Values)
            {
                // 스폰 시간 체크
                if (!schedule.isShipSpawned && ShouldSpawnShip(schedule, currentGameTime))
                {
                    SpawnShip(schedule);
                }
                
                // 배가 스폰되었지만 아직 활성 상태인지 확인
                if (schedule.isShipSpawned && schedule.shipController != null)
                {
                    // 배가 비활성 상태가 되면 (출발 완료) 스케줄 리셋
                    if (schedule.shipController.CurrentState == ShipState.Inactive)
                    {
                        DebugLog($"배 출발 완료 감지 - 풀로 반환: {schedule.route.routeId}");
                        
                        // 활성 배 목록에서 제거
                        activeShips.Remove(schedule.shipController);
                        
                        // 풀로 반환
                        shipPool.ReturnShip(schedule.shipController.gameObject);
                        
                        // 스케줄 리셋 및 다음 AI 스폰 시간으로 업데이트
                        ResetAndUpdateSchedule(schedule);
                    }
                }
            }
        }
        
        private bool ShouldSpawnShip(ShipSchedule schedule, float currentTime)
        {
            float spawnTime = schedule.arrivalTime - spawnTimeBeforeArrival;
            return currentTime >= spawnTime && currentTime < schedule.arrivalTime;
        }
        
        private void SpawnShip(ShipSchedule schedule)
        {
            GameObject shipObj = shipPool.GetShip();
            if (shipObj == null)
            {
                DebugLog("사용 가능한 배가 없습니다.");
                return;
            }
            
            ShipController shipController = shipObj.GetComponent<ShipController>();
            if (shipController == null)
            {
                shipController = shipObj.AddComponent<ShipController>();
            }
            
            // 배 초기화
            shipController.Initialize(schedule.route, this);
            shipController.StartJourney();
            
            // 스케줄 업데이트
            schedule.isShipSpawned = true;
            schedule.shipController = shipController;
            
            // 활성 배 목록에 추가
            activeShips.Add(shipController);
            
            DebugLog($"배 스폰됨: {schedule.route.routeId}");
            OnShipSpawned?.Invoke(shipController);
        }
        
        private void ResetAndUpdateSchedule(ShipSchedule schedule)
        {
            // 스케줄 리셋
            schedule.Reset();
            
            // 다음 AI 스폰 시간 가져오기
            if (aiSpawner != null)
            {
                float nextAISpawnTime = aiSpawner.GetNextSpawnTime();
                schedule.route.arrivalTime = nextAISpawnTime;
                schedule.arrivalTime = nextAISpawnTime;
                
                DebugLog($"스케줄 업데이트: {schedule.route.routeId} - 다음 도착 시간: {nextAISpawnTime}분");
            }
            else
            {
                DebugLog($"AISpawner가 없어 스케줄을 업데이트할 수 없습니다: {schedule.route.routeId}");
            }
        }
        
        private void UpdateActiveShips()
        {
            for (int i = activeShips.Count - 1; i >= 0; i--)
            {
                if (activeShips[i] == null)
                {
                    activeShips.RemoveAt(i);
                }
            }
        }
        
        public void AddRoute(ShipRoute route)
        {
            if (route.IsValid())
            {
                shipRoutes.Add(route);
                var schedule = new ShipSchedule(route);
                shipSchedules[route.routeId] = schedule;
                DebugLog($"새 루트 추가: {route.routeId}");
            }
        }
        
        public void RemoveRoute(string routeId)
        {
            shipRoutes.RemoveAll(r => r.routeId == routeId);
            if (shipSchedules.ContainsKey(routeId))
            {
                shipSchedules.Remove(routeId);
                DebugLog($"루트 제거: {routeId}");
            }
        }
        
        public List<ShipController> GetActiveShips()
        {
            return new List<ShipController>(activeShips);
        }
        
        public ShipSchedule GetSchedule(string routeId)
        {
            return shipSchedules.ContainsKey(routeId) ? shipSchedules[routeId] : null;
        }
        
        private void DebugLog(string message)
        {
            if (showDebugLogs)
            {
                Debug.Log($"[ShipSystem] {message}");
            }
        }
        
        private void OnDrawGizmos()
        {
            if (!showGizmos || shipRoutes == null) return;
            
            foreach (var route in shipRoutes)
            {
                route.DrawGizmos();
            }
        }
        
        // 시스템 정리
        private void OnDestroy()
        {
            foreach (var ship in activeShips)
            {
                if (ship != null && ship.gameObject != null)
                {
                    shipPool.ReturnShip(ship.gameObject);
                }
            }
            activeShips.Clear();
        }
    }
    
    /// <summary>
    /// 배 스케줄 정보
    /// </summary>
    [System.Serializable]
    public class ShipSchedule
    {
        public ShipRoute route;
        public float arrivalTime; // 게임 시간 (분)
        public bool isShipSpawned;
        public ShipController shipController;
        
        public ShipSchedule(ShipRoute shipRoute)
        {
            route = shipRoute;
            arrivalTime = shipRoute.arrivalTime;
            Reset();
        }
        
        public void Reset()
        {
            isShipSpawned = false;
            shipController = null;
        }
    }
}
