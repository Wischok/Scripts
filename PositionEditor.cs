using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(Position))]
public class PositionEditor : Editor
{
    private SerializedProperty _groundedThreshold;

    private void OnEnable()
    {
        EditorApplication.update += OnEditorUpdate;
        _groundedThreshold = serializedObject.FindProperty("_groundedThreshold");
    }

    private void OnDisable() => EditorApplication.update -= OnEditorUpdate;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.PropertyField(_groundedThreshold, new GUIContent("Grounded Threshold"));
        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Position", EditorStyles.boldLabel);

        Position  pos = (Position)target;
        Collision col = pos.GetComponent<Collision>();
        Vector4   cur = pos.Pos_4D;

        EditorGUI.BeginChangeCheck();
        float newX = EditorGUILayout.FloatField("X",          cur.x);
        float newY = EditorGUILayout.FloatField("Y",          cur.y);
        float newZ = EditorGUILayout.FloatField("Off Ground", cur.z);
        float newW = EditorGUILayout.FloatField("Elevation",  cur.w);

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(pos, "Edit Position");
            if (col != null) Undo.RecordObject(col, "Edit Position");

            pos.SetPositionInEditor(new Vector4(newX, newY, newZ, newW), col);

            EditorUtility.SetDirty(pos);
            if (col != null) EditorUtility.SetDirty(col);
        }

        EditorGUILayout.Space();
        EditorGUI.BeginDisabledGroup(true);
        EditorGUILayout.Vector2Field("View Position", pos.Pos_2D);
        EditorGUILayout.Toggle("Is Grounded", pos.IsGrounded);
        EditorGUI.EndDisabledGroup();
    }

    private void OnEditorUpdate()
    {
        if (Application.isPlaying || target == null) return;

        Position pos = (Position)target;
        if (!pos.TransformMovedExternally()) return;

        // Use GetComponent directly — CollisionVolume (_collision) is null in edit
        // mode before Awake runs, but GetComponent always works.
        Collision col = pos.GetComponent<Collision>();

        Undo.RecordObject(pos, "Move Position");
        if (col != null) Undo.RecordObject(col, "Move Position");

        Vector3 oldCenter = col != null ? col.Center : Vector3.zero;
        pos.SyncFromTransform();

        if (col != null)
        {
            col.ShiftSurfaces(pos.Pos_3D - oldCenter);
            col.UpdateCenter(pos.Pos_3D);
            EditorUtility.SetDirty(col);
        }

        EditorUtility.SetDirty(pos);
    }

    private void OnSceneGUI()
    {
        Position pos = (Position)target;
        Vector4  p4  = pos.Pos_4D;

        // Red dot: visual center — where the sprite renders (includes both z and w)
        Vector3 footView = new(p4.x, p4.y + 0.5f * (p4.z + p4.w), 0f);
        // Yellow dot: ground shadow — pure XY, no height contribution
        Vector3 shadowView = new(p4.x, p4.y, 0f);

        float dotSize = HandleUtility.GetHandleSize(footView) * 0.06f;

        Handles.color = Color.white;
        Handles.DrawLine(footView, shadowView);

        Handles.color = Color.red;
        Handles.DotHandleCap(0, footView, Quaternion.identity, dotSize, EventType.Repaint);

        Handles.color = Color.yellow;
        Handles.DotHandleCap(0, shadowView, Quaternion.identity, dotSize, EventType.Repaint);
    }
}
