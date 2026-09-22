using Bayou.Save;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ButtonManager : MonoBehaviour
{
    private void Start()
    {
        var hasSave = System.IO.File.Exists(GameSaveSystem.SaveFilePath);
        WireResume("btnContinue", hasSave);
        WireResume("btnLoad", hasSave);
    }

    private void WireResume(string objectName, bool hasSave)
    {
        var go = GameObject.Find(objectName);
        if (go == null) return;
        var button = go.GetComponent<Button>();
        if (button == null) return;
        button.interactable = hasSave;
        button.onClick.AddListener(ContinueGame);
    }

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
