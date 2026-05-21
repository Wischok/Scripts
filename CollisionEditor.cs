using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(Collision))]
public class CollisionEditor : Editor
{
    // ---- Serialized property references ----
    private SerializedProperty _naturalKinematics;
    private SerializedProperty _isTrigger;
    private SerializedProperty _volumeType;
    private SerializedProperty _radius;
    private SerializedProperty _height;
    private SerializedProperty _width;
    private SerializedProperty _depth;

    // ---- Inspector state ----
    private bool _surfaceListFoldout = false;

    // ---- Selection state ----
    private int _selectedVertIdx    = -1;
    private int _selectedSurfaceIdx = -1;

    private class VertexRecord
    {
        public Vector3                    Position3D;
        public List<(int surf, int vert)> Locations = new();
    }
    private readonly List<VertexRecord> _uniqueVerts = new();

    // Distinct cyan shades used to tint surfaces associated with the selected vertex.
    // Each index corresponds to a different surface so they can be told apart at a glance.
    private static readonly Color[] SurfaceTints =
    {
        new Color(0f,   1f,   1f,   0.30f),  // full cyan
        new Color(0f,   0.4f, 1f,   0.30f),  // blue
        new Color(0f,   1f,   0.4f, 0.30f),  // green-cyan
        new Color(0.6f, 1f,   1f,   0.30f),  // pale / white-cyan
    };

    // ---- Edge state ----
    private class EdgeRecord
    {
        public Vector3                         EndpointA, EndpointB;
        public Vector2                         ViewA, ViewB, MidView;
        public Vector3                         Mid3D;
        public List<(int surf, int a, int b)>  Locations = new();
    }
    private readonly List<EdgeRecord> _uniqueEdges = new();

    private void OnEnable()
    {
        _naturalKinematics = serializedObject.FindProperty("_naturalKinematics");
        _isTrigger         = serializedObject.FindProperty("IsTrigger");
        _volumeType        = serializedObject.FindProperty("_volumeType");
        _radius            = serializedObject.FindProperty("_radius");
        _height            = serializedObject.FindProperty("_height");
        _width             = serializedObject.FindProperty("_width");
        _depth             = serializedObject.FindProperty("_depth");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(_naturalKinematics, new GUIContent("Natural Kinematics"));
        EditorGUILayout.PropertyField(_isTrigger,         new GUIContent("Is Trigger"));
        EditorGUILayout.Space();
        EditorGUILayout.PropertyField(_volumeType, new GUIContent("Volume Type"));
        EditorGUILayout.Space();

        switch ((Collision.VolumeType)_volumeType.enumValueIndex)
        {
            case Collision.VolumeType.Sphere:
                EditorGUILayout.PropertyField(_radius, new GUIContent("Radius"));
                break;

            case Collision.VolumeType.Cylinder:
                EditorGUILayout.PropertyField(_radius, new GUIContent("Radius"));
                EditorGUILayout.PropertyField(_height, new GUIContent("Height"));
                break;

            case Collision.VolumeType.Cuboid:
                EditorGUILayout.PropertyField(_width,  new GUIContent("Width"));
                EditorGUILayout.PropertyField(_depth,  new GUIContent("Depth"));
                EditorGUILayout.PropertyField(_height, new GUIContent("Height"));
                break;

            case Collision.VolumeType.Custom:
                Collision col = (Collision)target;

                _surfaceListFoldout = EditorGUILayout.Foldout(_surfaceListFoldout, "Surfaces", true);
                if (_surfaceListFoldout)
                {
                    EditorGUI.indentLevel++;
                    for (int i = 0; i < col.Surfaces.Count; i++)
                    {
                        Surface s    = col.Surfaces[i];
                        string  type = s.IsFloor    ? "Floor"
                                     : s.IsWall     ? "Wall"
                                     : s.IsGradient ? "Gradient"
                                     : "Unknown";

                        bool    selected = i == _selectedSurfaceIdx;
                        Color   prevBg   = GUI.backgroundColor;
                        GUI.backgroundColor = selected ? new Color(0f, 0.8f, 0.8f, 1f) : Color.white;

                        EditorGUILayout.BeginHorizontal();
                        if (GUILayout.Button($"{type} {i}", GUILayout.Width(90)))
                        {
                            _selectedSurfaceIdx = selected ? -1 : i;
                            _selectedVertIdx    = -1;
                            Repaint();
                        }
                        GUI.backgroundColor = prevBg;

                        EditorGUI.BeginDisabledGroup(true);
                        EditorGUILayout.Vector3Field(GUIContent.none, s.Center);
                        EditorGUI.EndDisabledGroup();
                        EditorGUILayout.EndHorizontal();
                    }
                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.HelpBox($"Custom volume — {col.Surfaces.Count} surface{(col.Surfaces.Count == 1 ? "" : "s")}. Edit vertices in the Scene view.", MessageType.Info);
                break;
        }

        if (serializedObject.ApplyModifiedProperties())
            ((Collision)target).RecalculateBounds();

        DrawSelectedVertexInspector();
        DrawSelectedSurfaceInspector();
    }

    private void DrawSelectedVertexInspector()
    {
        Collision col = (Collision)target;
        if (col.Volume != Collision.VolumeType.Custom) return;
        if (_selectedVertIdx < 0 || _selectedVertIdx >= _uniqueVerts.Count) return;

        VertexRecord rec   = _uniqueVerts[_selectedVertIdx];
        Point        point = col.Surfaces[rec.Locations[0].surf].Vertices[rec.Locations[0].vert];

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Selected Vertex", EditorStyles.boldLabel);

        // X
        EditorGUI.BeginChangeCheck();
        float newX = EditorGUILayout.FloatField("X", point.X);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(col, "Edit Vertex X");
            ApplyVertexPosition(col, rec, new Vector3(newX, point.Y, point.Z));
        }

        // Y
        EditorGUI.BeginChangeCheck();
        float newY = EditorGUILayout.FloatField("Y", point.Y);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(col, "Edit Vertex Y");
            ApplyVertexPosition(col, rec, new Vector3(point.X, newY, point.Z));
        }

        // Z — subtract (diff * 0.5) from Y to keep WorldViewPosition2D stable
        EditorGUI.BeginChangeCheck();
        float newZ = Mathf.Max(0f, EditorGUILayout.FloatField("Z", point.Z));
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(col, "Edit Vertex Z");
            float adjustedY = point.Y - (newZ - point.Z) * 0.5f;
            ApplyVertexPosition(col, rec, new Vector3(point.X, adjustedY, newZ));
        }

        // Read-only display of the computed 2D view position
        EditorGUILayout.Space();
        EditorGUI.BeginDisabledGroup(true);
        EditorGUILayout.Vector2Field("World View Position", point.WorldViewPosition2D);
        EditorGUI.EndDisabledGroup();
    }

    private void DrawSelectedSurfaceInspector()
    {
        Collision col = (Collision)target;
        if (col.Volume != Collision.VolumeType.Custom) return;
        if (_selectedSurfaceIdx < 0 || _selectedSurfaceIdx >= col.Surfaces.Count) return;

        Surface s = col.Surfaces[_selectedSurfaceIdx];

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Selected Surface", EditorStyles.boldLabel);

        string type = s.IsFloor    ? "Floor"
                    : s.IsWall     ? "Wall"
                    : s.IsGradient ? "Gradient"
                    : "Unknown";
        EditorGUILayout.LabelField("Type", type);

        EditorGUI.BeginDisabledGroup(true);
        EditorGUILayout.Vector3Field("True Normal", s.TrueNormal);
        EditorGUILayout.Vector3Field("Center",      s.Center);
        EditorGUI.EndDisabledGroup();

        EditorGUILayout.Space();
        if (GUILayout.Button("Flip Normal"))
        {
            Undo.RecordObject(col, "Flip Surface Normal");
            s.FlipStoredNormal();
            EditorUtility.SetDirty(col);
            Repaint();
        }
    }

    private void OnSceneGUI()
    {
        Collision col = (Collision)target;
        if (col.Volume != Collision.VolumeType.Custom) return;

        RebuildUniqueVerts(col);
        RebuildUniqueEdges(col);

        bool shiftHeld = Event.current.shift;

        // Draw edges and midpoint buttons
        foreach (EdgeRecord edge in _uniqueEdges)
        {
            // Draw the edge line (white, 35% opacity)
            Handles.color = new Color(1f, 1f, 1f, 0.35f);
            Handles.DrawLine(new Vector3(edge.ViewA.x, edge.ViewA.y, 0f),
                             new Vector3(edge.ViewB.x, edge.ViewB.y, 0f));

            // Midpoint button — hollow circle (add vertex) or filled circle (add surface)
            Vector3 midWorld = new(edge.MidView.x, edge.MidView.y, 0f);
            float   midSize  = HandleUtility.GetHandleSize(midWorld) * 0.06f;
            Handles.color = Color.green;
            Handles.CapFunction midCap = shiftHeld ? FilledDiscCap : Handles.CircleHandleCap;

            if (Handles.Button(midWorld, Quaternion.identity, midSize, midSize * 1.5f, midCap))
            {
                Undo.RecordObject(col, shiftHeld ? "Add Surface at Edge" : "Insert Vertex at Edge");
                if (shiftHeld)
                    AddSurfaceAtEdgeMidpoint(col, edge);
                else
                    AddVertexAtEdgeMidpoint(col, edge);

                EditorUtility.SetDirty(col);
                Repaint();
                return; // list is stale; exit and let OnSceneGUI redraw next frame
            }
        }

        // Draw yellow lines along gradient edges of wall surfaces
        foreach (var surface in col.Surfaces)
        {
            if (!surface.IsWall) continue;
            int n = surface.Vertices.Count;
            foreach (int gi in surface.GradientEdgeIndices)
            {
                if (gi < 0 || gi >= n) continue;
                Point   va     = surface.Vertices[gi];
                Point   vb     = surface.Vertices[(gi + 1) % n];
                Vector3 worldA = new(va.WorldViewPosition2D.x, va.WorldViewPosition2D.y, 0f);
                Vector3 worldB = new(vb.WorldViewPosition2D.x, vb.WorldViewPosition2D.y, 0f);
                Handles.color  = Color.yellow;
                Handles.DrawLine(worldA, worldB);
            }
        }

        // Draw true-normal arrows from each surface's projected center
        foreach (var surface in col.Surfaces)
        {
            // Project the 3D center into view space
            Vector3 c   = surface.Center;
            Vector3 centerView = new(c.x, c.y + c.z * 0.5f, 0f);

            // Project the normal direction the same way (linear transform, no position offset)
            Vector3 n   = surface.TrueNormal;
            Vector3 dir = new(n.x, n.y + n.z * 0.5f, 0f);
            if (dir.sqrMagnitude < 0.0001f) continue;
            dir = dir.normalized;

            float   arrowLen = HandleUtility.GetHandleSize(centerView) * 0.5f;
            Vector3 tip      = centerView + dir * arrowLen;

            // Shaft
            Handles.color = Color.black; // dark yellow
            Handles.DrawLine(centerView, tip);

            // Arrowhead — two short lines forming a "V" at the tip
            float   headSize = arrowLen * 0.3f;
            Vector3 perp     = new(-dir.y, dir.x, 0f);
            Vector3 headBase = tip - dir * headSize;
            Handles.DrawLine(tip, headBase + perp * (headSize * 0.5f));
            Handles.DrawLine(tip, headBase - perp * (headSize * 0.5f));
        }

        // Tint the directly selected surface with a solid cyan fill.
        if (_selectedSurfaceIdx >= 0 && _selectedSurfaceIdx < col.Surfaces.Count)
        {
            Surface surf   = col.Surfaces[_selectedSurfaceIdx];
            int     vCount = surf.Vertices.Count;
            Vector3[] pts  = new Vector3[vCount];
            for (int v = 0; v < vCount; v++)
            {
                Vector2 vp = surf.Vertices[v].WorldViewPosition2D;
                pts[v] = new Vector3(vp.x, vp.y, 0f);
            }
            Handles.color = new Color(0f, 0.9f, 0.9f, 0.35f);
            Handles.DrawAAConvexPolygon(pts);
        }

        // Tint surfaces that contain the selected vertex, one shade per surface.
        if (_selectedVertIdx >= 0 && _selectedVertIdx < _uniqueVerts.Count)
        {
            var selLocs = _uniqueVerts[_selectedVertIdx].Locations;
            for (int li = 0; li < selLocs.Count; li++)
            {
                Surface surf   = col.Surfaces[selLocs[li].surf];
                int     vCount = surf.Vertices.Count;

                Vector3[] pts = new Vector3[vCount];
                for (int v = 0; v < vCount; v++)
                {
                    Vector2 vp = surf.Vertices[v].WorldViewPosition2D;
                    pts[v] = new Vector3(vp.x, vp.y, 0f);
                }

                Handles.color = SurfaceTints[li % SurfaceTints.Length];
                Handles.DrawAAConvexPolygon(pts);
            }
        }

        // Draw vertices
        for (int i = 0; i < _uniqueVerts.Count; i++)
        {
            VertexRecord rec   = _uniqueVerts[i];
            Point        point = col.Surfaces[rec.Locations[0].surf].Vertices[rec.Locations[0].vert];
            Vector2      view  = point.WorldViewPosition2D;
            Vector3      world = new(view.x, view.y, 0f);

            float size = HandleUtility.GetHandleSize(world) * 0.08f;

            if (Event.current.control)
            {
                // Ctrl held — red button to delete
                Handles.color = Color.red;
                if (Handles.Button(world, Quaternion.identity, size, size * 1.5f, Handles.DotHandleCap))
                {
                    Undo.RecordObject(col, "Delete Vertex");
                    TryDeleteVertex(col, i);
                    if      (_selectedVertIdx == i) _selectedVertIdx = -1;
                    else if (_selectedVertIdx  > i) _selectedVertIdx--;
                    EditorUtility.SetDirty(col);
                    Repaint();
                    return;
                }
            }
            else
            {
                // FreeMoveHandle for every vertex — click selects, drag moves.
                // Detect the initial mouse-down by watching hotControl change so
                // selection happens on the first press, not after releasing.

                // Check whether this vertex shares any surface with the selected vertex.
                bool sameSurface = false;
                if (i != _selectedVertIdx && _selectedVertIdx >= 0 && _selectedVertIdx < _uniqueVerts.Count)
                {
                    foreach (var (s, _) in rec.Locations)
                    {
                        foreach (var (ss, _) in _uniqueVerts[_selectedVertIdx].Locations)
                        {
                            if (s == ss) { sameSurface = true; break; }
                        }
                        if (sameSurface) break;
                    }
                }

                Handles.color = i == _selectedVertIdx       ? Color.cyan
                              : sameSurface                 ? new Color(0f, 0.55f, 0.55f, 1f)
                              :                               Color.yellow;

                EventType eventBefore = Event.current.type;
                int       hotBefore   = GUIUtility.hotControl;

                EditorGUI.BeginChangeCheck();
                Vector3 moved = Handles.FreeMoveHandle(world, size, Vector3.zero, FilledDiscCap);
                if (EditorGUI.EndChangeCheck())
                {
                    _selectedVertIdx    = i;
                    _selectedSurfaceIdx = -1;
                    Undo.RecordObject(col, "Move Vertex");

                    Vector3 newPos = new(moved.x, moved.y - point.Z * 0.5f, point.Z);

                    // Snap to a nearby vertex when the drag position falls within the
                    // handle's diameter of another vertex's view position.
                    float snapSqr = size * size * 4f;
                    for (int j = 0; j < _uniqueVerts.Count; j++)
                    {
                        if (j == i) continue;
                        Point   op = col.Surfaces[_uniqueVerts[j].Locations[0].surf]
                                           .Vertices[_uniqueVerts[j].Locations[0].vert];
                        Vector2 ov = op.WorldViewPosition2D;
                        if (((Vector2)moved - ov).sqrMagnitude <= snapSqr)
                        {
                            newPos = _uniqueVerts[j].Position3D;
                            break;
                        }
                    }

                    ApplyVertexPosition(col, rec, newPos);
                    Repaint();
                }
                else if (eventBefore == EventType.MouseDown &&
                         GUIUtility.hotControl != 0 &&
                         GUIUtility.hotControl != hotBefore)
                {
                    // Handle captured the mouse — select immediately on press.
                    _selectedVertIdx    = i;
                    _selectedSurfaceIdx = -1;
                    Repaint();
                }
            }
        }
    }

    // Collects all unique vertex positions across every surface.
    private void RebuildUniqueVerts(Collision col)
    {
        _uniqueVerts.Clear();
        for (int s = 0; s < col.Surfaces.Count; s++)
        {
            var surface = col.Surfaces[s];
            for (int v = 0; v < surface.Vertices.Count; v++)
            {
                Vector3 pos   = surface.Vertices[v].Position3D;
                bool    found = false;

                for (int u = 0; u < _uniqueVerts.Count; u++)
                {
                    if ((_uniqueVerts[u].Position3D - pos).sqrMagnitude < 0.0001f)
                    {
                        _uniqueVerts[u].Locations.Add((s, v));
                        found = true;
                        break;
                    }
                }

                if (!found)
                    _uniqueVerts.Add(new VertexRecord
                    {
                        Position3D = pos,
                        Locations  = new List<(int, int)> { (s, v) }
                    });
            }
        }
    }

    // Collects all unique edges (pairs of adjacent vertices) across every surface.
    private void RebuildUniqueEdges(Collision col)
    {
        _uniqueEdges.Clear();
        for (int s = 0; s < col.Surfaces.Count; s++)
        {
            var verts = col.Surfaces[s].Vertices;
            int n     = verts.Count;
            for (int v = 0; v < n; v++)
            {
                int nextV = (v + 1) % n;

                Vector3 posA = verts[v].Position3D;
                Vector3 posB = verts[nextV].Position3D;
                Vector3 mid3D = (posA + posB) * 0.5f;

                Vector2 viewA = verts[v].WorldViewPosition2D;
                Vector2 viewB = verts[nextV].WorldViewPosition2D;
                Vector2 midView = (viewA + viewB) * 0.5f;

                bool found = false;
                foreach (var edge in _uniqueEdges)
                {
                    bool sameAB = (edge.EndpointA - posA).sqrMagnitude < 0.0001f &&
                                  (edge.EndpointB - posB).sqrMagnitude < 0.0001f;
                    bool sameBA = (edge.EndpointA - posB).sqrMagnitude < 0.0001f &&
                                  (edge.EndpointB - posA).sqrMagnitude < 0.0001f;
                    if (sameAB || sameBA)
                    {
                        edge.Locations.Add((s, v, nextV));
                        found = true;
                        break;
                    }
                }

                if (!found)
                    _uniqueEdges.Add(new EdgeRecord
                    {
                        EndpointA = posA,
                        EndpointB = posB,
                        ViewA     = viewA,
                        ViewB     = viewB,
                        MidView   = midView,
                        Mid3D     = mid3D,
                        Locations = new List<(int, int, int)> { (s, v, nextV) }
                    });
            }
        }
    }

    // Removes vertex i from every surface that contains it, then removes surfaces with < 3 vertices.
    private void TryDeleteVertex(Collision col, int uniqueVertIdx)
    {
        VertexRecord rec = _uniqueVerts[uniqueVertIdx];

        // Count how many surfaces would still have >= 3 vertices after removal
        int validAfter = 0;
        for (int s = 0; s < col.Surfaces.Count; s++)
        {
            int removeCount = 0;
            foreach (var (surf, _) in rec.Locations)
                if (surf == s) removeCount++;

            int remaining = col.Surfaces[s].Vertices.Count - removeCount;
            if (remaining >= 3) validAfter++;
            else if (remaining > 0) { /* surface will be culled */ }
        }

        // Refuse if no surface would remain valid
        if (validAfter == 0) return;

        // Group removals by surface, sort indices descending to avoid shifting issues
        var bySurface = new Dictionary<int, List<int>>();
        foreach (var (surf, vert) in rec.Locations)
        {
            if (!bySurface.ContainsKey(surf)) bySurface[surf] = new List<int>();
            bySurface[surf].Add(vert);
        }

        // Remove vertices (descending index order per surface)
        foreach (var (surfIdx, indices) in bySurface)
        {
            indices.Sort((a, b) => b.CompareTo(a));
            foreach (int vi in indices)
                col.Surfaces[surfIdx].RemoveVertexAt(vi);
        }

        // Remove surfaces with fewer than 3 vertices (descending index order)
        var toRemove = new List<int>();
        for (int s = 0; s < col.Surfaces.Count; s++)
            if (col.Surfaces[s].Vertices.Count < 3) toRemove.Add(s);
        toRemove.Sort((a, b) => b.CompareTo(a));
        foreach (int s in toRemove)
            col.RemoveSurface(s);

        col.RecalculateBounds();
    }

    // Inserts a new vertex at the midpoint of the edge in every surface that shares it.
    private void AddVertexAtEdgeMidpoint(Collision col, EdgeRecord edge)
    {
        Point midPoint = new(edge.Mid3D.x, edge.Mid3D.y, edge.Mid3D.z);

        // Process shared surfaces in descending surface index order so indices stay valid
        var sorted = new List<(int surf, int a, int b)>(edge.Locations);
        sorted.Sort((x, y) => y.surf.CompareTo(x.surf));

        foreach (var (surfIdx, idxA, _) in sorted)
            col.Surfaces[surfIdx].AddVertexAt(idxA + 1, midPoint);
    }

    // Creates a new triangular surface sprouting from the edge midpoint, offset perpendicular in XY.
    private void AddSurfaceAtEdgeMidpoint(Collision col, EdgeRecord edge)
    {
        Vector2 edgeDir2D = (edge.ViewB - edge.ViewA).normalized;
        Vector2 perp2D    = new(-edgeDir2D.y, edgeDir2D.x); // 90° CCW in view space
        float   tipDist   = (edge.ViewA - edge.ViewB).magnitude * 0.5f;

        Vector3 mid = edge.Mid3D;
        float   z   = mid.z;

        // Reverse-project XY perp back from view space: view.y = world.y + z*0.5, so world.y = view.y - z*0.5
        Vector2 tipView  = edge.MidView + perp2D * tipDist;
        float   tipWorldY = tipView.y - z * 0.5f;

        Point ptA = new(edge.EndpointA.x, edge.EndpointA.y, edge.EndpointA.z);
        Point ptB = new(edge.EndpointB.x, edge.EndpointB.y, edge.EndpointB.z);
        Point ptC = new(tipView.x,        tipWorldY,         z);

        col.AddSurface(new Surface(ptA, ptB, ptC));
    }

    // Renders a solid flat disc and registers its hit area — used as the cap
    // function for filled-circle vertex handles and shift-midpoint buttons.
    private static void FilledDiscCap(int controlID, Vector3 position, Quaternion rotation,
        float size, EventType eventType)
    {
        if (eventType == EventType.Repaint)
            Handles.DrawSolidDisc(position, Vector3.forward, size);
        else if (eventType == EventType.Layout)
            HandleUtility.AddControl(controlID, HandleUtility.DistanceToCircle(position, size));
    }

    // Writes a new position to every Point sharing this vertex, recalculates
    // affected surface normals, and refreshes the Collision AABB.
    private void ApplyVertexPosition(Collision col, VertexRecord rec, Vector3 newPos)
    {
        foreach (var (surfIdx, vertIdx) in rec.Locations)
        {
            Point p = col.Surfaces[surfIdx].Vertices[vertIdx];
            p.X = newPos.x;
            p.Y = newPos.y;
            p.Z = Mathf.Max(0f, newPos.z);
            col.Surfaces[surfIdx].RecalculateNormal();
        }
        rec.Position3D = newPos;
        col.RecalculateBounds();
        EditorUtility.SetDirty(col);
    }
}
