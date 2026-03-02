using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public class GameMenu : MonoBehaviour
{
    [SerializeField] private GameObject firstSelected;

    public AK.Wwise.Event uiSelect;
    public AK.Wwise.Event startMusic;
    public AK.Wwise.Event stopMusic;

    private void OnEnable()
    {
        startMusic.Post(gameObject);
        if (EventSystem.current != null && firstSelected != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(firstSelected);
        }
    }

    public void StartGame()
    {
        uiSelect.Post(gameObject);
        stopMusic.Post(gameObject);
        SceneManager.LoadScene("Level");
    }

    public void QuitGame()
    {
        uiSelect.Post(gameObject);
        Debug.Log("Quit DustBunny Game"); 
        Application.Quit();
    }
}

