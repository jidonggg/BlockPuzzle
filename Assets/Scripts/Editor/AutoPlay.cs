using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class AutoPlay
{
    static AutoPlay()
    {
        // Check for flag file to enter play mode
        string flagPath = System.IO.Path.Combine(Application.dataPath, "..", "autoplay.flag");
        if (System.IO.File.Exists(flagPath))
        {
            System.IO.File.Delete(flagPath);
            EditorApplication.update += OncePlay;
        }
    }

    private static void OncePlay()
    {
        EditorApplication.update -= OncePlay;
        if (!EditorApplication.isPlaying)
        {
            Debug.Log("[AutoPlay] Entering Play Mode...");
            EditorApplication.isPlaying = true;
        }
    }

    public static void EnterPlayMode()
    {
        EditorApplication.delayCall += () =>
        {
            EditorApplication.isPlaying = true;
        };
    }
}
