# JY 스크립트 사용법 메모장

## 📋 목차
1. [시간 시스템](#시간-시스템)
2. [AI 시스템](#ai-시스템)
3. [방 관리 시스템](#방-관리-시스템)
4. [배 시스템](#배-시스템)
5. [결제 및 명성도 시스템](#결제-및-명성도-시스템)
6. [유틸리티 시스템](#유틸리티-시스템)

---

## 🕐 시간 시스템

### TimeSystem.cs
**용도**: 게임 내 24시간 순환 시간 시스템 관리 (싱글톤)

**주요 기능**:
- 24시간 순환 시간 관리
- 시간 배속 조절 (기본: 1초당 1분)
- 하루 단계 구분 (아침/오후/저녁/밤)
- 시간 이벤트 시스템

**사용법**:
```csharp
// 시간 시스템 접근
TimeSystem timeSystem = TimeSystem.Instance;

// 현재 시간 정보
int hour = timeSystem.CurrentHour;
int minute = timeSystem.CurrentMinute;
string timeString = timeSystem.CurrentTimeString;

// 시간 설정
timeSystem.SetTime(12, 30); // 12시 30분으로 설정

// 시간 배속 조절
timeSystem.SetTimeMultiplier(3600f); // 1초당 1분 (기본)
timeSystem.SetTimeMultiplier(360f);  // 1초당 10분 (빠름)

// 시간 일시정지/재개
timeSystem.PauseTime();
timeSystem.ResumeTime();

// 이벤트 구독
timeSystem.OnMinuteChanged += (hour, minute) => { /* 매분 실행 */ };
timeSystem.OnHourChanged += (hour, minute) => { /* 매시 실행 */ };
timeSystem.OnDayPhaseChanged += (phase) => { /* 하루 단계 변경 시 */ };
```

**인스펙터 설정**:
- `timeMultiplier`: 시간 흐름 속도 (기본: 60)
- `startingHour`: 게임 시작 시간 (기본: 6시)
- `useTimeEvents`: 특정 시간 이벤트 사용 여부
- `eventTimes`: 이벤트 발생 시간 목록

### TimeManager.cs
**용도**: TimeSystem의 UI 표시 및 시간 제어 인터페이스

**주요 기능**:
- 시간 UI 업데이트 (시계, 하루 단계 표시)
- 시간 제어 버튼 기능
- 하루 단계별 색상 변경

**사용법**:
```csharp
// UI 컴포넌트에 연결하여 사용
TimeManager timeManager = GetComponent<TimeManager>();

// 시간 제어 메서드들 (버튼에 연결 가능)
timeManager.SetMorningTime();  // 07:00
timeManager.SetNoonTime();     // 12:00
timeManager.SetEveningTime();  // 19:00
timeManager.SetNightTime();    // 23:00

timeManager.SetNormalSpeed();  // 기본 속도
timeManager.SetFastSpeed();    // 빠른 속도
timeManager.PauseTime();       // 일시정지
timeManager.ResumeTime();      // 재개
```

**인스펙터 설정**:
- `timeText`: 시간 표시 TextMeshPro
- `phaseText`: 하루 단계 표시 TextMeshPro
- 하루 단계별 색상 설정

### SunMoonController.cs
**용도**: 시간에 따른 해와 달의 위치 제어

**주요 기능**:
- 시간에 따른 해/달 자동 회전
- 빛 강도 자동 조절
- 렌즈플레어 효과 제어
- 부드러운 회전 애니메이션

**사용법**:
```csharp
SunMoonController controller = GetComponent<SunMoonController>();

// 회전 설정
controller.SetRotationRadius(50f);
controller.SetRotationCenter(Vector3.zero);
controller.SetSmoothRotation(true, 2f);

// 빛 제어
controller.SetAutoLightControl(true);
controller.SetSunLightIntensity(1.5f);
controller.SetMoonLightIntensity(0.5f);

// 즉시 위치 설정
controller.SetImmediatePosition(12, 0); // 정오로 설정
```

**인스펙터 설정**:
- `sunTransform`: 해 오브젝트 Transform
- `moonTransform`: 달 오브젝트 Transform
- `sunLight/moonLight`: 해/달 Light 컴포넌트
- `rotationRadius`: 회전 반지름
- `smoothRotation`: 부드러운 회전 여부

---

## 🤖 AI 시스템

### AISpawner.cs
**용도**: 시간 기반 AI 자동 스폰 시스템 (싱글톤)

**주요 기능**:
- 시간 기반 자동 AI 스폰 (11시~16시, 2시간 간격)
- 오브젝트 풀링으로 성능 최적화
- 스폰 개수 랜덤 조절

**사용법**:
```csharp
// AI 스포너 접근
AISpawner spawner = AISpawner.Instance;

// 수동 스폰
spawner.ManualSpawn(5); // 5명 스폰

// 스폰 설정 변경
spawner.SetSpawnSettings(1, 5, 11, 16, 2); // 최소1명, 최대5명, 11시~16시, 2시간간격

// 상태 확인
int activeCount = spawner.GetActiveAICount();
int pooledCount = spawner.GetPooledAICount();
float nextSpawnTime = spawner.GetNextSpawnTime();

// 모든 AI 풀로 반환
spawner.ReturnAllToPool();
```

**인스펙터 설정**:
- `aiPrefab`: 스폰할 AI 프리팹
- `poolSize`: 오브젝트 풀 크기 (기본: 200)
- `minSpawner/maxSpawner`: 스폰 개수 범위
- `startHour/endHour`: 스폰 시간 범위
- `spawnInterval`: 스폰 간격 (시간)

### AIAgent.cs
**용도**: AI의 행동 패턴 및 상태 관리

**주요 기능**:
- 시간별 행동 패턴 (11시~17시: 방 사용, 17시: 강제 디스폰)
- 대기열 시스템 연동
- 방 사용 및 결제 처리
- NavMesh 기반 이동

**주요 상태**:
- `Wandering`: 외부 배회
- `MovingToQueue`: 대기열로 이동
- `WaitingInQueue`: 대기열 대기
- `MovingToRoom`: 방으로 이동
- `UsingRoom`: 방 사용 중
- `ReportingRoom`: 방 사용 완료 보고

**사용법**:
```csharp
AIAgent agent = GetComponent<AIAgent>();

// AI 초기화
agent.InitializeAI();

// 대기열 위치 설정
agent.SetQueueDestination(queuePosition);

// 서비스 완료 처리
agent.OnServiceComplete();

// 스포너 참조 설정
agent.SetSpawner(spawnerReference);
```

### CounterManager.cs
**용도**: AI 대기열 및 카운터 서비스 관리

**주요 기능**:
- 통합 대기열 관리 (방 배정 + 방 사용완료 보고)
- 대기열 위치 자동 계산
- 서비스 처리 시간 관리

**사용법**:
```csharp
CounterManager counter = GetComponent<CounterManager>();

// 대기열 합류
bool success = counter.TryJoinQueue(aiAgent);

// 대기열 나가기
counter.LeaveQueue(aiAgent);

// 서비스 가능 확인
bool canReceive = counter.CanReceiveService(aiAgent);

// 서비스 시작
counter.StartService(aiAgent);

// 강제 대기열 정리 (17시 디스폰 시)
counter.ForceCleanupQueue();
```

**인스펙터 설정**:
- `queueSpacing`: AI 간격 (기본: 2f)
- `counterServiceDistance`: 카운터와 서비스 위치 거리
- `maxQueueLength`: 최대 대기열 길이
- `serviceTime`: 서비스 처리 시간

---

## 🏠 방 관리 시스템

### RoomManager.cs
**용도**: 방 관리 및 요금 청구 통합 처리

**주요 기능**:
- 방 자동 검색 및 등록
- 방 사용 요금 계산
- 결제 시스템 연동
- 명성도 시스템 연동

**사용법**:
```csharp
RoomManager roomManager = GetComponent<RoomManager>();

// 방 검색
roomManager.FindAllRooms();

// 새 방 등록
roomManager.RegisterNewRoom(roomContents);

// 방 사용 보고
roomManager.ReportRoomUsage("AI_01", roomContents);

// 결제 처리
int totalAmount = roomManager.ProcessRoomPayment("AI_01");

// 사용 가능한 방 찾기
List<RoomContents> availableRooms = roomManager.GetAvailableRooms();
List<RoomContents> priceRangeRooms = roomManager.FindRoomsInPriceRange(100, 500);
```

**인스펙터 설정**:
- `roomTag`: 방 태그 (기본: "Room")
- `priceMultiplier`: 오늘의 방 요금 배율
- `paymentSystem`: 결제 시스템 참조
- `reputationSystem`: 명성도 시스템 참조

### RoomContents.cs
**용도**: 개별 방의 내용물 및 가격 관리

**주요 기능**:
- 방 내 가구 자동 감지
- 가격 자동 계산
- Sunbed 방 특별 처리
- 방 사용 상태 관리

**사용법**:
```csharp
RoomContents room = GetComponent<RoomContents>();

// 방 사용
int price = room.UseRoom();

// 방 해제
room.ReleaseRoom();

// Sunbed 방 설정
room.SetAsSunbedRoom(fixedPrice, fixedReputation);

// 방 정보 확인
bool isUsed = room.IsRoomUsed;
int totalPrice = room.TotalRoomPrice;
int totalReputation = room.TotalRoomReputation;
```

### RoomDetector.cs
**용도**: 동적 방 감지 및 생성

**주요 기능**:
- 3D 그리드 기반 방 감지
- Flood-fill 알고리즘으로 방 경계 계산
- 실시간 방 업데이트
- 다층 건물 지원

**사용법**:
```csharp
RoomDetector detector = GetComponent<RoomDetector>();

// 방 스캔 시작
detector.ScanForRooms();

// 수동 업데이트
detector.UpdateRooms();

// 감지된 방 가져오기
GameObject[] detectedRooms = detector.GetDetectedRooms();

// 이벤트 구독
detector.OnRoomsUpdated += (rooms) => { /* 방 업데이트 시 처리 */ };
```

**인스펙터 설정**:
- `gridSize`: 그리드 크기
- `scanHeight`: 스캔 높이
- `updateInterval`: 업데이트 간격
- `minRoomSize`: 최소 방 크기

---

## 🚢 배 시스템

### ShipSystem.cs
**용도**: 배 시스템 메인 매니저

**주요 기능**:
- AI 스폰 시간과 연동된 배 스케줄 관리
- 배 스폰/정박/출발 자동 처리
- 오브젝트 풀링 지원

**사용법**:
```csharp
ShipSystem shipSystem = GetComponent<ShipSystem>();

// 루트 추가/제거
shipSystem.AddRoute(shipRoute);
shipSystem.RemoveRoute("route_01");

// 활성 배 정보
List<ShipController> activeShips = shipSystem.GetActiveShips();
ShipSchedule schedule = shipSystem.GetSchedule("route_01");
```

### ShipController.cs
**용도**: 개별 배의 이동 및 상태 제어

**주요 기능**:
- 웨이포인트 기반 이동
- 정박 및 대기 시스템
- 출발 루트 처리
- 자동 풀 반환

**사용법**:
```csharp
ShipController ship = GetComponent<ShipController>();

// 배 초기화
ship.Initialize(shipRoute, timeSystem);

// 상태 확인
ShipState currentState = ship.CurrentState;
bool isDocked = ship.IsDocked;

// 수동 제어
ship.StartJourney();
ship.ForceReturn();
```

### ShipRoute.cs
**용도**: 배 루트 정보 관리

**주요 기능**:
- 웨이포인트 경로 설정
- 출발 루트 설정
- 루트 유효성 검사

### ShipObjectPool.cs
**용도**: 배 오브젝트 풀링 관리

**주요 기능**:
- 배 오브젝트 풀 관리
- 자동 풀 확장
- 메모리 최적화

---

## 💰 결제 및 명성도 시스템

### PaymentSystem.cs
**용도**: AI 방 사용 요금 관리 및 결제 처리

**주요 기능**:
- 결제 대기열 관리
- 방 명성도 기반 명성도 지급
- 플레이어 소지금 증가

**사용법**:
```csharp
PaymentSystem payment = GetComponent<PaymentSystem>();

// 결제 정보 추가
payment.AddPayment("AI_01", 500, "Room_101");
payment.AddPayment("AI_01", 300, "Room_102", 10); // 명성도 포함

// 결제 처리
int totalAmount = payment.ProcessPayment("AI_01");

// 미결제 확인
bool hasUnpaid = payment.HasUnpaidPayments("AI_01");
int unpaidAmount = payment.GetTotalUnpaidAmount("AI_01");
List<string> unpaidRooms = payment.GetUnpaidRooms("AI_01");
```

### ReputationSystem.cs
**용도**: 플레이어 명성도 관리

**주요 기능**:
- 명성도 증가/감소
- 명성도 레벨 시스템
- 명성도 기록 관리

**사용법**:
```csharp
ReputationSystem reputation = ReputationSystem.Instance;

// 명성도 추가/차감
reputation.AddReputation(10, "방 사용 완료");
reputation.SubtractReputation(5, "서비스 불만");

// 명성도 정보
int currentReputation = reputation.CurrentReputation;
int currentLevel = reputation.CurrentLevel;
string levelName = reputation.GetLevelName();
```

### PlayerWallet.cs
**용도**: 플레이어 소지금 관리 (싱글톤)

**주요 기능**:
- 소지금 증가/차감
- 소지금 저장/로드

**사용법**:
```csharp
PlayerWallet wallet = PlayerWallet.Instance;

// 소지금 조작
wallet.AddMoney(1000);
bool success = wallet.SpendMoney(500);

// 소지금 확인
int currentMoney = wallet.CurrentMoney;
bool canAfford = wallet.CanAfford(300);
```

---

## 🔧 유틸리티 시스템

### AutoNavmeshBaker.cs
**용도**: NavMesh 자동 생성 및 업데이트

**주요 기능**:
- 태그 기반 NavMesh 자동 생성
- 실시간 NavMesh 업데이트
- 성능 최적화된 비동기 처리

**사용법**:
```csharp
AutoNavMeshBaker baker = GetComponent<AutoNavMeshBaker>();

// 수동 NavMesh 재생성
baker.RebuildNavMesh();

// 특정 태그 오브젝트 업데이트
baker.UpdateTaggedObjects("Ground");
```

**인스펙터 설정**:
- `_navsurface`: NavMeshSurface 참조
- `tagsToBake`: NavMesh 생성할 태그 목록
- `autoUpdate`: 자동 업데이트 여부
- `updateInterval`: 업데이트 간격

### FloorConstants.cs
**용도**: 다층 건물 시스템 상수 정의

**주요 기능**:
- 층간 높이 상수
- 층 레벨 계산 유틸리티
- 층별 Bounds 계산

**사용법**:
```csharp
// 층 레벨 계산
int floorLevel = FloorConstants.GetFloorLevel(yPosition);

// 층 기준 Y 좌표
float baseY = FloorConstants.GetFloorBaseY(floorLevel);

// 같은 층 확인
bool sameFloor = FloorConstants.IsSameFloor(y1, y2);

// 층별 Bounds 계산
Bounds floorBounds = FloorConstants.GetFloorBounds(floorLevel, originalBounds);
```

### FurnitureID.cs
**용도**: 가구 ID 열거형 정의

**주요 기능**:
- 가구 타입 분류
- 가구별 고유 ID 제공

---

## 📝 사용 시 주의사항

1. **싱글톤 패턴**: TimeSystem, AISpawner, ReputationSystem, PlayerWallet은 싱글톤이므로 Instance로 접근
2. **시간 시스템 의존성**: 대부분의 시스템이 TimeSystem에 의존하므로 TimeSystem을 먼저 초기화
3. **NavMesh 설정**: AI 이동을 위해 NavMesh가 올바르게 설정되어야 함
4. **태그 설정**: Room, Counter, Spawn 등의 태그가 올바르게 설정되어야 함
5. **이벤트 구독 해제**: OnDestroy에서 이벤트 구독을 해제하여 메모리 누수 방지

## 🔄 시스템 간 연동 흐름

1. **TimeSystem** → 시간 이벤트 발생
2. **AISpawner** → 시간 기반 AI 스폰
3. **AIAgent** → 대기열 진입, 방 사용
4. **CounterManager** → 대기열 관리, 서비스 제공
5. **RoomManager** → 방 사용 처리, 요금 계산
6. **PaymentSystem** → 결제 처리, 소지금 증가
7. **ReputationSystem** → 명성도 증가
8. **ShipSystem** → AI 스폰과 연동된 배 운항

이 메모장을 참고하여 각 시스템을 효율적으로 활용하시기 바랍니다! 