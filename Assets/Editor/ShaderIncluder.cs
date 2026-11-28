using UnityEngine;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using System.Collections.Generic;

/// <summary>
/// 打包前把所有自写着色器加进 Always Included Shaders，
/// 否则 Shader.Find 在运行时返回 null（编辑器里能找到，打包后丢失）。
/// </summary>
public class ShaderIncluder : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;

    [MenuItem("Tools/把自写着色器加入构建")]
    public static void IncludeShaders()
    {
        var graphicsSettings = AssetDatabase.LoadAssetAtPath<Object>("ProjectSettings/GraphicsSettings.asset");
        if (graphicsSettings == null)
        {
            Debug.LogError("[ShaderIncluder] 找不到 GraphicsSettings.asset");
            return;
        }

        var so = new SerializedObject(graphicsSettings);
        var list = so.FindProperty("m_AlwaysIncludedShaders");
        if (list == null)
        {
            Debug.LogError("[ShaderIncluder] 找不到 m_AlwaysIncludedShaders");
            return;
        }

        // 收集 Assets/Snake 下所有自写着色器
        var guids = AssetDatabase.FindAssets("t:Shader", new[] { "Assets/Snake" });
        var wanted = new List<Shader>();
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var sh = AssetDatabase.LoadAssetAtPath<Shader>(path);
            if (sh != null) wanted.Add(sh);
        }

        // 收集已有的
        var existing = new HashSet<Shader>();
        for (int i = 0; i < list.arraySize; i++)
        {
            var el = list.GetArrayElementAtIndex(i).objectReferenceValue as Shader;
            if (el != null) existing.Add(el);
        }

        int added = 0;
        foreach (var sh in wanted)
        {
            if (existing.Contains(sh)) continue;

            list.InsertArrayElementAtIndex(list.arraySize);
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = sh;
            existing.Add(sh);
            added++;
            Debug.Log("[ShaderIncluder] 加入：" + sh.name);
        }

        so.ApplyModifiedProperties();
        AssetDatabase.SaveAssets();
        Debug.Log($"[ShaderIncluder] 完成，新增 {added} 个，当前共 {list.arraySize} 个");
    }

    public void OnPreprocessBuild(BuildReport report)
    {
        IncludeShaders();
        AssetDatabase.Refresh();
    }
}
