using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public class GameMenu : MonoBehaviour
{
    [SerializeField] private GameObject firstSelected;

    public AK.Wwise.Event uiSelect;

    private void OnEnable()
    {
        if (EventSystem.current != null && firstSelected != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(firstSelected);
        }
    }

    public void StartGame()
    {
        uiSelect.Post(gameObject);
        SceneManager.LoadScene("Level");
    }

    public void QuitGame()
    {
        Debug.Log("Quit DustBunny Game"); 
        Application.Quit();
    }
}

