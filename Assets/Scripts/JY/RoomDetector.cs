using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace JY
{
    /// <summary>
    /// 방 자동 감지 시스템
    /// 건축된 벽, 문, 침대를 분석하여 방을 자동으로 인식하고 생성
    /// </summary>
    public class RoomDetector : MonoBehaviour
    {
        [Header("기본 컴포넌트")]
        [Tooltip("건축 시스템 참조")]
        [SerializeField] private PlacementSystem placementSystem;
        
        [Tooltip("그리드 시스템 참조")]
        [SerializeField] private Grid grid;
        
        [Header("디버그 설정")]
        [Tooltip("디버그 로그 표시 여부")]
        [SerializeField] private bool showDebugLogs = false;
        
        [Tooltip("중요한 이벤트만 로그 표시")]
        [SerializeField] private bool showImportantLogsOnly = true;
        
        [Tooltip("방 스캔 과정 로그 표시")]
        [SerializeField] private bool showScanLogs = false;
        
        [Header("방 인식 조건")]
        [Tooltip("방으로 인식하기 위한 최소 벽 개수")]
        [Range(1, 20)]
        [SerializeField] private int minWalls = 4;
        
        [Tooltip("방으로 인식하기 위한 최소 문 개수")]
        [Range(1, 10)]
        [SerializeField] private int minDoors = 1;
        
        [Tooltip("방으로 인식하기 위한 최소 침대 개수")]
        [Range(1, 10)]
        [SerializeField] private int minBeds = 1;
        
        [Header("스캔 설정")]
        [Tooltip("방 스캔 주기 (초)")]
        [Range(0.5f, 10f)]
        [SerializeField] private float scanInterval = 2f;
        
        [Tooltip("방 요소들의 레이어 마스크")]
        [SerializeField] private LayerMask roomElementsLayer;
        
        [Header("층별 감지 설정")]
        [Tooltip("층 구분을 위한 Y축 허용 오차")]
        [Range(0.1f, 5f)]
        [SerializeField] private float floorHeightTolerance = 1.5f;
        
        [Tooltip("바닥 감지 오프셋")]
        [Range(-2f, 0f)]
        [SerializeField] private float floorDetectionOffset = -0.5f;
        
        [Header("다층 건물 설정")]
        [Tooltip("현재 스캔할 층 번호")]
        [Range(1, 10)]
        [SerializeField] private int currentScanFloor = 1;
        
        [Tooltip("모든 층을 스캔할지 여부")]
        [SerializeField] private bool scanAllFloors = true;
        
        [Tooltip("건물의 최대 층수")]
        [Range(1, 20)]
        [SerializeField] private int maxFloors = 5;
        
        [Header("선베드 방 설정")]
        [Tooltip("선베드 방 인식 기능 활성화")]
        [SerializeField] private bool enableSunbedRooms = true;
        
        [Tooltip("선베드 방의 고정 가격 (원)")]
        [Range(0f, 10000f)]
        [SerializeField] private float sunbedRoomPrice = 100f;
        
        [Tooltip("선베드 방의 고정 명성도")]
        [Range(0f, 1000f)]
        [SerializeField] private float sunbedRoomReputation = 50f;
        
        [Header("현재 상태")]
        [Tooltip("현재 감지된 방의 개수")]
        [SerializeField] private int detectedRoomCount = 0;
        
        [Tooltip("현재 스캔 상태 정보")]
        [SerializeField] private string currentScanStatus = "초기화 중...";
        
        [Tooltip("마지막 스캔 시간")]
        [SerializeField] private string lastScanTime = "아직 스캔하지 않음";

        // 3D 그리드로 변경하여 층 구분 지원
        private Dictionary<Vector3Int, RoomCell> roomGrid = new Dictionary<Vector3Int, RoomCell>();
        private List<RoomInfo> detectedRooms = new List<RoomInfo>();
        private HashSet<string> existingRoomIds = new HashSet<string>();
        private bool isInitialized = false;

        public delegate void RoomUpdateHandler(GameObject[] rooms);
        public event RoomUpdateHandler OnRoomsUpdated;

        /// <summary>
        /// 방 셀 정보 클래스
        /// </summary>
        public class RoomCell
        {
            public bool isFloor;
            public bool isWall;
            public bool isDoor;
            public bool isBed;
            public bool isSunbed;
            public Vector3Int position;
            public List<GameObject> objects = new List<GameObject>();
            public float worldHeight; // 실제 월드 높이 저장
        }

        /// <summary>
        /// 방 정보 클래스
        /// </summary>
        public class RoomInfo
        {
            public List<Vector3Int> floorCells = new List<Vector3Int>();
            public List<GameObject> walls = new List<GameObject>();
            public List<GameObject> doors = new List<GameObject>();
            public List<GameObject> beds = new List<GameObject>();
            public List<GameObject> sunbeds = new List<GameObject>();
            public Bounds bounds;
            public Vector3 center;
            public string roomId;
            public GameObject gameObject;
            public bool isSunbedRoom = false;
            public float fixedPrice = 0f;
            public float fixedReputation = 0f;
            public int floorLevel;

            public bool isValid(int minWalls, int minDoors, int minBeds)
            {
                // Sunbed 방은 별도 검증 로직
                if (isSunbedRoom)
                {
                    return sunbeds.Count > 0;
                }
                
                return walls.Count >= minWalls && doors.Count >= minDoors && beds.Count >= minBeds;
            }
        }

        private void Start()
        {
            InitializeComponents();
            InitializeFloorSettings();
            if (isInitialized)
            {
                InvokeRepeating(nameof(ScanForRooms), 1f, scanInterval);
            }
        }
        
        /// <summary>
        /// 층 설정 초기화
        /// </summary>
        private void InitializeFloorSettings()
        {
            // 층 설정 초기화
            floorHeightTolerance = 1.5f;
            floorDetectionOffset = -0.5f;
            
            DebugLog($"층 설정 초기화 완료 - 허용 오차: {floorHeightTolerance}, 바닥 오프셋: {floorDetectionOffset}", true);
        }

        /// <summary>
        /// 컴포넌트 초기화
        /// </summary>
        private void InitializeComponents()
        {
            placementSystem = placementSystem ?? FindObjectOfType<PlacementSystem>();
            grid = grid ?? FindObjectOfType<Grid>();

            if (placementSystem == null || grid == null)
            {
                DebugLog("필수 컴포넌트 누락!", true);
                isInitialized = false;
                currentScanStatus = "초기화 실패 - 필수 컴포넌트 누락";
                return;
            }

            isInitialized = true;
            currentScanStatus = "초기화 완료 - 스캔 대기 중";
            DebugLog("방 감지 시스템 초기화 완료", true);
        }
        
        /// <summary>
        /// 오브젝트가 현재 스캔 대상인지 확인 (층별 필터링)
        /// </summary>
        private bool ShouldProcessObject(GameObject obj)
        {
            if (scanAllFloors) return true;
            
            // 현재 층만 스캔하는 경우
            float objY = obj.transform.position.y;
            int objFloor = Mathf.FloorToInt(objY / 3f) + 1; // 3m당 1층으로 계산
            
            bool shouldProcess = objFloor == currentScanFloor;
            
            if (showScanLogs && shouldProcess)
            {
                DebugLog($"오브젝트 {obj.name} 스캔 대상 - Y: {objY:F2}, 층: {objFloor}, 현재 스캔 층: {currentScanFloor}");
            }
            
            return shouldProcess;
        }
        
        /// <summary>
        /// 스캔할 층 설정
        /// </summary>
        public void SetScanFloor(int floorLevel)
        {
            currentScanFloor = Mathf.Clamp(floorLevel, 1, maxFloors);
            scanAllFloors = false;
            DebugLog($"스캔 층 설정: {currentScanFloor}층", true);
        }
        
        /// <summary>
        /// 모든 층 스캔 모드 설정
        /// </summary>
        public void SetScanAllFloors()
        {
            scanAllFloors = true;
            DebugLog("모든 층 스캔 모드 활성화", true);
        }
        
        /// <summary>
        /// 현재 스캔 정보 반환
        /// </summary>
        public string GetScanInfo()
        {
            return $"감지된 방: {detectedRoomCount}개, 상태: {currentScanStatus}, 마지막 스캔: {lastScanTime}";
        }
        
        /// <summary>
        /// 방 스캔 실행
        /// </summary>
        public void ScanForRooms()
        {
            if (!isInitialized)
            {
                DebugLog("시스템이 초기화되지 않았습니다.", true);
                return;
            }

            currentScanStatus = "스캔 중...";
            lastScanTime = System.DateTime.Now.ToString("HH:mm:ss");
            
            DebugLog("방 스캔 시작", showScanLogs);

            try
            {
                // 그리드 업데이트
                UpdateGridFromScene();
                
                // 방 찾기
                List<RoomInfo> newRooms = new List<RoomInfo>();
                HashSet<Vector3Int> visitedCells = new HashSet<Vector3Int>();

                foreach (var kvp in roomGrid)
                {
                    Vector3Int pos = kvp.Key;
                    RoomCell cell = kvp.Value;

                    if (cell.isFloor && !visitedCells.Contains(pos))
                    {
                        RoomInfo room = FloodFillRoom(pos, visitedCells);
                        if (room != null && room.isValid(minWalls, minDoors, minBeds))
                        {
                            newRooms.Add(room);
                        }
                    }
                }

                // Sunbed 방 찾기
                if (enableSunbedRooms)
                {
                    List<RoomInfo> sunbedRooms = FindSunbedRooms(newRooms);
                    newRooms.AddRange(sunbedRooms);
                }

                // 방 업데이트
                UpdateRooms(newRooms);
                
                detectedRoomCount = detectedRooms.Count;
                currentScanStatus = $"스캔 완료 - {detectedRoomCount}개 방 감지";
                
                DebugLog($"방 스캔 완료: {detectedRoomCount}개 방 감지됨", true);
            }
            catch (System.Exception e)
            {
                currentScanStatus = "스캔 오류 발생";
                DebugLog($"방 스캔 중 오류 발생: {e.Message}", true);
            }
        }

        private void UpdateRooms(List<RoomInfo> newRooms)
        {
            foreach (var room in detectedRooms)
            {
                if (room.gameObject != null)
                {
                    GameObject.Destroy(room.gameObject);
                }
            }

            detectedRooms = newRooms;
            foreach (var room in detectedRooms)
            {
                CreateRoomGameObject(room);
            }

            if (detectedRooms.Count > 0)
            {
                DebugLog($"총 {detectedRooms.Count}개의 방이 감지됨");
                OnRoomsUpdated?.Invoke(detectedRooms.Select(r => r.gameObject).ToArray());
            }
            else
            {
                DebugLog("감지된 방이 없음");
            }
        }

        private void UpdateGridFromScene()
        {
            roomGrid.Clear();
            DebugLog($"그리드 업데이트 시작 - 스캔 모드: {(scanAllFloors ? "전체 층" : $"{currentScanFloor}층만")}");

            // 각 태그별로 오브젝트 검색 및 3D 위치 사용 (층별 필터링 적용)
            ProcessTaggedObjects("Floor", (obj) => {
                // 층별 필터링 적용
                if (!ShouldProcessObject(obj)) return;
                Vector3Int gridPosition = GetAdjustedGridPosition(obj, floorDetectionOffset);
                if (!roomGrid.ContainsKey(gridPosition))
                {
                    roomGrid[gridPosition] = new RoomCell { 
                        position = gridPosition, 
                        objects = new List<GameObject>(),
                        worldHeight = obj.transform.position.y + floorDetectionOffset
                    };
                }
                roomGrid[gridPosition].isFloor = true;
                roomGrid[gridPosition].objects.Add(obj.transform.parent?.gameObject ?? obj);
            });

            ProcessTaggedObjects("Wall", (obj) => {
                // 층별 필터링 적용
                if (!ShouldProcessObject(obj)) return;
                Vector3Int gridPosition = GetAdjustedGridPosition(obj, 0f);
                if (!roomGrid.ContainsKey(gridPosition))
                {
                    roomGrid[gridPosition] = new RoomCell { 
                        position = gridPosition, 
                        objects = new List<GameObject>(),
                        worldHeight = obj.transform.position.y
                    };
                }
                roomGrid[gridPosition].isWall = true;
                roomGrid[gridPosition].objects.Add(obj.transform.parent?.gameObject ?? obj);
            });

            ProcessTaggedObjects("Door", (obj) => {
                // 층별 필터링 적용
                if (!ShouldProcessObject(obj)) return;
                Vector3Int gridPosition = GetAdjustedGridPosition(obj, 0f);
                if (!roomGrid.ContainsKey(gridPosition))
                {
                    roomGrid[gridPosition] = new RoomCell { 
                        position = gridPosition, 
                        objects = new List<GameObject>(),
                        worldHeight = obj.transform.position.y
                    };
                }
                roomGrid[gridPosition].isDoor = true;
                roomGrid[gridPosition].objects.Add(obj.transform.parent?.gameObject ?? obj);
            });

            ProcessTaggedObjects("Bed", (obj) => {
                // 층별 필터링 적용
                if (!ShouldProcessObject(obj)) return;
                Vector3Int gridPosition = GetAdjustedGridPosition(obj, floorDetectionOffset);
                if (!roomGrid.ContainsKey(gridPosition))
                {
                    roomGrid[gridPosition] = new RoomCell { 
                        position = gridPosition, 
                        objects = new List<GameObject>(),
                        worldHeight = obj.transform.position.y + floorDetectionOffset
                    };
                }
                roomGrid[gridPosition].isBed = true;
                roomGrid[gridPosition].objects.Add(obj.transform.parent?.gameObject ?? obj);
            });

            // Sunbed 태그 처리 추가
            ProcessTaggedObjects("Sunbed", (obj) => {
                // 층별 필터링 적용
                if (!ShouldProcessObject(obj)) return;
                Vector3Int gridPosition = GetAdjustedGridPosition(obj, floorDetectionOffset);
                if (!roomGrid.ContainsKey(gridPosition))
                {
                    roomGrid[gridPosition] = new RoomCell { 
                        position = gridPosition, 
                        objects = new List<GameObject>(),
                        worldHeight = obj.transform.position.y + floorDetectionOffset
                    };
                }
                roomGrid[gridPosition].isSunbed = true;
                roomGrid[gridPosition].objects.Add(obj.transform.parent?.gameObject ?? obj);
            });

            DebugLog($"그리드 셀 총 개수: {roomGrid.Count}");
            DebugLog($"바닥 셀: {roomGrid.Values.Count(c => c.isFloor)}개");
            DebugLog($"벽 셀: {roomGrid.Values.Count(c => c.isWall)}개");
            DebugLog($"문 셀: {roomGrid.Values.Count(c => c.isDoor)}개");
            DebugLog($"침대 셀: {roomGrid.Values.Count(c => c.isBed)}개");
        }

        // 오프셋을 적용한 그리드 위치 계산
        private Vector3Int GetAdjustedGridPosition(GameObject obj, float yOffset)
        {
            Vector3 worldPosition = obj.transform.position;
            worldPosition.y += yOffset;
            Vector3Int gridPosition = grid.WorldToCell(worldPosition);
            
            DebugLog($"오브젝트 위치 변환: {obj.name}\n" +
                     $"Original Position: {obj.transform.position}\n" +
                     $"Adjusted Position: {worldPosition}\n" +
                     $"Grid Position: {gridPosition}");
                     
            return gridPosition;
        }

        private Vector3Int GetParentGridPosition(GameObject obj)
        {
            // 기존 메서드는 유지하되, 새로운 메서드 사용 권장
            return GetAdjustedGridPosition(obj, 0f);
        }

        private void ProcessTaggedObjects(string tag, System.Action<GameObject> processor)
        {
            var taggedObjects = GameObject.FindGameObjectsWithTag(tag);
            DebugLog($"{tag} 태그 오브젝트 수: {taggedObjects.Length}");
            
            foreach (var obj in taggedObjects)
            {
                if (obj == null) continue;
                processor(obj);
            }
        }

        private RoomInfo FloodFillRoom(Vector3Int startPos, HashSet<Vector3Int> visitedCells)
        {
            RoomInfo room = new RoomInfo();
            Queue<Vector3Int> queue = new Queue<Vector3Int>();
            queue.Enqueue(startPos);
            visitedCells.Add(startPos);

            Vector3Int minBounds = startPos;
            Vector3Int maxBounds = startPos;

            DebugLog($"방 탐색 시작 - 시작 위치: {startPos}");

            // 4방향 탐색
            Vector3Int[] directions = new Vector3Int[]
            {
                new Vector3Int(1, 0, 0),   // 오른쪽
                new Vector3Int(-1, 0, 0),  // 왼쪽
                new Vector3Int(0, 0, 1),   // 앞
                new Vector3Int(0, 0, -1)   // 뒤
            };

            HashSet<Vector3Int> roomCells = new HashSet<Vector3Int>();
            roomCells.Add(startPos);

            while (queue.Count > 0)
            {
                Vector3Int current = queue.Dequeue();

                if (!roomGrid.TryGetValue(current, out RoomCell currentCell))
                {
                    DebugLog($"셀 없음: {current}");
                    continue;
                }

                if (currentCell.isFloor)
                {
                    room.floorCells.Add(current);
                    minBounds = Vector3Int.Min(minBounds, current);
                    maxBounds = Vector3Int.Max(maxBounds, current);

                    // 주변 셀 검사
                    foreach (var dir in directions)
                    {
                        Vector3Int neighbor = current + dir;

                        // 이웃 셀이 문인지 확인
                        bool isDoorBetween = false;
                        if (roomGrid.TryGetValue(neighbor, out RoomCell neighborCell))
                        {
                            if (neighborCell.isDoor)
                            {
                                isDoorBetween = true;
                                foreach (var obj in neighborCell.objects)
                                {
                                    if (!room.doors.Contains(obj))
                                    {
                                        room.doors.Add(obj);
                                        DebugLog($"문 발견: {neighbor}");
                                    }
                                }
                            }
                        }

                        // 문이 있는 방향으로는 더 이상 진행하지 않음
                        if (!isDoorBetween)
                        {
                            // 벽 확인
                            if (roomGrid.TryGetValue(neighbor, out RoomCell wallCell) && wallCell.isWall)
                            {
                                foreach (var obj in wallCell.objects)
                                {
                                    if (!room.walls.Contains(obj))
                                    {
                                        room.walls.Add(obj);
                                        DebugLog($"벽 발견: {neighbor}");
                                    }
                                }
                            }

                            // 침대 확인
                            if (roomGrid.TryGetValue(neighbor, out RoomCell bedCell) && bedCell.isBed)
                            {
                                foreach (var obj in bedCell.objects)
                                {
                                    if (!room.beds.Contains(obj))
                                    {
                                        room.beds.Add(obj);
                                        DebugLog($"침대 발견: {neighbor}");
                                    }
                                }
                            }

                            // Sunbed 확인 (일반 방에서도 수집)
                            if (roomGrid.TryGetValue(neighbor, out RoomCell sunbedCell) && sunbedCell.isSunbed)
                            {
                                foreach (var obj in sunbedCell.objects)
                                {
                                    if (!room.sunbeds.Contains(obj))
                                    {
                                        room.sunbeds.Add(obj);
                                        DebugLog($"Sunbed 발견 (방 내부): {neighbor}");
                                    }
                                }
                            }

                            // 바닥이 있고 아직 방문하지 않은 경우에만 큐에 추가
                            if (roomGrid.TryGetValue(neighbor, out RoomCell floorCell) && 
                                floorCell.isFloor && 
                                !visitedCells.Contains(neighbor))
                            {
                                queue.Enqueue(neighbor);
                                visitedCells.Add(neighbor);
                                roomCells.Add(neighbor);
                                DebugLog($"다음 바닥 탐색: {neighbor}");
                            }
                        }
                    }
                }
            }

            if (room.floorCells.Count > 0)
            {
                Vector3 worldMin = grid.GetCellCenterWorld(minBounds);
                Vector3 worldMax = grid.GetCellCenterWorld(maxBounds);
                room.bounds = new Bounds();
                room.bounds.SetMinMax(worldMin, worldMax);
                room.center = room.bounds.center;
                room.roomId = $"Room_{room.center.x:F0}_{room.center.z:F0}";

                DebugLog($"방 감지 완료:\n" +
                         $"ID: {room.roomId}\n" +
                         $"중심점: {room.center}\n" +
                         $"바닥: {room.floorCells.Count}개\n" +
                         $"벽: {room.walls.Count}개\n" +
                         $"문: {room.doors.Count}개\n" +
                         $"침대: {room.beds.Count}개\n" +
                         $"유효성: {room.isValid(minWalls, minDoors, minBeds)}");

                return room;
            }

            DebugLog("유효한 방이 감지되지 않음");
            return null;
        }
        

        // 추가: 벽으로 둘러싸인 영역을 더 정확히 감지하는 헬퍼 메서드
        private bool IsEnclosedByWalls(Vector3Int position, HashSet<Vector3Int> roomCells)
        {
            Vector3Int[] directions = new Vector3Int[]
            {
                new Vector3Int(1, 0, 0), new Vector3Int(-1, 0, 0),
                new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1)
            };

            int wallCount = 0;
            int doorCount = 0;

            foreach (var dir in directions)
            {
                Vector3Int neighbor = position + dir;
                
                if (roomGrid.TryGetValue(neighbor, out RoomCell neighborCell))
                {
                    if (neighborCell.isWall) wallCount++;
                    if (neighborCell.isDoor) doorCount++;
                }
                else
                {
                    // 그리드 외부도 벽으로 간주
                    wallCount++;
                }
            }

            // 최소 3면이 벽으로 둘러싸여 있고, 문이 있으면 방으로 인정
            return wallCount >= 2 && (wallCount + doorCount) >= 3;
        }

        // 방 검증을 더 엄격하게 하는 메서드 추가
        private bool ValidateRoomStructure(RoomInfo room)
        {
            if (room.floorCells.Count == 0) return false;
            
            // 방의 최소 크기 확인 (예: 2x2 이상)
            int minX = room.floorCells.Min(c => c.x);
            int maxX = room.floorCells.Max(c => c.x);
            int minZ = room.floorCells.Min(c => c.z);
            int maxZ = room.floorCells.Max(c => c.z);
            
            int width = maxX - minX + 1;
            int height = maxZ - minZ + 1;
            
            // 최소 크기 체크
            if (width < 2 || height < 2) return false;
            
            // 벽으로 둘러싸인 정도 확인
            int enclosedCells = 0;
            foreach (var floorCell in room.floorCells)
            {
                if (IsEnclosedByWalls(floorCell, new HashSet<Vector3Int>(room.floorCells)))
                {
                    enclosedCells++;
                }
            }
            
            // 최소 50% 이상의 바닥이 벽으로 둘러싸여 있어야 함
            return (float)enclosedCells / room.floorCells.Count >= 0.5f;
        }

        private bool AreRoomListsEqual(List<RoomInfo> list1, List<RoomInfo> list2)
        {
            if (list1.Count != list2.Count)
                return false;

            var sortedList1 = list1.OrderBy(r => r.center.x).ThenBy(r => r.center.z).ToList();
            var sortedList2 = list2.OrderBy(r => r.center.x).ThenBy(r => r.center.z).ToList();

            for (int i = 0; i < sortedList1.Count; i++)
            {
                if (!AreRoomsEqual(sortedList1[i], sortedList2[i]))
                    return false;
            }
            return true;
        }

        private bool AreRoomsEqual(RoomInfo room1, RoomInfo room2)
        {
            if (room1.roomId != room2.roomId || room1.center != room2.center)
                return false;

            if (room1.walls.Count != room2.walls.Count ||
                room1.doors.Count != room2.doors.Count ||
                room1.beds.Count != room2.beds.Count ||
                room1.floorCells.Count != room2.floorCells.Count)
                return false;

            bool wallsEqual = room1.walls.All(w1 => room2.walls.Any(w2 => w2.GetInstanceID() == w1.GetInstanceID()));
            bool doorsEqual = room1.doors.All(d1 => room2.doors.Any(d2 => d2.GetInstanceID() == d1.GetInstanceID()));
            bool bedsEqual = room1.beds.All(b1 => room2.beds.Any(b2 => b2.GetInstanceID() == b1.GetInstanceID()));

            if (!wallsEqual || !doorsEqual || !bedsEqual)
                return false;

            var sortedFloors1 = room1.floorCells.OrderBy(v => v.x).ThenBy(v => v.z).ToList();
            var sortedFloors2 = room2.floorCells.OrderBy(v => v.x).ThenBy(v => v.z).ToList();
            
            for (int i = 0; i < sortedFloors1.Count; i++)
            {
                if (sortedFloors1[i] != sortedFloors2[i])
                    return false;
            }

            return true;
        }

        private void OnDrawGizmos()
        {
            if (!Application.isPlaying || !showDebugLogs || detectedRooms == null)
                return;

            Gizmos.color = Color.yellow;
            Vector3 gridCenter = transform.position;
            Vector3 gridSize = new Vector3(100f, 10f, 100f);
            Gizmos.DrawWireCube(gridCenter, gridSize);

            foreach (var room in detectedRooms)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireCube(room.bounds.center, room.bounds.size);

                foreach (var wall in room.walls)
                {
                    if (wall != null)
                    {
                        Gizmos.color = Color.blue;
                        Gizmos.DrawWireCube(wall.transform.position, Vector3.one);
                        Gizmos.DrawLine(wall.transform.position, room.bounds.center);
                    }
                }

                foreach (var door in room.doors)
                {
                    if (door != null)
                    {
                        Gizmos.color = Color.yellow;
                        Gizmos.DrawWireSphere(door.transform.position, 0.5f);
                        Gizmos.DrawLine(door.transform.position, room.bounds.center);
                    }
                }

                foreach (var bed in room.beds)
                {
                    if (bed != null)
                    {
                        Gizmos.color = Color.magenta;
                        Gizmos.DrawWireCube(bed.transform.position, new Vector3(1f, 0.5f, 2f));
                        Gizmos.DrawLine(bed.transform.position, room.bounds.center);
                    }
                }

                UnityEditor.Handles.Label(room.bounds.center, 
                    $"Room {room.roomId}\nWalls: {room.walls.Count}\n" +
                    $"Doors: {room.doors.Count}\nBeds: {room.beds.Count}\n" +
                    $"Valid: {room.isValid(minWalls, minDoors, minBeds)}");
            }
        }

        private void CreateRoomGameObject(RoomInfo room)
        {
            // 층 정보를 포함한 더 명확한 방 이름 생성
            string roomName = room.isSunbedRoom ? 
                $"SunbedRoom_F{room.floorLevel}_{room.center.x:F0}_{room.center.z:F0}" : 
                $"Room_F{room.floorLevel}_{room.center.x:F0}_{room.center.z:F0}";
                
            room.gameObject = new GameObject(roomName);
            room.gameObject.transform.position = room.center;
            room.gameObject.tag = "Room";

            // RoomManager를 통해 방 등록 (Sunbed 방 설정 포함)
            RoomManager roomManager = FindObjectOfType<RoomManager>();
            if (roomManager != null)
            {
                roomManager.RegisterRoomFromDetector(room, room.gameObject);
            }
            else
            {
                // RoomManager가 없는 경우 직접 RoomContents 추가
                var roomContents = room.gameObject.AddComponent<RoomContents>();
                roomContents.roomID = room.roomId;
                roomContents.SetRoomBounds(room.bounds);
                
                if (room.isSunbedRoom)
                {
                    roomContents.SetAsSunbedRoom(room.fixedPrice, room.fixedReputation);
                }
            }

            BoxCollider roomCollider = room.gameObject.AddComponent<BoxCollider>();
            roomCollider.center = new Vector3(0, 1.5f, 0);
            roomCollider.size = new Vector3(room.bounds.size.x, 3f, room.bounds.size.z);
            roomCollider.isTrigger = true;

            DebugLog($"방 생성: {room.roomId}\n" +
                     $"- 층: {room.floorLevel}층\n" +
                     $"- 위치: {room.center}\n" +
                     $"- 벽: {room.walls.Count}개\n" +
                     $"- 문: {room.doors.Count}개\n" +
                     $"- 침대: {room.beds.Count}개\n" +
                     $"- Sunbed: {room.sunbeds.Count}개\n" +
                     $"- 바닥: {room.floorCells.Count}개\n" +
                     $"- Sunbed 방: {room.isSunbedRoom}");
        }

        // Sunbed 방 찾기
        private List<RoomInfo> FindSunbedRooms(List<RoomInfo> existingRooms)
        {
            List<RoomInfo> sunbedRooms = new List<RoomInfo>();
            
            // 모든 sunbed 오브젝트 찾기
            var sunbedObjects = GameObject.FindGameObjectsWithTag("Sunbed");
            DebugLog($"Sunbed 태그 오브젝트 수: {sunbedObjects.Length}");
            
            foreach (var sunbedObj in sunbedObjects)
            {
                if (sunbedObj == null) continue;
                
                Vector3Int sunbedGridPos = GetParentGridPosition(sunbedObj);
                
                // 기존 방에 포함되어 있는지 확인
                bool isInsideExistingRoom = false;
                foreach (var existingRoom in existingRooms)
                {
                    if (IsPositionInsideRoom(sunbedGridPos, existingRoom))
                    {
                        isInsideExistingRoom = true;
                        DebugLog($"Sunbed {sunbedObj.name}은 기존 방 {existingRoom.roomId} 내부에 있음");
                        break;
                    }
                }
                
                // 기존 방에 포함되지 않은 경우에만 독립적인 방으로 생성
                if (!isInsideExistingRoom)
                {
                    RoomInfo sunbedRoom = CreateSunbedRoom(sunbedObj, sunbedGridPos);
                    if (sunbedRoom != null)
                    {
                        sunbedRooms.Add(sunbedRoom);
                        DebugLog($"독립적인 Sunbed 방 생성: {sunbedRoom.roomId}");
                    }
                }
            }
            
            return sunbedRooms;
        }

        // 위치가 방 내부에 있는지 확인
        private bool IsPositionInsideRoom(Vector3Int position, RoomInfo room)
        {
            // 방의 바닥 셀 중 하나와 일치하거나 인접한지 확인
            foreach (var floorCell in room.floorCells)
            {
                if (floorCell == position)
                {
                    return true;
                }
                
                // 인접 셀도 확인 (1칸 거리)
                float distance = Vector3Int.Distance(floorCell, position);
                if (distance <= 1.5f) // 대각선 포함
                {
                    return true;
                }
            }
            
            return false;
        }

        // Sunbed 방 생성
        private RoomInfo CreateSunbedRoom(GameObject sunbedObj, Vector3Int gridPos)
        {
            RoomInfo room = new RoomInfo();
            room.isSunbedRoom = true;
            room.fixedPrice = sunbedRoomPrice;
            room.fixedReputation = sunbedRoomReputation;
            
            // Sunbed를 중심으로 한 작은 영역 설정
            room.floorCells.Add(gridPos);
            room.sunbeds.Add(sunbedObj.transform.parent?.gameObject ?? sunbedObj);
            
            // 층 정보 계산
            if (roomGrid.TryGetValue(gridPos, out RoomCell sunbedCell))
            {
                room.floorLevel = Mathf.RoundToInt(sunbedCell.worldHeight / 3f); // 3m당 1층으로 가정
            }
            
            // 방 경계 설정 (sunbed 위치 기준)
            Vector3 worldPos = grid.GetCellCenterWorld(gridPos);
            room.bounds = new Bounds(worldPos, new Vector3(2f, 1f, 2f)); // 2x2 크기
            room.center = worldPos;
            
            string roomId = $"SunbedRoom_F{room.floorLevel}_{room.center.x:F0}_{room.center.z:F0}";
            room.roomId = roomId;
            
            DebugLog($"Sunbed 방 생성:\n" +
                     $"ID: {room.roomId}\n" +
                     $"위치: {room.center}\n" +
                     $"층: {room.floorLevel}\n" +
                     $"고정 가격: {room.fixedPrice}\n" +
                     $"고정 명성도: {room.fixedReputation}");
            
            return room;
        }

        /// <summary>
        /// 디버그 로그 출력
        /// </summary>
        private void DebugLog(string message, bool isImportant = false)
        {
            if (!showDebugLogs) return;
            
            if (showImportantLogsOnly && !isImportant) return;
            
            Debug.Log($"[RoomDetector] {message}");
        }
    }
} 