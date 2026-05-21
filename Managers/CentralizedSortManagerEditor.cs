using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CentralizedSortManager))]
public class CentralizedSortManagerEditor : Editor
{
    private SerializedProperty _sortScale;

    private void OnEnable() =>
        _sortScale = serializedObject.FindProperty("_sortScale");

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.PropertyField(_sortScale, new GUIContent("Sort Scale"));
        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Registered", EditorStyles.boldLabel);

        CentralizedSortManager mgr = (CentralizedSortManager)target;

        EditorGUI.BeginDisabledGroup(true);
        EditorGUILayout.IntField("Entities", mgr.EntityCount);
        EditorGUILayout.IntField("Objects",  mgr.ObjectCount);
        EditorGUI.EndDisabledGroup();
    }
}
