using UnityEngine;

public class ChangeFloorSystem : MonoBehaviour
{
    [SerializeField] private CameraCon cameraCon;
    [SerializeField] private Grid grid;
    [SerializeField] private PlacementSystem placementSystem;
    [SerializeField] private InputManager inputManager;
    public int currentFloor = 1;
    
    // 층 변경 메서드 (파라미터 없이 currentFloor 사용)
    private void ChangeFloor()
    {
        // 층 범위 제한
        currentFloor = Mathf.Clamp(currentFloor, 1, 4);

        Vector3 newCellSize = grid.cellSize;

        switch (currentFloor)
        {
            case 1:
                newCellSize.y = 0f;
                cameraCon.SetOffset(25);
                break;
            case 2:
                newCellSize.y = 1.927f;
                cameraCon.SetOffset(30);
                break;
            case 3:
                newCellSize.y = 3.854f; // 1.927 * 2 (예상값, 필요 시 조정)
                cameraCon.SetOffset(35);
                break;
            case 4:
                newCellSize.y = 5.781f; // 1.927 * 3 (예상값, 필요 시 조정)
                cameraCon.SetOffset(40);
                
                break;
        }

        OnBuildModeChanged();
        grid.cellSize = newCellSize;
    }

    // Up 버튼에서 호출
    public void IncreaseFloor()
    {
        currentFloor++;
        ChangeFloor();
    }

    // Down 버튼에서 호출
    public void DecreaseFloor()
    {
        currentFloor--;
        ChangeFloor();
    }

    private void CheckBuildMode()
    {
        if (inputManager.isBuildMode)
        {
            placementSystem.HidePlane(currentFloor); // 빌드 모드일 때 현재 층 표시
        }
        else
        {
            placementSystem.HideAllPlanes(); // 빌드 모드가 아닐 때 모든 플레인 비활성화
        }
    }

    // 빌드 모드 변경 시 호출 (InputManager에서 호출)
    public void OnBuildModeChanged()
    {
        CheckBuildMode();
    }
}
