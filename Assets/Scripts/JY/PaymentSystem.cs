using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace JY
{
    public class PaymentSystem : MonoBehaviour
    {
        [System.Serializable]
        private class PaymentInfo
        {
            public string aiName;
            public int amount;
            public string roomID;
            public int roomReputation;
            public bool isPaid;

            public PaymentInfo(string aiName, int amount, string roomID, int roomReputation = 0)
            {
                this.aiName = aiName;
                this.amount = amount;
                this.roomID = roomID;
                this.roomReputation = roomReputation;
                this.isPaid = false;
            }
        }

        [SerializeField] private List<PaymentInfo> paymentQueue = new List<PaymentInfo>();
        
        // 명성도 시스템 참조
        private ReputationSystem reputationSystem;
        
        void Start()
        {
            // 명성도 시스템 참조 찾기
            reputationSystem = ReputationSystem.Instance;
            if (reputationSystem == null)
            {
                reputationSystem = FindObjectOfType<ReputationSystem>();
            }
        }
        
        public void AddPayment(string aiName, int amount, string roomID)
        {
            paymentQueue.Add(new PaymentInfo(aiName, amount, roomID));
            Debug.Log($"새로운 결제 등록: {aiName}, 방 {roomID}, {amount}원");
        }
        
        /// <summary>
        /// 방 명성도를 포함한 결제 추가
        /// </summary>
        public void AddPayment(string aiName, int amount, string roomID, int roomReputation)
        {
            paymentQueue.Add(new PaymentInfo(aiName, amount, roomID, roomReputation));
            Debug.Log($"새로운 결제 등록: {aiName}, 방 {roomID}, {amount}원, 명성도 {roomReputation}");
        }
        
        public int ProcessPayment(string aiName)
        {
            int totalAmount = 0;
            List<PaymentInfo> aiPayments = paymentQueue.FindAll(p => p.aiName == aiName && !p.isPaid);
            
            foreach (var payment in aiPayments)
            {
                totalAmount += payment.amount;
                payment.isPaid = true;
                Debug.Log($"결제 처리: {payment.aiName}, 방 {payment.roomID}, {payment.amount}원, 명성도: {payment.roomReputation}");
            }
            
            // 결제된 금액을 플레이어 소지금에 추가
            if (totalAmount > 0)
            {
                var playerWallet = PlayerWallet.Instance;
                if (playerWallet != null)
                {
                    playerWallet.AddMoney(totalAmount);
                }
                else
                {
                    Debug.LogError("PlayerWallet을 찾을 수 없습니다.");
                }
                
                // ★ 명성도 증가 - 각 방의 명성도를 기반으로 명성도 증가
                if (reputationSystem != null)
                {
                    foreach (var payment in aiPayments)
                    {
                        // 방 명성도 기반으로 명성도 지급
                        reputationSystem.AddReputation(payment.aiName, payment.roomID, payment.roomReputation);
                    }
                }
            }
            
            // 처리된 결제 제거
            paymentQueue.RemoveAll(p => p.isPaid);
            
            return totalAmount;
        }
        
        public bool HasUnpaidPayments(string aiName)
        {
            return paymentQueue.Exists(p => p.aiName == aiName && !p.isPaid);
        }
        
        public int GetTotalUnpaidAmount(string aiName)
        {
            return paymentQueue
                .Where(p => p.aiName == aiName && !p.isPaid)
                .Sum(p => p.amount);
        }
        
        /// <summary>
        /// 특정 AI의 미결제 방 목록을 반환합니다.
        /// </summary>
        /// <param name="aiName">AI 이름</param>
        /// <returns>미결제 방 ID 목록</returns>
        public List<string> GetUnpaidRooms(string aiName)
        {
            return paymentQueue
                .Where(p => p.aiName == aiName && !p.isPaid)
                .Select(p => p.roomID)
                .ToList();
        }
    }
}