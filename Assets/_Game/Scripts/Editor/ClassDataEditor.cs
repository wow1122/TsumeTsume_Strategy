using UnityEditor;
using UnityEngine;

/// <summary>
/// ClassData の Inspector を日本語表示にするエディタ拡張。
/// 見た目（ラベル）を変えるだけで、データや動作には一切影響しない。
/// Editor フォルダ内のスクリプトは Unity エディタ専用で、ゲームのビルドには含まれない。
/// </summary>
[CustomEditor(typeof(ClassData))]
[CanEditMultipleObjects]
public class ClassDataEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField("基本情報", EditorStyles.boldLabel);
        Draw("className", "名前");
        Draw("tier", "階級");

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("移動", EditorStyles.boldLabel);
        Draw("moveType", "移動タイプ");
        Draw("move", "移動力");

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("武器", EditorStyles.boldLabel);
        Draw("usableWeapons", "装備できる武器種");

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("特攻・昇格（データのみ・効果は将来）", EditorStyles.boldLabel);
        Draw("tags", "特攻タグ");
        Draw("promotions", "昇格先");

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
