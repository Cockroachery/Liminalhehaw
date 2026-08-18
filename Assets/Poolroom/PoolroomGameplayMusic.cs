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
        int songIndex = 0;

        while (hasStarted)
        {
            AudioClip song = songs[songIndex];
            songIndex = (songIndex + 1) % songs.Length;

            if (song != null)
            {
                musicSource.clip = song;
                musicSource.volume = musicVolume;
                musicSource.Play();

                while (musicSource.isPlaying && hasStarted)
                {
                    yield return null;
                }
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
