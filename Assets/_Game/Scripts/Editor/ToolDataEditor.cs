using UnityEditor;
using UnityEngine;

/// <summary>
/// ToolData の Inspector を日本語表示にするエディタ拡張。
/// 見た目（ラベル）を変えるだけで、データや動作には一切影響しない。
/// Editor フォルダ内のスクリプトは Unity エディタ専用で、ゲームのビルドには含まれない。
/// </summary>
[CustomEditor(typeof(ToolData))]
[CanEditMultipleObjects]
public class ToolDataEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField("基本情報", EditorStyles.boldLabel);
        Draw("toolName", "名前");

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("効果", EditorStyles.boldLabel);
        Draw("healAmount", "HP回復量");
        Draw("maxUses", "使用回数");

        serializedObject.ApplyModifiedProperties();
    }

    /// <summary>指定フィールドを日本語ラベル付きで描画する。</summary>
    private void Draw(string propertyName, string label)
    {
        SerializedProperty prop = serializedObject.FindProperty(propertyName);
        if (prop != null)
            EditorGUILayout.PropertyField(prop, new GUIContent(label), true);
    }
}
