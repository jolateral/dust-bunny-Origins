using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class GameMenu : MonoBehaviour
{
    [Header("Menu Selection")]
    [SerializeField] private GameObject firstSelected;

    [Header("Prompt Icon")]
    [SerializeField] private RectTransform playstationXIcon;
    [SerializeField] private Vector2 iconOffset = new Vector2(-169f, 0f);
    [SerializeField] private bool hideIconWhenNothingSelected = false;

    [Header("Controller Reselect")]
    [SerializeField] private bool forceControllerSelection = true;

    public AK.Wwise.Event uiSelect;
    public AK.Wwise.Event startMusic;
    public AK.Wwise.Event stopMusic;

    private GameObject lastSelected;
    private GameObject lastIconTarget;

    private void OnEnable()
    {
        startMusic.Post(gameObject);
        SelectObject(firstSelected);
        UpdateSelectionIcon(true);
    }

    private void Update()
    {
        MaintainControllerSelection();
        UpdateSelectionIcon(false);
    }

    private void MaintainControllerSelection()
    {
        if (!forceControllerSelection || EventSystem.current == null)
            return;

        GameObject current = EventSystem.current.currentSelectedGameObject;

        if (current != null)
            lastSelected = current;

        bool controllerUsed =
            Gamepad.current != null && (
                Gamepad.current.leftStick.ReadValue().sqrMagnitude > 0.01f ||
                Gamepad.current.dpad.ReadValue().sqrMagnitude > 0.01f ||
                Gamepad.current.buttonSouth.wasPressedThisFrame ||
                Gamepad.current.buttonNorth.wasPressedThisFrame ||
                Gamepad.current.buttonEast.wasPressedThisFrame ||
                Gamepad.current.buttonWest.wasPressedThisFrame
            );

        if (controllerUsed && EventSystem.current.currentSelectedGameObject == null)
        {
            if (lastSelected != null)
                SelectObject(lastSelected);
            else if (firstSelected != null)
                SelectObject(firstSelected);
        }

        if (EventSystem.current.currentSelectedGameObject == null && firstSelected != null)
        {
            if (lastSelected != null)
                SelectObject(lastSelected);
            else
                SelectObject(firstSelected);
        }
    }

    private void SelectObject(GameObject obj)
    {
        if (EventSystem.current == null || obj == null)
            return;

        EventSystem.current.SetSelectedGameObject(null);
        EventSystem.current.SetSelectedGameObject(obj);
        lastSelected = obj;
    }

    private void UpdateSelectionIcon(bool force)
    {
        if (EventSystem.current == null || playstationXIcon == null)
            return;

        GameObject currentSelected = EventSystem.current.currentSelectedGameObject;

        if (!force && currentSelected == lastIconTarget && playstationXIcon.gameObject.activeSelf)
            return;

        if (currentSelected == null)
        {
            lastIconTarget = null;

            if (hideIconWhenNothingSelected)
                playstationXIcon.gameObject.SetActive(false);

            return;
        }

        RectTransform selectedRect = currentSelected.GetComponent<RectTransform>();
        if (selectedRect == null)
        {
            lastIconTarget = null;

            if (hideIconWhenNothingSelected)
                playstationXIcon.gameObject.SetActive(false);

            return;
        }

        playstationXIcon.gameObject.SetActive(true);
        playstationXIcon.SetParent(selectedRect.parent, false);
        playstationXIcon.anchoredPosition = selectedRect.anchoredPosition + iconOffset;

        lastIconTarget = currentSelected;
    }

    public void StartGame()
    {
        uiSelect.Post(gameObject);
        stopMusic.Post(gameObject);
        FadeSequenceManager.Instance.FadeToScene("Level", 1.5f);
    }

    public void QuitGame()
    {
        uiSelect.Post(gameObject);
        Debug.Log("Quit DustBunny Game");
        Application.Quit();
    }
}