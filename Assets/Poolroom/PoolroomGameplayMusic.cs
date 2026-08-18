using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
public sealed class PoolroomGameplayMusic : MonoBehaviour
{
    [Header("Playlist")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioClip[] songs;

    [Header("Playback")]
    [SerializeField, Range(0f, 1f)] private float musicVolume = 0.55f;
    [SerializeField, Min(0f)] private float minimumSongGap = 3f;
    [SerializeField, Min(0f)] private float maximumSongGap = 5f;

    private bool hasStarted;

    private void Awake()
    {
        if (musicSource == null)
        {
            musicSource = GetComponent<AudioSource>();
        }
    }

    public void Configure(AudioSource source, AudioClip[] playlist)
    {
        musicSource = source;
        songs = playlist;
    }

    public void BeginPlaying()
    {
        if (hasStarted || musicSource == null || songs == null || songs.Length == 0)
        {
            return;
        }

        hasStarted = true;
        StartCoroutine(PlayPlaylist());
    }

    private IEnumerator PlayPlaylist()
    {
        AudioClip[] playOrder = (AudioClip[])songs.Clone();
        AudioClip lastPlayed = null;

        while (hasStarted)
        {
            ShuffleSongs(playOrder, lastPlayed);

            for (int songIndex = 0; songIndex < playOrder.Length && hasStarted; songIndex++)
            {
                AudioClip song = playOrder[songIndex];
                if (song == null)
                {
                    continue;
                }

                lastPlayed = song;
                musicSource.clip = song;
                musicSource.volume = musicVolume;
                musicSource.Play();

                while (musicSource.isPlaying && hasStarted)
                {
                    yield return null;
                }

                float gap = Random.Range(
                    Mathf.Min(minimumSongGap, maximumSongGap),
                    Mathf.Max(minimumSongGap, maximumSongGap));
                float waited = 0f;
                while (waited < gap && hasStarted)
                {
                    waited += Time.unscaledDeltaTime;
                    yield return null;
                }
            }
        }
    }

    private static void ShuffleSongs(AudioClip[] playOrder, AudioClip lastPlayed)
    {
        for (int index = playOrder.Length - 1; index > 0; index--)
        {
            int swapIndex = Random.Range(0, index + 1);
            (playOrder[index], playOrder[swapIndex]) = (playOrder[swapIndex], playOrder[index]);
        }

        if (playOrder.Length > 1 && playOrder[0] == lastPlayed)
        {
            int swapIndex = Random.Range(1, playOrder.Length);
            (playOrder[0], playOrder[swapIndex]) = (playOrder[swapIndex], playOrder[0]);
        }
    }
}
