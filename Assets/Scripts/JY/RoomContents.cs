using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace JY
{
    /// <summary>
    /// 방 내용물 관리 클래스
    /// 방 안의 가구들을 관리하고 가격, 명성도를 계산
    /// </summary>
    public class RoomContents : MonoBehaviour
    {
        [Header("방 정보")]
        [Tooltip("방 고유 ID")]
        public string roomID;
        
        [Header("방 상태")]
        [Tooltip("방 사용 중 여부")]
        [SerializeField] private bool isRoomUsed = false;
        
        [Header("방 범위")]
        [Tooltip("방의 3D 경계")]
        public Bounds roomBounds;
        
        [Header("선베드 방 설정")]
        [Tooltip("선베드 방 여부")]
        public bool isSunbedRoom = false;
        
        [Tooltip("고정 가격")]
        public float fixedPrice = 0f;
        
        [Tooltip("고정 명성도")]
        public float fixedReputation = 0f;
        
        [Header("디버그 설정")]
        [Tooltip("디버그 로그 표시 여부")]
        [SerializeField] private bool showDebugLogs = false;
        
        [Tooltip("중요한 이벤트만 로그 표시")]
        [SerializeField] private bool showImportantLogsOnly = true;
        
        [Tooltip("가구 스캔 과정 로그 표시")]
        [SerializeField] private bool showFurnitureLogs = false;
        
        [Header("가구 목록")]
        private List<FurnitureID> furnitureList = new List<FurnitureID>();
        
        // 공개 속성
        public bool IsRoomUsed => isRoomUsed;
        public int TotalRoomPrice { get; private set; }
        public int TotalRoomReputation { get; private set; }
        
        private void Start()
        {
            if (string.IsNullOrEmpty(roomID))
            {
                roomID = gameObject.name;
            }
            UpdateRoomContents();
            DebugLog("방 내용물 시스템 초기화 완료", true);
        }
        
        /// <summary>
        /// 방 범위 설정
        /// </summary>
        public void SetRoomBounds(Bounds bounds)
        {
            roomBounds = bounds;

            // Y축 높이를 4로 조정
            float roomHeight = 4f;
            Vector3 adjustedMin = roomBounds.min;
            Vector3 adjustedMax = roomBounds.max;

            float originalYMin = bounds.min.y;
            adjustedMin.y = originalYMin; // 바닥 높이
            adjustedMax.y = roomHeight; // 천장 높이
            
            roomBounds.SetMinMax(adjustedMin, adjustedMax);

            UpdateRoomContents();
            DebugLog($"방 {roomID}의 범위가 업데이트되었습니다. 중심: {bounds.center}, 크기: {bounds.size}", true);
        }

        /// <summary>
        /// 선베드 방으로 설정
        /// </summary>
        public void SetAsSunbedRoom(float price, float reputation)
        {
            isSunbedRoom = true;
            fixedPrice = price;
            fixedReputation = reputation;
            
            // 고정값으로 설정
            TotalRoomPrice = Mathf.RoundToInt(fixedPrice);
            TotalRoomReputation = Mathf.RoundToInt(fixedReputation);
            
            DebugLog($"선베드 방 {roomID} 설정: 고정 가격 {TotalRoomPrice}원, 고정 명성도 {TotalRoomReputation}", true);
        }
        
        /// <summary>
        /// 방 내용물 업데이트
        /// </summary>
        public void UpdateRoomContents()
        {
            // 선베드 방인 경우 고정값 사용
            if (isSunbedRoom)
            {
                TotalRoomPrice = Mathf.RoundToInt(fixedPrice);
                TotalRoomReputation = Mathf.RoundToInt(fixedReputation);
                DebugLog($"선베드 방 {roomID} 업데이트: 고정 가격 {TotalRoomPrice}원, 고정 명성도 {TotalRoomReputation}", showFurnitureLogs);
                return;
            }
            
            // 일반 방인 경우 기존 로직 사용
            furnitureList.Clear();
            
            // 씬의 모든 FurnitureID 컴포넌트 찾기
            var allFurniture = GameObject.FindObjectsOfType<FurnitureID>();
            
            // roomBounds 안에 있는 가구만 필터링
            foreach (var furniture in allFurniture)
            {
                if (roomBounds.Contains(furniture.transform.position))
                {
                    furnitureList.Add(furniture);
                    DebugLog($"방 {roomID}에서 가구 발견: {furniture.gameObject.name}, 위치: {furniture.transform.position}", showFurnitureLogs);
                }
            }
            
            // 총 가격 계산
            CalculateTotalPrice();
            
            // 총 명성도 계산
            CalculateTotalReputation();
            
            DebugLog($"방 {roomID} 업데이트: 가구 {furnitureList.Count}개, 총 가격 {TotalRoomPrice}원, 총 명성도 {TotalRoomReputation}", showImportantLogsOnly);
        }
        
        /// <summary>
        /// 총 가격 계산
        /// </summary>
        private void CalculateTotalPrice()
        {
            // 선베드 방인 경우 고정값 사용
            if (isSunbedRoom)
            {
                TotalRoomPrice = Mathf.RoundToInt(fixedPrice);
                return;
            }
            
            TotalRoomPrice = 0;
            foreach (var furniture in furnitureList)
            {
                if (furniture != null && furniture.Data != null)
                {
                    TotalRoomPrice += furniture.Data.BasePrice;
                    DebugLog($"가구 가격 추가: {furniture.gameObject.name}, 가격: {furniture.Data.BasePrice}원", showFurnitureLogs);
                }
            }
        }
        
        /// <summary>
        /// 방 내 모든 가구의 명성도 합계 계산
        /// </summary>
        private void CalculateTotalReputation()
        {
            // 선베드 방인 경우 고정값 사용
            if (isSunbedRoom)
            {
                TotalRoomReputation = Mathf.RoundToInt(fixedReputation);
                return;
            }
            
            TotalRoomReputation = 0;
            foreach (var furniture in furnitureList)
            {
                if (furniture != null && furniture.Data != null)
                {
                    TotalRoomReputation += furniture.Data.ReputationValue;
                    DebugLog($"가구 명성도 추가: {furniture.gameObject.name}, 명성도: {furniture.Data.ReputationValue}", showFurnitureLogs);
                }
            }
        }
        
        /// <summary>
        /// 방 사용 시작
        /// </summary>
        public int UseRoom()
        {
            if (isRoomUsed)
            {
                DebugLog($"방 {roomID}는 이미 사용 중입니다.", true);
                return 0;
            }
            
            isRoomUsed = true;
            DebugLog($"방 {roomID} 사용 시작", true);
            return TotalRoomPrice;
        }
        
        /// <summary>
        /// 방 사용 완료
        /// </summary>
        public void ReleaseRoom()
        {
            isRoomUsed = false;
            DebugLog($"방 {roomID} 사용 완료", true);
        }

        /// <summary>
        /// 기즈모 그리기 (방 범위 시각화)
        /// </summary>
        private void OnDrawGizmos()
        {
            // 방의 범위를 시각적으로 표시
            Gizmos.color = isRoomUsed ? Color.red : Color.yellow;
            
            // 선베드 방은 다른 색상으로 표시
            if (isSunbedRoom)
            {
                Gizmos.color = isRoomUsed ? Color.magenta : Color.cyan;
            }
            
            Gizmos.DrawWireCube(roomBounds.center, roomBounds.size);
        }
        
        #region 디버그 메서드
        
        /// <summary>
        /// 디버그 로그 출력
        /// </summary>
        private void DebugLog(string message, bool isImportant = false)
        {
            if (!showDebugLogs) return;
            
            if (showImportantLogsOnly && !isImportant) return;
            
            Debug.Log($"[RoomContents-{roomID}] {message}");
        }
        
        #endregion
    }
} 