using LastSon;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LastSon.Editor
{
    [InitializeOnLoad]
    public sealed class AndroidBuildPreflight : IPreprocessBuildWithReport
    {
        private static readonly string[] RuntimeShaderNames =
        {
            "Standard",
            "Skybox/Procedural",
            "Sprites/Default"
        };

        public int callbackOrder { get { return -1000; } }

        static AndroidBuildPreflight()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        [MenuItem("LastSon/Prepare Scene For Android Build")]
        public static void PrepareSceneForAndroidBuild()
        {
            EnsureRuntimeShadersIncluded();
            BakeProceduralScene("menu");
        }

        public void OnPreprocessBuild(BuildReport report)
        {
            EnsureRuntimeShadersIncluded();
            BakeProceduralScene("build");
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.ExitingEditMode)
                return;

            EnsureRuntimeShadersIncluded();
            BakeProceduralScene("play");
        }

        private static void BakeProceduralScene(string reason)
        {
            if (Application.isPlaying)
                return;

            Bootstrap.BuildSceneIfNeeded();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            Debug.Log("LastSon Android preflight baked procedural scene for " + reason + ".");
        }

        private static void EnsureRuntimeShadersIncluded()
        {
            Object[] graphicsSettingsAssets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
            if (graphicsSettingsAssets == null || graphicsSettingsAssets.Length == 0)
            {
                Debug.LogWarning("LastSon Android preflight could not load GraphicsSettings.asset.");
                return;
            }

            Object graphicsSettingsAsset = graphicsSettingsAssets[0];
            var serializedSettings = new SerializedObject(graphicsSettingsAsset);
            SerializedProperty shaders = serializedSettings.FindProperty("m_AlwaysIncludedShaders");

            if (shaders == null)
            {
                Debug.LogWarning("LastSon Android preflight could not find m_AlwaysIncludedShaders.");
                return;
            }

            bool changed = false;
            for (int i = 0; i < RuntimeShaderNames.Length; i++)
            {
                Shader shader = Shader.Find(RuntimeShaderNames[i]);
                if (shader == null)
                {
                    Debug.LogWarning("LastSon Android preflight could not find shader: " + RuntimeShaderNames[i]);
                    continue;
                }

                if (ContainsShader(shaders, shader))
                    continue;

                int index = shaders.arraySize;
                shaders.InsertArrayElementAtIndex(index);
                shaders.GetArrayElementAtIndex(index).objectReferenceValue = shader;
                changed = true;
            }

            if (!changed)
                return;

            serializedSettings.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
            Debug.Log("LastSon Android preflight added runtime shaders to Always Included Shaders.");
        }

        private static bool ContainsShader(SerializedProperty shaders, Shader shader)
        {
            for (int i = 0; i < shaders.arraySize; i++)
            {
                if (shaders.GetArrayElementAtIndex(i).objectReferenceValue == shader)
                    return true;
            }

            return false;
        }
    }
}
