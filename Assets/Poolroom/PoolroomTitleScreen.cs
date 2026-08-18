using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class PoolroomTitleScreen : MonoBehaviour
{
    [Header("Scene References")]
    [SerializeField] private Player player;
    [SerializeField] private CanvasGroup titleCanvas;
    [SerializeField] private Button playButton;
    [SerializeField] private Toggle drunkModeToggle;
    [SerializeField] private Volume blurVolume;

    [Header("Transition")]
    [SerializeField, Min(0f)] private float fadeDuration = 0.85f;

    private bool playStarted;

    private void Awake()
    {
        ResolveMissingReferences();

        if (playButton != null)
        {
            playButton.onClick.AddListener(Play);
        }

        ShowTitleScreen();
    }

    private void Start()
    {
        // Reassert the menu state after every object's Awake/OnEnable has run.
        ShowTitleScreen();
    }

    private void OnDestroy()
    {
        if (playButton != null)
        {
            playButton.onClick.RemoveListener(Play);
        }
    }

    public void Configure(
        Player targetPlayer,
        CanvasGroup canvas,
        Button button,
        Toggle drunkToggle,
        Volume titleBlur)
    {
        player = targetPlayer;
        titleCanvas = canvas;
        playButton = button;
        drunkModeToggle = drunkToggle;
        blurVolume = titleBlur;
    }

    public void Play()
    {
        if (playStarted)
        {
            return;
        }

        playStarted = true;
        if (playButton != null)
        {
            playButton.interactable = false;
        }

        StartCoroutine(FadeIntoGame());
    }

    private void ShowTitleScreen()
    {
        if (!Application.isPlaying || playStarted)
        {
            return;
        }

        if (titleCanvas != null)
        {
            titleCanvas.alpha = 1f;
            titleCanvas.interactable = true;
            titleCanvas.blocksRaycasts = true;
        }

        if (blurVolume != null)
        {
            blurVolume.gameObject.SetActive(true);
            blurVolume.weight = 1f;
        }

        player?.SetGameplayInputEnabled(false);
    }

    private IEnumerator FadeIntoGame()
    {
        float elapsed = 0f;
        float duration = Mathf.Max(fadeDuration, 0.01f);
        bool keepBlur = drunkModeToggle != null && drunkModeToggle.isOn;

        if (titleCanvas != null)
        {
            titleCanvas.interactable = false;
            titleCanvas.blocksRaycasts = false;
        }

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float amount = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));

            if (titleCanvas != null)
            {
                titleCanvas.alpha = 1f - amount;
            }

            if (blurVolume != null && !keepBlur)
            {
                blurVolume.weight = 1f - amount;
            }

            yield return null;
        }

        if (blurVolume != null)
        {
            blurVolume.weight = keepBlur ? 1f : 0f;
            blurVolume.gameObject.SetActive(keepBlur);
        }

        player?.SetGameplayInputEnabled(true);
        if (!keepBlur)
        {
            gameObject.SetActive(false);
        }
    }

    private void ResolveMissingReferences()
    {
        if (player == null)
        {
            player = FindAnyObjectByType<Player>();
        }

        if (titleCanvas == null)
        {
            titleCanvas = GetComponent<CanvasGroup>();
        }

        if (playButton == null)
        {
            playButton = GetComponentInChildren<Button>(true);
        }

        if (drunkModeToggle == null)
        {
            drunkModeToggle = GetComponentInChildren<Toggle>(true);
        }

        if (blurVolume == null)
        {
            blurVolume = GetComponentInChildren<Volume>(true);
        }
    }
}
