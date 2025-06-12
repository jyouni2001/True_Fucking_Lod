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
            
            // 시작 위치로 이동
            if (route.waypoints.Count > 0 && route.waypoints[0] != null)
            {
                transform.position = route.GetWaypointPosition(0);
                transform.rotation = Quaternion.LookRotation(
                    route.GetWaypointPosition(1) - route.GetWaypointPosition(0)
                );
            }
            
            DebugLog($"배 초기화 완료: {route.routeId}");
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
            currentWaypointIndex = 0;
            
            // 첫 번째 웨이포인트로 이동 시작
            MoveToNextWaypoint();
            
            // 효과 시작 (나중에 활성화 가능)
            // StartMovementEffects();
            
            DebugLog("여행 시작");
        }
        
        /// <summary>
        /// 정박 시작
        /// </summary>
        public void StartDocking()
        {
            if (currentState != ShipState.Moving) return;
            
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
            if (currentState != ShipState.Docked) return;
            
            currentState = ShipState.Departing;
            
            // 출발 애니메이션 시작
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
            
            // 효과 중지 (나중에 활성화 가능)
            // StopMovementEffects();
            
            // 참조 해제
            assignedRoute = null;
            shipSystem = null;
            
            DebugLog("배 리셋 완료");
        }
        
        private void MoveToNextWaypoint()
        {
            if (assignedRoute == null || currentWaypointIndex >= assignedRoute.waypoints.Count)
            {
                // 모든 웨이포인트 통과 완료
                DebugLog("모든 웨이포인트 통과 완료");
                return;
            }
            
            targetPosition = assignedRoute.GetWaypointPosition(currentWaypointIndex);
            
            // 다음 웨이포인트 방향으로 회전
            if (currentWaypointIndex < assignedRoute.waypoints.Count - 1)
            {
                Vector3 nextPos = assignedRoute.GetWaypointPosition(currentWaypointIndex + 1);
                Vector3 direction = (nextPos - targetPosition).normalized;
                targetRotation = Quaternion.LookRotation(direction);
            }
            
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
            float journeyTime = distance / assignedRoute.movementSpeed;
            float elapsedTime = 0f;
            
            while (elapsedTime < journeyTime)
            {
                elapsedTime += Time.deltaTime;
                float progress = elapsedTime / journeyTime;
                
                // 속도 곡선 적용
                float curveValue = assignedRoute.speedCurve.Evaluate(progress);
                
                // 위치 보간
                transform.position = Vector3.Lerp(startPosition, destination, curveValue);
                
                // 회전 보간
                transform.rotation = Quaternion.Slerp(startRotation, rotation, 
                    progress * assignedRoute.rotationSpeed);
                
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
            Quaternion dockRotation = assignedRoute.GetDockingRotation();
            
            // 정박 지점으로 부드럽게 이동
            yield return StartCoroutine(MoveToPosition(dockPosition, dockRotation));
            
            // 정박 완료
            currentState = ShipState.Docked;
            OnDockingCompleted?.Invoke(this);
            DebugLog("정박 완료");
        }
        
        private IEnumerator DepartureSequence()
        {
            // 출발 효과 시작
            currentState = ShipState.Departing;
            OnDepartureStarted?.Invoke(this);
            
            // 출발 방향으로 회전
            Vector3 departureDirection = -transform.forward;
            Quaternion departureRotation = Quaternion.LookRotation(departureDirection);
            
            // 회전
            float rotationTime = 2f;
            Quaternion startRotation = transform.rotation;
            float elapsedTime = 0f;
            
            while (elapsedTime < rotationTime)
            {
                elapsedTime += Time.deltaTime;
                float progress = elapsedTime / rotationTime;
                transform.rotation = Quaternion.Slerp(startRotation, departureRotation, progress);
                yield return null;
            }
            
            // 출발 경로의 첫 번째 웨이포인트로 이동
            if (assignedRoute.waypoints.Count > 0)
            {
                Vector3 firstWaypoint = assignedRoute.GetWaypointPosition(0);
                yield return StartCoroutine(MoveToPosition(firstWaypoint, departureRotation));
            }
            
            // 나머지 웨이포인트를 따라 이동
            currentWaypointIndex = 1;
            while (currentWaypointIndex < assignedRoute.waypoints.Count)
            {
                Vector3 nextWaypoint = assignedRoute.GetWaypointPosition(currentWaypointIndex);
                Vector3 direction = (nextWaypoint - transform.position).normalized;
                Quaternion nextRotation = Quaternion.LookRotation(direction);
                
                yield return StartCoroutine(MoveToPosition(nextWaypoint, nextRotation));
                currentWaypointIndex++;
            }
            
            // 마지막 웨이포인트에서 바다 방향으로 이동
            Vector3 finalPosition = transform.position + transform.forward * 100f;
            yield return StartCoroutine(MoveToPosition(finalPosition, transform.rotation));
            
            // 출발 완료
            currentState = ShipState.Inactive;
            DebugLog("출발 완료");
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
                $"{currentState}\nSpeed: {currentSpeed:F1}\nWP: {currentWaypointIndex}");
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