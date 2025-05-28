using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace JY
{
    public class RoomManager : MonoBehaviour
    {
        [Header("Room Management")]
        [Tooltip("모든 방 내용물 관리 컴포넌트")]
        public List<RoomContents> allRooms = new List<RoomContents>();

        [Tooltip("방 결제 시스템 참조")]
        public PaymentSystem paymentSystem;

        [Tooltip("명성도 시스템 참조")]
        public ReputationSystem reputationSystem;

        [Header("Room Settings")]
        [Tooltip("방을 찾을 때 사용할 태그")]
        public string roomTag = "Room";

        [Header("Pricing")]
        [Tooltip("오늘의 방 요금 배율")]
        public float priceMultiplier = 1.0f;

        [Header("Debug")]
        [Tooltip("디버그 로그 표시")]
        public bool showDebug = true;

        [Tooltip("사용된 방 정보")]
        [SerializeField] private List<string> usedRoomLogs = new List<string>();

        [Tooltip("결제 내역")]
        [SerializeField] private List<string> paymentLogs = new List<string>();

        private void Start()
        {
            FindAllRooms();

            if (reputationSystem == null)
            {
                reputationSystem = ReputationSystem.Instance;
                if (reputationSystem == null)
                {
                    reputationSystem = FindObjectOfType<ReputationSystem>();
                }
            }
        }

        public void FindAllRooms()
        {
            allRooms.Clear();

            GameObject[] roomObjects = GameObject.FindGameObjectsWithTag(roomTag);
            foreach (GameObject roomObj in roomObjects)
            {
                RoomContents roomContents = roomObj.GetComponent<RoomContents>();
                if (roomContents != null)
                {
                    allRooms.Add(roomContents);
                }
            }

            for (int i = 0; i < allRooms.Count; i++)
            {
                if (string.IsNullOrEmpty(allRooms[i].roomID))
                {
                    allRooms[i].roomID = (i + 101).ToString();
                }
            }

            if (showDebug)
            {
                Debug.Log($"총 {allRooms.Count}개의 방이 감지되었습니다.");
                if (allRooms.Count == 0)
                {
                    Debug.LogWarning($"'{roomTag}' 태그를 가진 방을 찾을 수 없습니다. 방 오브젝트에 태그가 설정되어 있는지 확인하세요.");
                }
            }
        }

        public void RegisterNewRoom(RoomContents room)
        {
            if (room != null && !allRooms.Contains(room))
            {
                allRooms.Add(room);
                if (string.IsNullOrEmpty(room.roomID))
                {
                    room.roomID = (allRooms.Count + 100).ToString();
                }
                if (showDebug)
                {
                    Debug.Log($"새로운 방 {room.roomID}이(가) 등록되었습니다.");
                }
            }
        }

        public void UnregisterRoom(RoomContents room)
        {
            if (room != null && allRooms.Contains(room))
            {
                allRooms.Remove(room);
                if (showDebug)
                {
                    Debug.Log($"방 {room.roomID}이(가) 제거되었습니다.");
                }
            }
        }

        public void ReportRoomUsage(string aiName, RoomContents room)
        {
            if (room == null) return;

            if (room.IsRoomUsed)
            {
                if (showDebug)
                    Debug.Log($"{aiName}가 이미 사용 중인 방 {room.roomID}에 접근했습니다.");
                return;
            }

            int finalPrice = Mathf.RoundToInt(room.UseRoom() * priceMultiplier);
            int finalReputation = room.TotalRoomReputation; // 방의 명성도 사용

            string usageLog = $"{aiName}이(가) 방 {room.roomID}을(를) 사용: {finalPrice}원, 명성도 {finalReputation}";
            usedRoomLogs.Add(usageLog);

            if (showDebug)
                Debug.Log(usageLog);

            if (paymentSystem != null)
            {
                paymentSystem.AddPayment(aiName, finalPrice, finalReputation, room.roomID); // 명성도 추가
            }
        }

        public int ProcessRoomPayment(string aiName)
        {
            if (paymentSystem == null) return 0;

            int amount = paymentSystem.ProcessPayment(aiName);

            string paymentLog = $"{aiName}의 방 사용 요금 결제: {amount}원";
            paymentLogs.Add(paymentLog);

            if (showDebug)
                Debug.Log(paymentLog);

            return amount;
        }

        public void UpdateRooms()
        {
            foreach (var room in allRooms)
            {
                room.UpdateRoomContents();
            }
        }

        public List<RoomContents> FindRoomsInPriceRange(int minPrice, int maxPrice)
        {
            return allRooms.Where(r => !r.IsRoomUsed && r.TotalRoomPrice >= minPrice && r.TotalRoomPrice <= maxPrice).ToList();
        }

        public List<RoomContents> GetAvailableRooms()
        {
            return allRooms.Where(r => !r.IsRoomUsed).ToList();
        }
    }
}