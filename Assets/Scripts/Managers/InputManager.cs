using System.Collections.Generic;
using System;

using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine;

public class InputManager : MonoBehaviour
{
    #region Singleton
    public static InputManager Instance { get; private set; }
    #endregion

    #region Events
    public event Action<Vector2> OnMouseLook;
    public event Action<Vector3> OnMove;
    public event Action OnPlaceCell;
    public event Action OnRemoveCell;
    public event Action<bool> OnFocusChanged;
    public event Action OnShowLayer;
    public event Action OnHideLayer;
    #endregion

    #region Private Fields
    private bool ignoreNextMouseMovement = false;
    private bool isFocused = false;

    private GraphicRaycaster graphicRaycaster;
    private PointerEventData pointerEventData;
    private EventSystem eventSystem;
    #endregion

    #region Unity Lifecycle Methods
    private void Awake()
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

        graphicRaycaster = FindFirstObjectByType<Canvas>().GetComponent<GraphicRaycaster>();
        eventSystem = FindFirstObjectByType<EventSystem>();
    }

    private void Start()
    {
        SetFocus(false);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            ToggleFocus();
        }

        if (!isFocused && Input.GetMouseButtonDown(0))
        {
            CheckForUIClick();
        }

        if (isFocused)
        {
            if (!ignoreNextMouseMovement)
            {
                HandleMouseLook();
            }
            else
            {
                ignoreNextMouseMovement = false;
            }
            HandleMovement();
            HandleCellInteraction();
        }

        if (Input.GetKeyDown(KeyCode.LeftShift))
        {
            OnShowLayer?.Invoke();
        }
        else if (Input.GetKeyDown(KeyCode.LeftControl))
        {
            OnHideLayer?.Invoke();
        }
    }
    #endregion

    #region Public Methods
    public void ToggleFocus()
    {
        SetFocus(!isFocused);
    }

    public void SetFocus(bool _focused)
    {
        if (isFocused != _focused)
        {
            isFocused = _focused;

            if (isFocused)
            {
                ignoreNextMouseMovement = true;
                Cursor.visible = false;
                Cursor.lockState = CursorLockMode.Locked;
            }
            else
            {
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
            }

            OnFocusChanged?.Invoke(isFocused);
        }
    }
    #endregion

    #region Private Methods
    private void HandleMouseLook()
    {
        float mouseX = Input.GetAxis("Mouse X");
        float mouseY = Input.GetAxis("Mouse Y");
        OnMouseLook?.Invoke(new Vector2(mouseX, mouseY));
    }

    private void HandleMovement()
    {
        float moveHorizontal = Input.GetAxis("Horizontal");
        float moveVertical = Input.GetAxis("Vertical");
        OnMove?.Invoke(new Vector3(moveHorizontal, 0, moveVertical));
    }

    private void HandleCellInteraction()
    {
        if (Input.GetMouseButtonDown(1))
        {
            OnPlaceCell?.Invoke();
            Debug.Log("Place Cell event triggered");
        }
        else if (Input.GetMouseButtonDown(0))
        {
            OnRemoveCell?.Invoke();
            Debug.Log("Remove Cell event triggered");
        }
    }

    private void CheckForUIClick()
    {
        pointerEventData = new PointerEventData(eventSystem)
        {
            position = Input.mousePosition
        };

        List<RaycastResult> results = new();
        graphicRaycaster.Raycast(pointerEventData, results);

        if (results.Count == 0)
        {
            SetFocus(true);
        }
    }
    #endregion
}
