using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace JY
{
    public class RoomContents : MonoBehaviour
    {
        [Header("Room Information")]
        public string roomID;
        
        [Header("Room Status")]
        [SerializeField] private bool isRoomUsed = false;
        
        [Header("Room Bounds")]
        public Bounds roomBounds;
        
        [Header("Sunbed Room Settings")]
        public bool isSunbedRoom = false; // Sunbed 방 여부
        public float fixedPrice = 0f; // 고정 가격
        public float fixedReputation = 0f; // 고정 명성도
        
        [Header("Furniture")]
        private List<FurnitureID> furnitureList = new List<FurnitureID>();
        
        public bool IsRoomUsed => isRoomUsed;
        public int TotalRoomPrice { get; private set; }
        public int TotalRoomReputation { get; private set; } // 방 총 명성도
        
        private void Start()
        {
            if (string.IsNullOrEmpty(roomID))
            {
                roomID = gameObject.name;
            }
            UpdateRoomContents();
        }
        
        public void SetRoomBounds(Bounds bounds)
        {
            roomBounds = bounds;

            // Y축 높이를 4로 조정
            float roomHeight = 4f; // 원하는 Y축 높이
            Vector3 adjustedMin = roomBounds.min;
            Vector3 adjustedMax = roomBounds.max;

            float originalYMin = bounds.min.y;
            adjustedMin.y = originalYMin; // 바닥 높이
            adjustedMax.y = roomHeight; // 천장 높이
            
            roomBounds.SetMinMax(adjustedMin, adjustedMax);

            UpdateRoomContents();
            Debug.Log($"방 {roomID}의 범위가 업데이트되었습니다. 중심: {bounds.center}, 크기: {bounds.size}");
        }

        // Sunbed 방 설정 메서드 추가
        public void SetAsSunbedRoom(float price, float reputation)
        {
            isSunbedRoom = true;
            fixedPrice = price;
            fixedReputation = reputation;
            
            // 고정값으로 설정
            TotalRoomPrice = Mathf.RoundToInt(fixedPrice);
            TotalRoomReputation = Mathf.RoundToInt(fixedReputation);
            
            Debug.Log($"Sunbed 방 {roomID} 설정: 고정 가격 {TotalRoomPrice}원, 고정 명성도 {TotalRoomReputation}");
        }
        
        public void UpdateRoomContents()
        {
            // Sunbed 방인 경우 고정값 사용
            if (isSunbedRoom)
            {
                TotalRoomPrice = Mathf.RoundToInt(fixedPrice);
                TotalRoomReputation = Mathf.RoundToInt(fixedReputation);
                Debug.Log($"Sunbed 방 {roomID} 업데이트: 고정 가격 {TotalRoomPrice}원, 고정 명성도 {TotalRoomReputation}");
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
                    Debug.Log($"방 {roomID}에서 가구 발견: {furniture.gameObject.name}, 위치: {furniture.transform.position}");
                }
            }
            
            // 총 가격 계산
            CalculateTotalPrice();
            
            // 총 명성도 계산
            CalculateTotalReputation();
            
            Debug.Log($"방 {roomID} 업데이트: 가구 {furnitureList.Count}개, 총 가격 {TotalRoomPrice}원, 총 명성도 {TotalRoomReputation}");
        }
        
        private void CalculateTotalPrice()
        {
            // Sunbed 방인 경우 고정값 사용
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
                    Debug.Log($"가구 가격 추가: {furniture.gameObject.name}, 가격: {furniture.Data.BasePrice}원");
                }
            }
        }
        
        /// <summary>
        /// 방 내 모든 가구의 명성도 합계를 계산합니다.
        /// </summary>
        private void CalculateTotalReputation()
        {
            // Sunbed 방인 경우 고정값 사용
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
                    Debug.Log($"가구 명성도 추가: {furniture.gameObject.name}, 명성도: {furniture.Data.ReputationValue}");
                }
            }
        }
        
        public int UseRoom()
        {
            if (isRoomUsed)
            {
                Debug.LogWarning($"방 {roomID}는 이미 사용 중입니다.");
                return 0;
            }
            
            isRoomUsed = true;
            return TotalRoomPrice;
        }
        
        public void ReleaseRoom()
        {
            isRoomUsed = false;
            Debug.Log($"방 {roomID} 사용 완료");
        }

        private void OnDrawGizmos()
        {
            // 방의 범위를 시각적으로 표시
            Gizmos.color = isRoomUsed ? Color.red : Color.yellow;
            
            // Sunbed 방은 다른 색상으로 표시
            if (isSunbedRoom)
            {
                Gizmos.color = isRoomUsed ? Color.magenta : Color.cyan;
            }
            
            Gizmos.DrawWireCube(roomBounds.center, roomBounds.size);
        }
    }
} 