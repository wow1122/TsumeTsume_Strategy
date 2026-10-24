using UnityEditor;
using UnityEngine;

/// <summary>
/// WeaponData の Inspector を日本語表示にするエディタ拡張。
/// 見た目（ラベル）を変えるだけで、データや動作には一切影響しない。
/// Editor フォルダ内のスクリプトは Unity エディタ専用で、ゲームのビルドには含まれない。
/// </summary>
[CustomEditor(typeof(WeaponData))]
[CanEditMultipleObjects]
public class WeaponDataEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField("基本情報", EditorStyles.boldLabel);
        Draw("weaponName", "名前");
        Draw("type", "種類");
        Draw("category", "分類（前衛/後衛）");

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("性能", EditorStyles.boldLabel);
        Draw("might", "威力");
        Draw("minRange", "最小射程");
        Draw("maxRange", "最大射程");

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
