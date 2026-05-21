using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(KickHandler))]
public class KickHandlerEditor : Editor
{
    ///
    /// ------- Editor State (SerializeField persists across recompiles) -----
    ///

    [SerializeField] private int  _previewStrikeIndex = 0;
    [SerializeField] private int  _selectedFrameIndex = -1;
    [SerializeField] private bool _facingLeft         = false;

    ///
    /// ------- Colors -------------------------------------------------------
    ///

    private static readonly Color ColHitCircle   = Color.white;
    private static readonly Color ColHitRing     = new Color(0.55f, 0.55f, 0.55f, 0.9f);
    private static readonly Color ColSweetCircle = Color.green;
    private static readonly Color ColSweetRing   = new Color(0f, 0.45f, 0f, 0.95f);
    private static readonly Color ColChain       = Color.yellow;
    private static readonly Color ColSelHit      = new Color(1f, 0.85f, 0.2f, 1f);
    private static readonly Color ColSelSweet    = new Color(0.45f, 1f, 0.45f, 1f);

    ///
    /// ------- Inspector ----------------------------------------------------
    ///

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Strike Frame Editor", EditorStyles.boldLabel);
        EditorGUILayout.Space(2);

        SerializedProperty bindingsProp = serializedObject.FindProperty("_strikeBindings");

        if (bindingsProp == null || bindingsProp.arraySize == 0)
        {
            EditorGUILayout.HelpBox("No Strike Bindings. Add one in the list above.", MessageType.Info);
            return;
        }

        // Strike selector
        string[] names = BuildStrikeNameArray(bindingsProp);
        _previewStrikeIndex = Mathf.Clamp(_previewStrikeIndex, 0, bindingsProp.arraySize - 1);
        _previewStrikeIndex = EditorGUILayout.Popup("Preview Strike", _previewStrikeIndex, names);

        Strike strike = GetSelectedStrike(bindingsProp);
        if (strike == null)
        {
            EditorGUILayout.HelpBox("Selected binding has no Strike asset assigned.", MessageType.Warning);
            return;
        }

        // Facing direction toggle
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel("Preview Facing");
        if (GUILayout.Toggle(!_facingLeft, "Right", EditorStyles.miniButtonLeft))
            _facingLeft = false;
        if (GUILayout.Toggle(_facingLeft, "Left", EditorStyles.miniButtonRight))
            _facingLeft = true;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Frame Chain", EditorStyles.boldLabel);

        List<StrikeFrame> frames = GetFrameChain(strike);

        if (frames.Count == 0)
            EditorGUILayout.HelpBox("No frames yet. Add one below.", MessageType.None);

        // Draw each frame; record index to delete outside the loop
        int deleteIndex = -1;
        for (int i = 0; i < frames.Count; i++)
        {
            if (!DrawFrameInspector(strike, frames[i], i))
                deleteIndex = i;
        }

        if (deleteIndex >= 0)
        {
            DeleteFrame(strike, frames[deleteIndex]);
            _selectedFrameIndex = Mathf.Clamp(_selectedFrameIndex, -1, frames.Count - 2);
            GUIUtility.ExitGUI();
            return;
        }

        EditorGUILayout.Space(4);
        if (GUILayout.Button("+ Add Frame"))
        {
            AddFrame(strike);
            _selectedFrameIndex = frames.Count; // select the new frame
        }

        SceneView.RepaintAll();
    }

    // Returns false when the frame's delete button was pressed.
    private bool DrawFrameInspector(Strike strike, StrikeFrame frame, int index)
    {
        bool isSelected = _selectedFrameIndex == index;

        GUI.backgroundColor = isSelected ? new Color(1f, 0.95f, 0.65f) : Color.white;
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        GUI.backgroundColor = Color.white;

        // Header row
        EditorGUILayout.BeginHorizontal();
        string header = string.IsNullOrWhiteSpace(frame.Label)
            ? $"Frame {index}"
            : $"Frame {index}  —  {frame.Label}";

        if (GUILayout.Button(header, isSelected ? EditorStyles.boldLabel : EditorStyles.label, GUILayout.ExpandWidth(true)))
        {
            _selectedFrameIndex = isSelected ? -1 : index;
            SceneView.RepaintAll();
        }

        bool keepFrame = true;
        if (GUILayout.Button("✕", GUILayout.Width(22)))
            keepFrame = false;

        EditorGUILayout.EndHorizontal();

        // Fields
        EditorGUI.BeginChangeCheck();

        frame.Label       = EditorGUILayout.TextField("Label", frame.Label ?? "");
        frame.LocalOffset = EditorGUILayout.Vector3Field("Local Offset", frame.LocalOffset);
        frame.Radius      = Mathf.Max(0f, EditorGUILayout.FloatField("Radius", frame.Radius));

        float maxSweet        = frame.Radius * 0.75f;
        frame.SweetSpotRadius = EditorGUILayout.Slider(
            new GUIContent("Sweet Spot Radius", $"Max: 75 % of Radius ({maxSweet:F3})"),
            Mathf.Min(frame.SweetSpotRadius, maxSweet),
            0f, maxSweet
        );

        frame.FallOffCurve = EditorGUILayout.CurveField(
            "Fall Off Curve",
            frame.FallOffCurve ?? AnimationCurve.Linear(0f, 1f, 1f, 0.3f)
        );

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(strike, "Edit Strike Frame");
            EditorUtility.SetDirty(strike);
        }

        EditorGUILayout.EndVertical();
        return keepFrame;
    }

    ///
    /// ------- Scene View ---------------------------------------------------
    ///

    private void OnSceneGUI()
    {
        KickHandler kh = (KickHandler)target;

        SerializedProperty bindingsProp = serializedObject.FindProperty("_strikeBindings");
        Strike strike = GetSelectedStrike(bindingsProp);
        if (strike == null) return;

        // Use the oblique-projected 2D position so that LocalOffset.z changes are visible
        // as upward movement in the scene view (matching the in-game HEIGHT_PROJECTION_VECTOR).
        Vector3 entityPos = kh.transform.position;

        Vector2 facingDir = _facingLeft ? Vector2.left : Vector2.right;
        float   angle     = Mathf.Atan2(facingDir.y, facingDir.x);
        float   cos       = Mathf.Cos(angle);
        float   sin       = Mathf.Sin(angle);
        float   invCos    = Mathf.Cos(-angle);
        float   invSin    = Mathf.Sin(-angle);

        List<StrikeFrame> frames = GetFrameChain(strike);
        if (frames.Count == 0) return;

        var scenePos = new Vector3[frames.Count];
        for (int i = 0; i < frames.Count; i++)
            scenePos[i] = GetFrameScenePos(frames[i], entityPos, cos, sin);

        // Chain lines
        Handles.color = ColChain;
        for (int i = 0; i < scenePos.Length - 1; i++)
            Handles.DrawLine(scenePos[i], scenePos[i + 1], 2f);

        // Per-frame visualization and handles
        for (int i = 0; i < frames.Count; i++)
        {
            StrikeFrame frame    = frames[i];
            Vector3     center   = scenePos[i];
            bool        selected = _selectedFrameIndex == i;

            // Hit radius — semi-transparent tint fill, then outline + ring
            Vector3 camFwd = Camera.current.transform.forward;
            Handles.color = selected
                ? new Color(1f, 0.85f, 0.2f, 0.10f)
                : new Color(1f, 1f,    1f,   0.06f);
            Handles.DrawSolidDisc(center, camFwd, frame.Radius);
            Handles.color = selected ? ColSelHit : ColHitCircle;
            Handles.DrawWireDisc(center, camFwd, frame.Radius);
            Handles.color = ColHitRing;
            DrawEllipse(center, frame.Radius, frame.Radius * 0.35f);

            // Sweet spot — same structure, green palette
            if (frame.SweetSpotRadius > 0f)
            {
                Handles.color = selected
                    ? new Color(0.45f, 1f, 0.45f, 0.12f)
                    : new Color(0f,    1f, 0f,    0.08f);
                Handles.DrawSolidDisc(center, camFwd, frame.SweetSpotRadius);
                Handles.color = selected ? ColSelSweet : ColSweetCircle;
                Handles.DrawWireDisc(center, camFwd, frame.SweetSpotRadius);
                Handles.color = ColSweetRing;
                DrawEllipse(center, frame.SweetSpotRadius, frame.SweetSpotRadius * 0.35f);
            }

            // Label above the frame
            string label = string.IsNullOrWhiteSpace(frame.Label)
                ? $"F{i}"
                : $"F{i}: {frame.Label}";
            Handles.Label(center + new Vector3(0f, frame.Radius + 0.15f, 0f), label);

            // Draggable handle dot
            float   dotSize    = HandleUtility.GetHandleSize(center) * 0.08f;
            Handles.color = selected ? Color.yellow : Color.white;

            EditorGUI.BeginChangeCheck();
            Vector3 newCenter = Handles.FreeMoveHandle(
                center,
                dotSize,
                Vector3.zero,
                Handles.DotHandleCap
            );

            if (EditorGUI.EndChangeCheck())
            {
                _selectedFrameIndex = i;
                Undo.RecordObject(strike, "Move Strike Frame");

                // Reverse the facing rotation to write back to LocalOffset.
                // delta.y contains both the rotated Y offset AND the 0.5 * Z projection baked
                // into the handle's displayed position. Subtract the Z contribution first so only
                // the true Y motion is fed into the inverse rotation. Z is never changed by dragging.
                Vector3 delta          = newCenter - entityPos;
                float   correctedDeltaY = delta.y - 0.5f * frame.LocalOffset.z;
                frame.LocalOffset = new Vector3(
                    delta.x         * invCos - correctedDeltaY * invSin,
                    delta.x         * invSin + correctedDeltaY * invCos,
                    frame.LocalOffset.z
                );

                EditorUtility.SetDirty(strike);
                Repaint(); // Sync the inspector LocalOffset field immediately
            }
        }
    }

    ///
    /// ------- Helpers ------------------------------------------------------
    ///

    private string[] BuildStrikeNameArray(SerializedProperty bindingsProp)
    {
        var names = new string[bindingsProp.arraySize];
        for (int i = 0; i < bindingsProp.arraySize; i++)
        {
            Strike s = bindingsProp.GetArrayElementAtIndex(i)
                                   .FindPropertyRelative("Strike")
                                   ?.objectReferenceValue as Strike;
            names[i] = s != null ? s.name : $"Binding {i} (empty)";
        }
        return names;
    }

    private Strike GetSelectedStrike(SerializedProperty bindingsProp)
    {
        if (bindingsProp == null || bindingsProp.arraySize == 0) return null;
        _previewStrikeIndex = Mathf.Clamp(_previewStrikeIndex, 0, bindingsProp.arraySize - 1);
        return bindingsProp.GetArrayElementAtIndex(_previewStrikeIndex)
                           .FindPropertyRelative("Strike")
                           ?.objectReferenceValue as Strike;
    }

    // Returns the frame's oblique-projected scene position.
    // XY: LocalOffset rotated by facing angle.
    // Z:  LocalOffset.z mapped to visual Y via HEIGHT_PROJECTION_VECTOR (0.5), matching Position.Pos_2D.
    private static Vector3 GetFrameScenePos(StrikeFrame frame, Vector3 entitySceneBase, float cos, float sin)
    {
        float rotX = frame.LocalOffset.x * cos - frame.LocalOffset.y * sin;
        float rotY = frame.LocalOffset.x * sin + frame.LocalOffset.y * cos;
        return entitySceneBase + new Vector3(rotX, rotY + 0.5f * frame.LocalOffset.z, 0f);
    }

    // Draws an ellipse in the XY plane. radiusX is the horizontal half-extent,
    // radiusY is the vertical (compressed) half-extent that gives the horizontal-ring look.
    private static void DrawEllipse(Vector3 center, float radiusX, float radiusY, int segments = 48)
    {
        var pts = new Vector3[segments + 1];
        for (int i = 0; i <= segments; i++)
        {
            float t = (float)i / segments * 2f * Mathf.PI;
            pts[i]  = center + new Vector3(Mathf.Cos(t) * radiusX, Mathf.Sin(t) * radiusY, 0f);
        }
        Handles.DrawPolyLine(pts);
    }

    private static List<StrikeFrame> GetFrameChain(Strike strike)
    {
        var frames = new List<StrikeFrame>();
        StrikeFrame current = strike?.FirstFrame;
        int safety = 0;
        while (current != null && safety++ < 100)
        {
            frames.Add(current);
            current = current.NextFrame;
        }
        return frames;
    }

    private static void AddFrame(Strike strike)
    {
        Undo.RecordObject(strike, "Add Strike Frame");

        var newFrame = new StrikeFrame
        {
            Label           = "",
            Radius          = 0.5f,
            SweetSpotRadius = 0.2f,
            FallOffCurve    = AnimationCurve.Linear(0f, 1f, 1f, 0.3f)
        };

        if (strike.FirstFrame == null)
        {
            strike.FirstFrame = newFrame;
        }
        else
        {
            StrikeFrame last = strike.FirstFrame;
            while (last.NextFrame != null) last = last.NextFrame;
            newFrame.PreviousFrame = last;
            last.NextFrame         = newFrame;
        }

        EditorUtility.SetDirty(strike);
    }

    private static void DeleteFrame(Strike strike, StrikeFrame frame)
    {
        Undo.RecordObject(strike, "Delete Strike Frame");

        if (frame.PreviousFrame != null)
            frame.PreviousFrame.NextFrame = frame.NextFrame;
        else
            strike.FirstFrame = frame.NextFrame;

        if (frame.NextFrame != null)
            frame.NextFrame.PreviousFrame = frame.PreviousFrame;

        // Null both links to reduce orphaned managed-reference bloat in the asset file.
        frame.PreviousFrame = null;
        frame.NextFrame     = null;

        EditorUtility.SetDirty(strike);
    }
}
