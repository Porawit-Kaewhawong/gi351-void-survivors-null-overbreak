using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LoreDialogueManager : MonoBehaviour
{
    public static LoreDialogueManager Instance { get; private set; }

    /// <summary>
    /// Global flag indicating whether dialogue is currently active.
    /// Check this in GameManager to prevent BGM from restarting during dialogue.
    /// </summary>
    public bool IsDialogueActive { get; private set; }

    [Header("UI Component References")]
    public GameObject dialoguePanel;
    public TMP_Text speakerNameText;
    public TMP_Text dialogueText;
    public GameObject continuePrompt;

    [Header("Typewriter Settings")]
    public float typingSpeed = 0.03f;

    [Header("SFX Settings")]
    public AudioClip typingSound;
    public AudioClip clickSound;
    [Range(0f, 1f)] public float sfxVolume = 1f;
    public float typingPitchVariation = 0.08f;
    [Tooltip("Optional custom AudioSource for dialogue SFX. If unassigned, one will be created automatically.")]
    public AudioSource dialogueAudioSource;

    [Header("Intro Lore Settings")]
    public string introSpeakerName = "The Unknown";
    [TextArea(2, 5)]
    public List<string> firstLaunchLoreLines = new List<string>()
    {
        "The cycle begins anew...",
        "You awaken in a place forgotten by time, bound to an endless struggle.",
        "Step forward, wanderer. Prove your worth."
    };
    public bool playIntroOnlyOnFirstLaunch = true;

    [Header("Death Lore / Quotes Pool")]
    public string deathSpeakerName = "Echoes of the Void";
    [TextArea(2, 5)]
    public List<string> deathLorePhrases = new List<string>()
    {
        "Death is not the end here... merely a reset of the balance.",
        "Your body collapses, but the labyrinth remembers your steps.",
        "Another attempt extinguished. Rise again.",
        "The darkness claims you once more.",
        "Your strength was insufficient this time, but knowledge remains."
    };

    private List<AudioSource> pausedAudioSources = new List<AudioSource>();
    private Queue<string> currentDialogueQueue = new Queue<string>();
    private Coroutine typingCoroutine;
    private bool isTyping = false;
    private string currentFullLine = "";
    private Action onDialogueCompleteCallback;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        SetupAudioSource();

        if (dialoguePanel != null)
        {
            dialoguePanel.SetActive(false);
        }
    }

    private void SetupAudioSource()
    {
        if (dialogueAudioSource == null)
        {
            dialogueAudioSource = gameObject.AddComponent<AudioSource>();
        }

        dialogueAudioSource.playOnAwake = false;
        dialogueAudioSource.loop = false;
        dialogueAudioSource.ignoreListenerPause = true;
    }

    private void Update()
    {
        if (dialoguePanel == null || !dialoguePanel.activeInHierarchy) return;

        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetMouseButtonDown(0))
        {
            OnPlayerAdvanceInput();
        }
    }

    private void SilenceAllGameAudio()
    {
        IsDialogueActive = true;
        pausedAudioSources.Clear();

        if (dialogueAudioSource == null) SetupAudioSource();

        AudioSource[] allSources = FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (AudioSource source in allSources)
        {
            if (source != null && source != dialogueAudioSource && source.isPlaying)
            {
                source.Pause();
                pausedAudioSources.Add(source);
            }
        }

        if (dialogueAudioSource != null)
        {
            dialogueAudioSource.UnPause();
        }
    }

    private void RestoreGameAudio()
    {
        foreach (AudioSource source in pausedAudioSources)
        {
            if (source != null)
            {
                source.UnPause();
            }
        }
        pausedAudioSources.Clear();
        IsDialogueActive = false;
    }

    public void StartIntroDialogue(Action onComplete)
    {
        bool hasPlayedIntro = PlayerPrefs.GetInt("HasSeenIntroLore", 0) == 1;

        if (playIntroOnlyOnFirstLaunch && hasPlayedIntro)
        {
            onComplete?.Invoke();
            return;
        }

        PlayerPrefs.SetInt("HasSeenIntroLore", 1);
        PlayerPrefs.Save();

        StartDialogueSequence(introSpeakerName, firstLaunchLoreLines, onComplete);
    }

    public void StartDeathDialogue(Action onComplete)
    {
        if (deathLorePhrases == null || deathLorePhrases.Count == 0)
        {
            onComplete?.Invoke();
            return;
        }

        string randomDeathLine = deathLorePhrases[UnityEngine.Random.Range(0, deathLorePhrases.Count)];
        List<string> lines = new List<string> { randomDeathLine };

        StartDialogueSequence(deathSpeakerName, lines, onComplete);
    }

    public void StartDialogueSequence(string speaker, List<string> lines, Action onComplete)
    {
        if (lines == null || lines.Count == 0)
        {
            onComplete?.Invoke();
            return;
        }

        SilenceAllGameAudio();

        onDialogueCompleteCallback = onComplete;
        currentDialogueQueue.Clear();

        foreach (string line in lines)
        {
            currentDialogueQueue.Enqueue(line);
        }

        if (speakerNameText != null)
        {
            speakerNameText.text = speaker;
        }

        if (dialoguePanel != null)
        {
            dialoguePanel.SetActive(true);
            dialoguePanel.transform.SetAsLastSibling(); // Brings dialogue box to the front of all UI panels
        }

        DisplayNextLine();
    }

    private void OnPlayerAdvanceInput()
    {
        PlayClickSFX();

        if (isTyping)
        {
            if (typingCoroutine != null) StopCoroutine(typingCoroutine);
            dialogueText.text = currentFullLine;
            isTyping = false;

            if (dialogueAudioSource != null) dialogueAudioSource.Stop();

            if (continuePrompt != null) continuePrompt.SetActive(true);
        }
        else
        {
            DisplayNextLine();
        }
    }

    private void DisplayNextLine()
    {
        if (currentDialogueQueue.Count > 0)
        {
            currentFullLine = currentDialogueQueue.Dequeue();
            if (typingCoroutine != null) StopCoroutine(typingCoroutine);
            typingCoroutine = StartCoroutine(TypeText(currentFullLine));
        }
        else
        {
            EndDialogue();
        }
    }

    private IEnumerator TypeText(string line)
    {
        isTyping = true;
        dialogueText.text = "";
        if (continuePrompt != null) continuePrompt.SetActive(false);

        foreach (char letter in line.ToCharArray())
        {
            dialogueText.text += letter;
            PlayTypingSFX(letter);
            yield return new WaitForSecondsRealtime(typingSpeed);
        }

        isTyping = false;
        if (continuePrompt != null) continuePrompt.SetActive(true);
    }

    private void PlayTypingSFX(char letter)
    {
        if (typingSound == null || dialogueAudioSource == null || char.IsWhiteSpace(letter)) return;

        dialogueAudioSource.pitch = 1f + UnityEngine.Random.Range(-typingPitchVariation, typingPitchVariation);
        dialogueAudioSource.PlayOneShot(typingSound, sfxVolume);
    }

    private void PlayClickSFX()
    {
        if (clickSound == null || dialogueAudioSource == null) return;

        dialogueAudioSource.pitch = 1f;
        dialogueAudioSource.PlayOneShot(clickSound, sfxVolume);
    }

    public void PlayDialogueSFX(AudioClip clip)
    {
        if (clip != null && dialogueAudioSource != null)
        {
            dialogueAudioSource.PlayOneShot(clip);
        }
    }

    private void EndDialogue()
    {
        if (typingCoroutine != null) StopCoroutine(typingCoroutine);

        if (dialogueAudioSource != null)
        {
            dialogueAudioSource.Stop();
        }

        if (dialoguePanel != null)
        {
            dialoguePanel.SetActive(false);
        }

        RestoreGameAudio();

        Action callback = onDialogueCompleteCallback;
        onDialogueCompleteCallback = null;
        callback?.Invoke();
    }
}