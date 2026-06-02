#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class FloatileAutoQAEditor
{
    const string ScenePath = "Assets/Scenes/SampleScene.unity";

    public static void Run()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Object.DestroyImmediate(GameObject.Find("[DevAutoQA]"));
        new GameObject("[DevAutoQA]").AddComponent<DevAutoQA>();
        EditorApplication.isPlaying = true;
    }
}
#endif
