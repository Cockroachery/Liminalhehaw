using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
internal static class AutoSaveOnPlay
{
    static AutoSaveOnPlay()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.ExitingEditMode)
        {
            return;
        }

        // Save scene changes first, then any modified project assets.
        // If an unsaved scene's save dialog is cancelled, do not enter Play Mode.
        if (!EditorSceneManager.SaveOpenScenes())
        {
            EditorApplication.isPlaying = false;
            Debug.LogWarning("Play Mode cancelled because the open scenes could not be saved.");
            return;
        }

        AssetDatabase.SaveAssets();
        Debug.Log("Autosaved scenes and assets before entering Play Mode.");
    }
}
