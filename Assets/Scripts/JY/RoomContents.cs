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

        [Header("Furniture")]
        private List<FurnitureID> furnitureList = new List<FurnitureID>();

        public bool IsRoomUsed => isRoomUsed;
        public int TotalRoomPrice { get; private set; }
        public int TotalRoomReputation { get; private set; } // 방의 총 명성도 추가

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

            float roomHeight = 4f;
            Vector3 adjustedMin = roomBounds.min;
            Vector3 adjustedMax = roomBounds.max;

            float originalYMin = bounds.min.y;
            adjustedMin.y = originalYMin;
            adjustedMax.y = roomHeight;

            roomBounds.SetMinMax(adjustedMin, adjustedMax);

            UpdateRoomContents();
            Debug.Log($"방 {roomID}의 범위가 업데이트되었습니다. 중심: {bounds.center}, 크기: {bounds.size}");
        }

        public void UpdateRoomContents()
        {
            furnitureList.Clear();

            var allFurniture = GameObject.FindObjectsOfType<FurnitureID>();

            foreach (var furniture in allFurniture)
            {
                if (roomBounds.Contains(furniture.transform.position))
                {
                    furnitureList.Add(furniture);
                    Debug.Log($"방 {roomID}에서 가구 발견: {furniture.gameObject.name}, 위치: {furniture.transform.position}");
                }
            }

            CalculateTotalPriceAndReputation(); // 수정된 메서드 호출

            Debug.Log($"방 {roomID} 업데이트: 가구 {furnitureList.Count}개, 총 가격 {TotalRoomPrice}원, 총 명성도 {TotalRoomReputation}");
        }

        private void CalculateTotalPriceAndReputation()
        {
            TotalRoomPrice = 0;
            TotalRoomReputation = 0; // 명성도 초기화
            foreach (var furniture in furnitureList)
            {
                if (furniture != null && furniture.Data != null)
                {
                    TotalRoomPrice += furniture.Data.BasePrice;
                    TotalRoomReputation += furniture.Data.BaseReputation; // 명성도 합산
                    Debug.Log($"가구 처리: {furniture.gameObject.name}, 가격: {furniture.Data.BasePrice}원, 명성도: {furniture.Data.BaseReputation}");
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
            Gizmos.color = isRoomUsed ? Color.red : Color.yellow;
            Gizmos.DrawWireCube(roomBounds.center, roomBounds.size);
        }
    }
}