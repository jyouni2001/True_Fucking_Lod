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
            public int reputation; // 명성도 추가
            public string roomID;
            public bool isPaid;

            public PaymentInfo(string aiName, int amount, int reputation, string roomID)
            {
                this.aiName = aiName;
                this.amount = amount;
                this.reputation = reputation; // 명성도 초기화
                this.roomID = roomID;
                this.isPaid = false;
            }
        }

        [SerializeField] private List<PaymentInfo> paymentQueue = new List<PaymentInfo>();

        private ReputationSystem reputationSystem;

        void Start()
        {
            reputationSystem = ReputationSystem.Instance;
            if (reputationSystem == null)
            {
                reputationSystem = FindObjectOfType<ReputationSystem>();
            }
        }

        public void AddPayment(string aiName, int amount, int reputation, string roomID)
        {
            paymentQueue.Add(new PaymentInfo(aiName, amount, reputation, roomID));
            Debug.Log($"새로운 결제 등록: {aiName}, 방 {roomID}, {amount}원, 명성도 {reputation}");
        }

        public int ProcessPayment(string aiName)
        {
            int totalAmount = 0;
            int totalReputation = 0; // 총 명성도
            List<PaymentInfo> aiPayments = paymentQueue.FindAll(p => p.aiName == aiName && !p.isPaid);
            List<string> processedRooms = new List<string>();

            foreach (var payment in aiPayments)
            {
                totalAmount += payment.amount;
                totalReputation += payment.reputation; // 명성도 합산
                payment.isPaid = true;
                processedRooms.Add(payment.roomID);
                Debug.Log($"결제 처리: {payment.aiName}, 방 {payment.roomID}, {payment.amount}원, 명성도 {payment.reputation}");
            }

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

                if (reputationSystem != null)
                {
                    reputationSystem.AddReputation(aiName, totalReputation, processedRooms); // 수정된 메서드 호출
                }
            }

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

        public List<string> GetUnpaidRooms(string aiName)
        {
            return paymentQueue
                .Where(p => p.aiName == aiName && !p.isPaid)
                .Select(p => p.roomID)
                .ToList();
        }
    }
}