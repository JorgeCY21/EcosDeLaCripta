using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace EcosDeLaCripta.UI
{
    /// <summary>Pantalla de inicio: botón Jugar dispara una carga asíncrona del Nivel 1 con pantalla de carga.</summary>
    public class MainMenuController : MonoBehaviour
    {
        [SerializeField] private GameObject menuPanel;
        [SerializeField] private GameObject loadingPanel;
        [SerializeField] private Slider loadingBar;
        [SerializeField] private Text loadingText;
        [SerializeField] private Button playButton;
        [SerializeField] private GameObject instructionsPanel;
        [SerializeField] private Button instructionsButton;
        [SerializeField] private Button instructionsBackButton;
        [SerializeField] private string levelSceneName = "Level1";

        public void Bind(GameObject menu, GameObject loading, Slider bar, Text text, Button play,
            GameObject instructions, Button instructionsOpen, Button instructionsBack, string sceneName)
        {
            menuPanel = menu;
            loadingPanel = loading;
            loadingBar = bar;
            loadingText = text;
            playButton = play;
            instructionsPanel = instructions;
            instructionsButton = instructionsOpen;
            instructionsBackButton = instructionsBack;
            levelSceneName = sceneName;
        }

        private void Start()
        {
            if (playButton != null) playButton.onClick.AddListener(OnPlayClicked);
            if (instructionsButton != null) instructionsButton.onClick.AddListener(ShowInstructions);
            if (instructionsBackButton != null) instructionsBackButton.onClick.AddListener(HideInstructions);
            if (loadingPanel != null) loadingPanel.SetActive(false);
            if (instructionsPanel != null) instructionsPanel.SetActive(false);
        }

        private void ShowInstructions()
        {
            if (menuPanel != null) menuPanel.SetActive(false);
            if (instructionsPanel != null) instructionsPanel.SetActive(true);
        }

        private void HideInstructions()
        {
            if (instructionsPanel != null) instructionsPanel.SetActive(false);
            if (menuPanel != null) menuPanel.SetActive(true);
        }

        private void OnPlayClicked()
        {
            if (playButton != null) playButton.interactable = false;
            if (menuPanel != null) menuPanel.SetActive(false);
            if (loadingPanel != null) loadingPanel.SetActive(true);
            StartCoroutine(LoadLevelAsync());
        }

        private IEnumerator LoadLevelAsync()
        {
            AsyncOperation op = SceneManager.LoadSceneAsync(levelSceneName);
            op.allowSceneActivation = false;

            while (op.progress < 0.9f)
            {
                float pct = Mathf.Clamp01(op.progress / 0.9f);
                if (loadingBar != null) loadingBar.value = pct;
                if (loadingText != null) loadingText.text = $"Cargando Fase 1... {Mathf.RoundToInt(pct * 100f)}%";
                yield return null;
            }

            if (loadingBar != null) loadingBar.value = 1f;
            if (loadingText != null) loadingText.text = "Cargando Fase 1... 100%";

            yield return new WaitForSeconds(0.35f);
            op.allowSceneActivation = true;
        }
    }
}
