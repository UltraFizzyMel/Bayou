using Bayou.Save;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ButtonManager : MonoBehaviour
{
    public void NewGame()
    {
        GameSaveSystem.SuppressNextLoad = true;
        SceneManager.LoadScene("TerrainTest");
    }

    public void ContinueGame()
    {
        GameSaveSystem.SuppressNextLoad = false;
        SceneManager.LoadScene("TerrainTest");
    }

    public void Quit()
    {
        #if UNITY_EDITOR
            // This stops Play Mode in the Unity Editor
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            // This closes the actual built application
            Application.Quit();
        #endif
    }
}
