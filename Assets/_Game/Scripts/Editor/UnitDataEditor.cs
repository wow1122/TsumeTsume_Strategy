using UnityEditor;
using UnityEngine;

/// <summary>
/// UnitData の Inspector を日本語表示にするエディタ拡張。
/// 見た目（ラベル）を変えるだけで、データや動作には一切影響しない。
/// Editor フォルダ内のスクリプトは Unity エディタ専用で、ゲームのビルドには含まれない。
/// </summary>
[CustomEditor(typeof(UnitData))]
[CanEditMultipleObjects]
public class UnitDataEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField("基本情報", EditorStyles.boldLabel);
        Draw("unitName", "名前");
        Draw("faction", "陣営");
        Draw("unitClass", "兵種");

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("能力値", EditorStyles.boldLabel);
        Draw("maxHP", "HP");
        Draw("strength", "力");
        Draw("magic", "魔力");
        Draw("skill", "技");
        Draw("speed", "速さ");
        Draw("defense", "守備");
        Draw("resistance", "魔防");
        Draw("move", "移動力");

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("装備", EditorStyles.boldLabel);
        Draw("weapon", "武器");

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
