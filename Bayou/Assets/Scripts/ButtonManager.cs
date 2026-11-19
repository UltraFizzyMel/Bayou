using Bayou.Save;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ButtonManager : MonoBehaviour
{
    private GameObject _controlsButton;

    private void Start()
    {
        if (SceneManager.GetActiveScene().name != "MainMenu")
            return;

        var hasSave = System.IO.File.Exists(GameSaveSystem.SaveFilePath);
        WireResume("btnContinue", hasSave);
        WireResume("btnLoad", hasSave);
        WireResume("btnLoadGame", hasSave);
        var menuButton = GameObject.Find("btnContinue");
        var canvas = menuButton != null ? menuButton.GetComponentInParent<Canvas>() : null;
        if (canvas != null)
        {
            var control = new GameObject("Controls", typeof(RectTransform), typeof(Image), typeof(Button));
            _controlsButton = control;
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
            control.transform.SetParent(canvas.transform, false);
            var rect = control.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = Vector2.one;
            rect.pivot = Vector2.one;
            rect.anchoredPosition = new Vector2(-24f, -24f);
            rect.sizeDelta = new Vector2(150f, 46f);
            control.GetComponent<Image>().color = new Color(0.18f, 0.15f, 0.1f);
            control.GetComponent<Button>().onClick.AddListener(Bayou.UI.PauseMenu.OpenMainMenuControls);
            var label = new GameObject("Label", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
            label.transform.SetParent(control.transform, false);
            var text = label.GetComponent<TMPro.TextMeshProUGUI>();
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
            text.text = "Controls";
            text.fontSize = 24;
            text.alignment = TMPro.TextAlignmentOptions.Center;
            text.raycastTarget = false;
        }
    }

    private void OnActiveSceneChanged(Scene previous, Scene current)
    {
        if (current.name != "MainMenu") RemoveControlsButton();
    }

    private void OnDestroy() => RemoveControlsButton();

    private void RemoveControlsButton()
    {
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        if (_controlsButton != null)
            Destroy(_controlsButton);
        _controlsButton = null;
    }

    private void WireResume(string objectName, bool hasSave)
    {
        var go = GameObject.Find(objectName);
        if (go == null) return;
        var button = go.GetComponent<Button>();
        if (button == null) return;
        button.interactable = hasSave;
        if (button.onClick.GetPersistentEventCount() == 0)
            button.onClick.AddListener(objectName == "btnContinue" ? ContinueGame : LoadGame);
    }

    public void NewGame()
    {
        GameSaveSystem.SuppressNextLoad = true;
        SceneManager.LoadScene("TerrainTest");
    }

    public void ContinueGame()
    {
        GameSaveSystem.ResumeAtSceneSpawn = true;
        LoadGame();
    }

    public void LoadGame()
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
