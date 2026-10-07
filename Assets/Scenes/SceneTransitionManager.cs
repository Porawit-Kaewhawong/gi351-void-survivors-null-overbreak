using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneTransitionManager : MonoBehaviour
{
    public static SceneTransitionManager Instance { get; private set; }

    [Header("UI References")]
    [Tooltip("Drag the Black Screen Image/Panel GameObject here.")]
    [SerializeField] private GameObject blackScreenPanel;

    [Header("Audio Settings")]
    [SerializeField] private AudioClip transitionSFX;

    [Header("Transition Settings")]
    [Tooltip("Time in seconds to hold the black image and play SFX before loading the scene.")]
    public float delayBeforeLoad = 0.5f;

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
            return;
        }

        // Turn off immediately on game start
        if (blackScreenPanel != null)
        {
            blackScreenPanel.SetActive(false);
        }
    }

    public void TransitionToScene(string sceneName)
    {
        StartCoroutine(ShowImagePlaySFXAndLoad(sceneName));
    }

    private IEnumerator ShowImagePlaySFXAndLoad(string sceneName)
    {
        // 1. Enable the black screen panel right when transition starts
        if (blackScreenPanel != null)
        {
            blackScreenPanel.SetActive(true);
        }

        // 2. Play the audio clip
        if (transitionSFX != null)
        {
            Vector3 soundPos = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
            AudioSource.PlayClipAtPoint(transitionSFX, soundPos);
        }

        // 3. Pause briefly so the SFX plays
        if (delayBeforeLoad > 0f)
        {
            yield return new WaitForSecondsRealtime(delayBeforeLoad);
        }

        // 4. Load the scene asynchronously
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);
        while (!asyncLoad.isDone)
        {
            yield return null;
        }

        // 5. Turn off the black screen panel after the scene finishes loading
        if (blackScreenPanel != null)
        {
            blackScreenPanel.SetActive(false);
        }
    }
}