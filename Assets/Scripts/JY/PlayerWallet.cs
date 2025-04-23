using UnityEngine;

public class PlayerWallet : MonoBehaviour
{
    public static PlayerWallet Instance { get; private set; }
    
    [SerializeField] private int money = 0;

    public int Money => money;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void AddMoney(int amount)
    {
        money += amount;
        Debug.Log($"플레이어 소지금 증가: {amount}원, 현재 소지금: {money}원");
    }

    public void SpendMoney(int amount)
    {
        if (money >= amount)
        {
            money -= amount;
            Debug.Log($"플레이어 소지금 감소: {amount}원, 현재 소지금: {money}원");
        }
        else
        {
            Debug.LogWarning($"소지금 부족: 필요 {amount}원, 현재 {money}원");
        }
    }
}