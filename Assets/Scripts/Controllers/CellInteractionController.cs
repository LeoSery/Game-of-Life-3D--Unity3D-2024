using System.Collections;

using UnityEngine;

using Unity.Mathematics;

public class CellInteractionController : MonoBehaviour
{
    #region Public Fields
    [Header("Settings")]
    public float interactionDistance = 10f;
    public Color highlightColor = Color.yellow;

    [Header("Debug")]
    public bool showDebugRay = false;
    public Color debugRayColor = Color.magenta;
    public float debugRayDuration = 0.05f;
    #endregion

    #region Private Fields
    private Camera mainCamera;
    private Grid grid;
    private VisualGrid visualGrid;
    private Vector3 gridOffset;
    private Vector3Int? lastHighlightedCell;

    private int lastGridSize = -1;
    private Vector3 cachedRayOrigin = new(0.5f, 0.5f, 0);
    private float lastUpdateTime = 0f;
    private const float UPDATE_THROTTLE = 0.01f;
    #endregion

    #region Unity Lifecycle Methods

    private void Start()
    {
        mainCamera = Camera.main;

        if (mainCamera == null)
        {
            Debug.LogError("Main camera not found!");
            return;
        }

        StartCoroutine(WaitForGameManager());

        visualGrid = GameManager.Instance.visualGrid;

        GameManager.OnGridChanged += OnGridChanged;
    }

    private void OnDisable()
    {
        UnsubscribeToEvents();
        GameManager.OnGridChanged += OnGridChanged;
    }
    #endregion

    #region Public Methods
    public void ShowLayer()
    {
        visualGrid.ShowLayer();
        UpdateCellHighlight();
    }

    public void HideLayer()
    {
        visualGrid.HideLayer();
        UpdateCellHighlight();
    }

    public void UpdateCellHighlight()
    {
        float currentTime = Time.time;
        if (currentTime - lastUpdateTime < UPDATE_THROTTLE)
        {
            return;
        }

        lastUpdateTime = currentTime;

        bool shouldHideHighlight = visualGrid.isSiulationRunning && visualGrid.HideGridOnSimulate;
        if (shouldHideHighlight)
        {
            if (lastHighlightedCell.HasValue)
            {
                visualGrid.UnhighlightCell();
                lastHighlightedCell = null;
            }
            return;
        }

        UpdateGridOffset(true);

        Ray ray = mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));

        if (showDebugRay)
        {
            Debug.DrawRay(ray.origin, ray.direction * 100f, debugRayColor, debugRayDuration);
        }

        Vector3Int? targetCell = FindTargetCell(ray);

        if (targetCell.HasValue)
        {
            if (!targetCell.Equals(lastHighlightedCell))
            {
                visualGrid.UnhighlightCell();
                visualGrid.HighlightCell(targetCell.Value);
                lastHighlightedCell = targetCell;
            }
        }
        else if (lastHighlightedCell.HasValue)
        {
            visualGrid.UnhighlightCell();
            lastHighlightedCell = null;
        }
    }
    #endregion

    #region Private Methods
    private IEnumerator WaitForGameManager()
    {
        while (GameManager.Instance == null || GameManager.Instance.Grid == null || GameManager.Instance.visualGrid == null)
        {
            yield return new WaitForSeconds(0.1f);
        }

        grid = GameManager.Instance.Grid;
        visualGrid = GameManager.Instance.visualGrid;

        UpdateGridOffset();

        if (InputManager.Instance != null)
        {
            SubscribeToEvents();
        }
        else
        {
            Debug.LogError("InputManager instance is null. Make sure it's initialized before CellInteractionController.");
        }

        Debug.Log("CellInteractionController initialized with grid size: " + grid.GridSize);
    }

    private void UpdateGridOffset(bool forceUpdate = false)
    {
        if (GameManager.Instance == null)
        {
            Debug.LogWarning("UpdateGridOffset: GameManager.Instance is null");
            return;
        }

        int currentGridSize = GameManager.Instance.gridSize;

        if (forceUpdate || currentGridSize != lastGridSize)
        {
            lastGridSize = currentGridSize;
            float cellSize = GameManager.Instance.CellSize;

            // Calcul plus précis de l'offset pour centrer la grille
            gridOffset = new Vector3(
                currentGridSize / 2f * cellSize,
                currentGridSize / 2f * cellSize,
                currentGridSize / 2f * cellSize
            );

            if (showDebugRay)
            {
                Debug.Log($"Grid offset updated: {gridOffset}");
            }

            lastHighlightedCell = null;
        }
    }

    private void OnGridChanged(Grid newGrid, int newSize)
    {
        if (newGrid == null)
        {
            Debug.LogWarning("OnGridChanged called with null grid");
            return;
        }

        grid = newGrid;

        lastGridSize = -1;
        UpdateGridOffset();

        if (lastHighlightedCell.HasValue)
        {
            visualGrid.UnhighlightCell();
            lastHighlightedCell = null;
        }

        Debug.Log($"CellInteractionController: Grid reference updated to new size: {newSize}");
    }

    private void SubscribeToEvents()
    {
        if (InputManager.Instance != null)
        {
            InputManager.Instance.OnPlaceCell += PlaceCell;
            InputManager.Instance.OnRemoveCell += RemoveCell;

            InputManager.Instance.OnMove += HandleMovement;
            InputManager.Instance.OnMouseLook += HandleMouseLook;

            InputManager.Instance.OnShowLayer += ShowLayer;
            InputManager.Instance.OnHideLayer += HideLayer;
        }
    }

    private void UnsubscribeToEvents()
    {
        if (InputManager.Instance != null)
        {
            InputManager.Instance.OnPlaceCell -= PlaceCell;
            InputManager.Instance.OnRemoveCell -= RemoveCell;

            InputManager.Instance.OnMove -= HandleMovement;
            InputManager.Instance.OnMouseLook -= HandleMouseLook;

            InputManager.Instance.OnShowLayer -= ShowLayer;
            InputManager.Instance.OnHideLayer -= HideLayer;
        }
    }

    private Vector3Int? FindTargetCell(Ray _ray)
    {
        int gridSize = GameManager.Instance.gridSize;
        float cellSize = GameManager.Instance.CellSize;
        int currentVisibleLayer = visualGrid.CurrentVisibleLayer;

        Vector3 gridMin = Vector3.zero - gridOffset;
        Vector3 gridMax = new Vector3(gridSize * cellSize, gridSize * cellSize, gridSize * cellSize) - gridOffset;

        float planeY = currentVisibleLayer * cellSize - gridOffset.y;

        if (Mathf.Abs(_ray.direction.y) < 0.0001f)
        {
            return null;
        }

        float t = (planeY - _ray.origin.y) / _ray.direction.y;

        if (t < 0)
        {
            return null;
        }

        Vector3 hitPoint = _ray.origin + _ray.direction * t;

        if (showDebugRay)
        {
            Debug.DrawLine(_ray.origin, hitPoint, Color.yellow, debugRayDuration);

            float markerSize = 0.1f;
            Vector3 up = new Vector3(0, markerSize, 0);
            Vector3 right = new Vector3(markerSize, 0, 0);
            Vector3 forward = new Vector3(0, 0, markerSize);

            Debug.DrawLine(hitPoint - up, hitPoint + up, Color.red, debugRayDuration);
            Debug.DrawLine(hitPoint - right, hitPoint + right, Color.red, debugRayDuration);
            Debug.DrawLine(hitPoint - forward, hitPoint + forward, Color.red, debugRayDuration);
        }

        if (hitPoint.x >= gridMin.x && hitPoint.x < gridMax.x &&
            hitPoint.z >= gridMin.z && hitPoint.z < gridMax.z)
        {
            int3 cell = WorldToCellPosition(hitPoint);

            cell.y = currentVisibleLayer;

            if (IsValidCell(cell))
            {
                return new Vector3Int(cell.x, cell.y, cell.z);
            }
        }

        return null;
    }

    private int3 WorldToCellPosition(Vector3 _worldPosition)
    {
        Vector3 localPosition = _worldPosition + gridOffset;

        const float epsilon = 0.0001f;
        return new int3(
            Mathf.FloorToInt(localPosition.x + epsilon),
            Mathf.FloorToInt(localPosition.y + epsilon),
            Mathf.FloorToInt(localPosition.z + epsilon)
        );
    }

    private bool IsValidCell(int3 _cellPosition)
    {
        return _cellPosition.x >= 0 && _cellPosition.x < grid.GridSize &&
               _cellPosition.y >= 0 && _cellPosition.y < grid.GridSize &&
               _cellPosition.z >= 0 && _cellPosition.z < grid.GridSize;
    }

    private void HandleMovement(Vector3 _movement)
    {
        UpdateCellHighlight();
    }

    private void HandleMouseLook(Vector2 _mouseDelta)
    {
        UpdateCellHighlight();
    }

    private void PlaceCell()
    {
        if (lastHighlightedCell.HasValue)
        {
            Vector3Int cellPosition = lastHighlightedCell.Value;
            GameManager.Instance.CreateCell(new int3(cellPosition.x, cellPosition.y, cellPosition.z));
        }
    }

    private void RemoveCell()
    {
        if (lastHighlightedCell.HasValue)
        {
            Vector3Int cellPosition = lastHighlightedCell.Value;
            GameManager.Instance.DestroyCell(new int3(cellPosition.x, cellPosition.y, cellPosition.z));
        }
    }
    #endregion
}
