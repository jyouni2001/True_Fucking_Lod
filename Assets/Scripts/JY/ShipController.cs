using System.Collections;
using UnityEngine;

namespace JY
{
    /// <summary>
    /// 개별 배의 움직임과 상태 제어
    /// </summary>
    public class ShipController : MonoBehaviour
    {
        [Header("Ship Status")]
        [SerializeField] private ShipState currentState = ShipState.Inactive;
        [SerializeField] private string shipId;
        
        [Header("Movement")]
        [SerializeField] private float currentSpeed = 0f;
        [SerializeField] private int currentWaypointIndex = 0;
        [SerializeField] private float waypointReachDistance = 2f;
        [SerializeField] private bool hasCompletedRoute = false;
        
        // [Header("Visual Effects")] // 시각적 효과는 나중에 추가 가능
        // [SerializeField] private ParticleSystem wakeEffect; // 물보라 효과
        // [SerializeField] private AudioSource shipAudio; // 배 소리
        // [SerializeField] private Transform shipModel; // 배 모델
        
        // 시스템 참조
        private ShipRoute assignedRoute;
        private ShipSystem shipSystem;
        
        // 이동 관련
        private Vector3 targetPosition;
        private Quaternion targetRotation;
        private Coroutine movementCoroutine;
        
        // 상태
        public ShipState CurrentState => currentState;
        public string ShipId => shipId;
        public ShipRoute AssignedRoute => assignedRoute;
        public bool HasCompletedRoute => hasCompletedRoute;
        
        // 이벤트
        public System.Action<ShipController> OnWaypointReached;
        public System.Action<ShipController> OnDockingStarted;
        public System.Action<ShipController> OnDockingCompleted;
        public System.Action<ShipController> OnDepartureStarted;
        
        private void Awake()
        {
            shipId = System.Guid.NewGuid().ToString();
            // SetupComponents(); // 시각적 효과 컴포넌트 설정은 나중에 추가 가능
        }
        
        // 시각적 효과 컴포넌트 설정 (나중에 활성화 가능)
        /*
        private void SetupComponents()
        {
            // 오디오 소스 설정
            if (shipAudio == null)
            {
                shipAudio = GetComponent<AudioSource>();
                if (shipAudio == null)
                {
                    shipAudio = gameObject.AddComponent<AudioSource>();
                }
            }
            
            // 파티클 시스템 찾기
            if (wakeEffect == null)
            {
                wakeEffect = GetComponentInChildren<ParticleSystem>();
            }
            
            // 배 모델 찾기
            if (shipModel == null)
            {
                shipModel = transform.GetChild(0); // 첫 번째 자식을 모델로 가정
            }
        }
        */
        
        /// <summary>
        /// 배 초기화
        /// </summary>
        public void Initialize(ShipRoute route, ShipSystem system)
        {
            assignedRoute = route;
            shipSystem = system;
            currentState = ShipState.Inactive;
            currentWaypointIndex = 0;
            hasCompletedRoute = false;
            
            // 시작 위치로 이동 (첫 번째 웨이포인트 Way0)
            if (route.waypoints.Count > 0 && route.waypoints[0] != null)
            {
                Vector3 startPos = route.GetWaypointPosition(0);
                transform.position = new Vector3(startPos.x, 0.5f, startPos.z); // 바닥에서 약간 위로
                
                // 두 번째 웨이포인트 방향으로 회전
                if (route.waypoints.Count > 1 && route.waypoints[1] != null)
                {
                    Vector3 direction = (route.GetWaypointPosition(1) - startPos).normalized;
                    if (direction != Vector3.zero)
                    {
                        transform.rotation = Quaternion.LookRotation(direction);
                    }
                }
            }
            
            DebugLog($"배 초기화 완료: {route.routeId}, 시작 위치: Way0");
        }
        
        /// <summary>
        /// 여행 시작
        /// </summary>
        public void StartJourney()
        {
            if (assignedRoute == null || !assignedRoute.IsValid())
            {
                DebugLog("유효하지 않은 루트입니다.");
                return;
            }
            
            currentState = ShipState.Moving;
            currentWaypointIndex = 1; // Way0에서 시작하므로 다음은 Way1
            hasCompletedRoute = false;
            
            DebugLog("여행 시작 - Way1으로 이동");
            MoveToNextWaypoint();
        }
        
        /// <summary>
        /// 정박 시작 (자동 호출)
        /// </summary>
        public void StartDocking()
        {
            if (currentState != ShipState.Moving)
            {
                DebugLog($"정박 불가 - 현재 상태: {currentState}");
                return;
            }
            
            currentState = ShipState.Docking;
            
            // 정박지로 이동
            if (movementCoroutine != null)
            {
                StopCoroutine(movementCoroutine);
            }
            
            movementCoroutine = StartCoroutine(MoveToDockingPoint());
            
            OnDockingStarted?.Invoke(this);
            DebugLog("정박 시작");
        }
        
        /// <summary>
        /// 출발 시작
        /// </summary>
        public void StartDeparture()
        {
            if (currentState != ShipState.Docked) 
            {
                DebugLog($"출발 불가 - 현재 상태: {currentState}");
                return;
            }
            
            currentState = ShipState.Departing;
            
            // 출발 애니메이션 시작
            if (movementCoroutine != null)
            {
                StopCoroutine(movementCoroutine);
            }
            
            movementCoroutine = StartCoroutine(DepartureSequence());
            
            OnDepartureStarted?.Invoke(this);
            DebugLog("출발 시작");
        }
        
        /// <summary>
        /// 배 리셋 (풀로 반환 시)
        /// </summary>
        public void ResetShip()
        {
            // 모든 코루틴 중지
            if (movementCoroutine != null)
            {
                StopCoroutine(movementCoroutine);
                movementCoroutine = null;
            }
            
            // 상태 리셋
            currentState = ShipState.Inactive;
            currentWaypointIndex = 0;
            currentSpeed = 0f;
            hasCompletedRoute = false;
            
            // 효과 중지 (나중에 활성화 가능)
            // StopMovementEffects();
            
            // 참조 해제
            assignedRoute = null;
            shipSystem = null;
            
            DebugLog("배 리셋 완료");
        }
        
        private void MoveToNextWaypoint()
        {
            if (assignedRoute == null)
            {
                DebugLog("루트가 없습니다.");
                return;
            }
            
            if (currentWaypointIndex >= assignedRoute.waypoints.Count)
            {
                // 모든 웨이포인트 통과 완료 - 정박지점으로 이동
                hasCompletedRoute = true;
                DebugLog("모든 웨이포인트 통과 완료 - 정박지점으로 이동");
                
                // 자동으로 정박 시작
                StartDocking();
                return;
            }
            
            targetPosition = assignedRoute.GetWaypointPosition(currentWaypointIndex);
            targetPosition = new Vector3(targetPosition.x, 0.5f, targetPosition.z);
            
            // 다음 웨이포인트 방향으로 회전 설정
            Vector3 direction = (targetPosition - transform.position).normalized;
            if (direction != Vector3.zero)
            {
                targetRotation = Quaternion.LookRotation(direction);
            }
            else
            {
                targetRotation = transform.rotation;
            }
            
            DebugLog($"Way{currentWaypointIndex}로 이동 시작");
            
            // 이동 코루틴 시작
            if (movementCoroutine != null)
            {
                StopCoroutine(movementCoroutine);
            }
            movementCoroutine = StartCoroutine(MoveToPosition(targetPosition, targetRotation));
        }
        
        private IEnumerator MoveToPosition(Vector3 destination, Quaternion rotation)
        {
            Vector3 startPosition = transform.position;
            Quaternion startRotation = transform.rotation;
            
            float distance = Vector3.Distance(startPosition, destination);
            
            // 거리가 너무 가까우면 즉시 완료
            if (distance < 0.1f)
            {
                transform.position = destination;
                transform.rotation = rotation;
                OnWaypointReached?.Invoke(this);
                currentWaypointIndex++;
                DebugLog($"웨이포인트 {currentWaypointIndex - 1} 도달 (즉시)");
                
                if (currentState == ShipState.Moving)
                {
                    MoveToNextWaypoint();
                }
                yield break;
            }
            
            float journeyTime = distance / assignedRoute.movementSpeed;
            float elapsedTime = 0f;
            
            DebugLog($"이동 시작: 거리={distance:F1}, 예상시간={journeyTime:F1}초");
            
            while (elapsedTime < journeyTime)
            {
                elapsedTime += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsedTime / journeyTime);
                
                // 속도 곡선 적용
                float curveValue = assignedRoute.speedCurve.Evaluate(progress);
                
                // 위치 보간
                transform.position = Vector3.Lerp(startPosition, destination, progress);
                
                // 회전 보간
                transform.rotation = Quaternion.Slerp(startRotation, rotation, progress);
                
                // 현재 속도 계산
                currentSpeed = assignedRoute.movementSpeed * curveValue;
                
                yield return null;
            }
            
            // 최종 위치 설정
            transform.position = destination;
            transform.rotation = rotation;
            
            // 웨이포인트 도달 처리
            OnWaypointReached?.Invoke(this);
            currentWaypointIndex++;
            
            DebugLog($"웨이포인트 {currentWaypointIndex - 1} 도달");
            
            // 다음 웨이포인트로 이동
            if (currentState == ShipState.Moving)
            {
                MoveToNextWaypoint();
            }
        }
        
        private IEnumerator MoveToDockingPoint()
        {
            Vector3 dockPosition = assignedRoute.GetDockingPosition();
            dockPosition = new Vector3(dockPosition.x, 0.5f, dockPosition.z);
            Quaternion dockRotation = assignedRoute.GetDockingRotation();
            
            DebugLog("정박지점으로 이동 중");
            
            // 정박 지점으로 부드럽게 이동
            yield return StartCoroutine(MoveToPosition(dockPosition, dockRotation));
            
            // 정박 완료
            currentState = ShipState.Docked;
            OnDockingCompleted?.Invoke(this);
            DebugLog("정박 완료 - 대기 시작");
            
            // 대기 (게임 시간)
            yield return StartCoroutine(WaitForDockingDuration());
            
            // 대기 후 자동 출발
            StartDeparture();
        }
        
        private IEnumerator WaitForDockingDuration()
        {
            float dockingTimeInMinutes = 30f; // 정식 30분
            float elapsedTimeInMinutes = 0f;
            
            DebugLog($"정박 대기 시작: {dockingTimeInMinutes}분 대기");
            
            // TimeSystem 참조 가져오기
            TimeSystem timeSystem = TimeSystem.Instance;
            if (timeSystem == null)
            {
                DebugLog("TimeSystem을 찾을 수 없습니다. 실제 시간으로 대기합니다.");
                yield return new WaitForSeconds(30f); // 30초로 대체
                DebugLog("정박 대기 완료 (실제 시간)");
                yield break;
            }
            
            float startTime = timeSystem.GetCurrentTimeInMinutes();
            float targetTime = startTime + dockingTimeInMinutes;
            
            DebugLog($"정박 시작 시간: {startTime}분, 목표 시간: {targetTime}분");
            
            while (currentState == ShipState.Docked)
            {
                float currentTime = timeSystem.GetCurrentTimeInMinutes();
                elapsedTimeInMinutes = currentTime - startTime;
                
                // 30분이 지났는지 확인
                if (currentTime >= targetTime)
                {
                    DebugLog($"정박 대기 완료: {elapsedTimeInMinutes:F1}분 경과");
                    break;
                }
                
                // 1초마다 상태 체크
                yield return new WaitForSeconds(1f);
            }
            
            DebugLog("정박 대기 종료");
        }
        
        private IEnumerator DepartureSequence()
        {
            DebugLog("출발 시퀀스 시작");
            
            // 출발 경로가 있는 경우
            if (assignedRoute.departureWaypoints != null && assignedRoute.departureWaypoints.Count > 0)
            {
                DebugLog($"출발 경로 따라 이동: {assignedRoute.departureWaypoints.Count}개 웨이포인트");
                
                // 출발 경로를 따라 이동
                for (int i = 0; i < assignedRoute.departureWaypoints.Count; i++)
                {
                    if (assignedRoute.departureWaypoints[i] == null)
                    {
                        DebugLog($"출발 웨이포인트 {i}가 null입니다. 건너뜁니다.");
                        continue;
                    }
                    
                    Vector3 nextWaypoint = assignedRoute.GetDepartureWaypointPosition(i);
                    nextWaypoint = new Vector3(nextWaypoint.x, 0.5f, nextWaypoint.z);
                    
                    Vector3 direction = (nextWaypoint - transform.position).normalized;
                    Quaternion nextRotation = direction != Vector3.zero ? 
                        Quaternion.LookRotation(direction) : transform.rotation;
                    
                    DebugLog($"출발 웨이포인트 DP{i}로 이동: {nextWaypoint}");
                    yield return StartCoroutine(MoveToPosition(nextWaypoint, nextRotation));
                    DebugLog($"출발 웨이포인트 DP{i} 도착 완료");
                }
            }
            else
            {
                DebugLog("출발 경로 없음 - 바다 방향으로 이동");
                
                // 출발 경로가 없는 경우 바다 방향으로 이동
                Vector3 finalPosition = transform.position + transform.forward * 50f;
                finalPosition = new Vector3(finalPosition.x, 0.5f, finalPosition.z);
                DebugLog($"바다 방향으로 이동: {finalPosition}");
                yield return StartCoroutine(MoveToPosition(finalPosition, transform.rotation));
            }
            
            // 출발 완료
            currentState = ShipState.Inactive;
            DebugLog("출발 완료 - 디스폰 준비");
        }
        
        // 시각적 효과 관련 메서드들 (나중에 활성화 가능)
        /*
        private void StartMovementEffects()
        {
            // 파티클 효과 시작
            if (wakeEffect != null)
            {
                wakeEffect.Play();
            }
            
            // 오디오 재생
            if (shipAudio != null && !shipAudio.isPlaying)
            {
                shipAudio.Play();
            }
        }
        
        private void StopMovementEffects()
        {
            // 파티클 효과 중지
            if (wakeEffect != null)
            {
                wakeEffect.Stop();
            }
            
            // 오디오 중지
            if (shipAudio != null && shipAudio.isPlaying)
            {
                shipAudio.Stop();
            }
        }
        */
        
        private void DebugLog(string message)
        {
            Debug.Log($"[ShipController] {shipId}: {message}");
        }
        
        private void OnDrawGizmos()
        {
            if (currentState == ShipState.Inactive) return;
            
            // 현재 타겟 위치 표시
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(targetPosition, 1f);
            
            // 현재 위치에서 타겟까지 선 그리기
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, targetPosition);
            
            // 배 상태 표시
            #if UNITY_EDITOR
            UnityEditor.Handles.Label(transform.position + Vector3.up * 3f, 
                $"{currentState}\nSpeed: {currentSpeed:F1}\nWP: {currentWaypointIndex}\nCompleted: {hasCompletedRoute}");
            #endif
        }
    }
    
    /// <summary>
    /// 배의 상태
    /// </summary>
    public enum ShipState
    {
        Inactive,   // 비활성
        Moving,     // 이동 중
        Docking,    // 정박 중
        Docked,     // 정박 완료
        Departing   // 출발 중
    }
} 