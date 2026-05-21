/// Description:
///     The surface of objects and their colliders / collisions.
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class Surface
{
    ///
    /// --------- Private Variables --------------------------------------------------------
    ///

    //--- Floor Plan Type Variables ---
    /// Summary:
    ///     The type of floor plan this instance is:
    /// 
    ///     _isGradient:
    ///         A gradient surface, aka a changing, uneven surface such as a ramp or a slope.
    ///         The height of the floor plan changes based on the position on the floor plan.
    /// 
    ///     IsWall:
    ///         A vertical surface such as a wall. Walls can not be stood upon, as this is a
    ///         references specifically to the 2D VERTICAL FACE of the object, rather than a
    ///         vertical surface with 3D dimension. Walls can have gradient edges, uneven or
    ///         sloped parts of the wall, and bottom edges for determining if an entity runs
    ///         into the wall for physics interactions.
    /// 
    ///     IsFloor:
    ///         A flat surface with a consistent height across the entire floor plan. This is
    ///         the most basic type of floor plan, and is used for most surfaces in the game.
    [SerializeField] private bool _isGradient = false;
    [SerializeField] private bool _isWall = false;
    [SerializeField] private bool _isFloor = true;

    /// Summary:
    ///     The various heights defining the floor plan's overall height. _height is for IsFloors, while _minHeight and _maxHeight are used for both _isGradient and IsWall floor plans.
    [SerializeField] private float _height = 0f;
    [SerializeField] private float _minHeight = 0f;
    [SerializeField] private float _maxHeight = 0f;

    /// Summary:
    ///     The points that define the floor plan in 3D space. The points have a corresponding 2D projection (x = x & y = y + (x * 0.5f)) that is used for properly portraying the point from the 3D world onto the 2D viewing plane. The only difference is that an adjusted y value is ignored. Mathematically, it should be about rad(2)/3, found through 30 60 90 triangle computation, but this was found to be unnecssary and not as fun. So y remains the same in every aspect, especially in physics computation.
    [SerializeField] private List<Point> _vertices = new();

    /// The indices of the points to draw the plane equation and normal
    [SerializeField] private int _pIndex = 0;//p index
    [SerializeField] private int _qIndex = 1;//q index
    [SerializeField] private int _rIndex = 2;//r index

    // --- Normal Variables ---

    /// Summary:
    ///     The normals of the floor plan. True normal is the computationally correct normal,
    ///     while physics normal is the same as true normal, except _normalTitlt is added to 
    ///     the z component to create something slightly different. This is just for 
    ///     fun physics interactions. Anything that requires true computation, such as the
    ///     plane
    ///     equation for height calculations, will use the true normal.
    [SerializeField] private Vector3 _trueNormal = Vector3.zero;
    [SerializeField] private float _normalTilt = 0f;
    [SerializeField] private Vector3 _physicsNormal = Vector3.zero;

    // When true, the normal has been manually flipped by the editor. EnsureOutwardNormal
    // and RecalculateTrueNormal both respect this so the flip survives play mode and
    // vertex edits without being overridden by automatic outward-facing correction.
    [SerializeField] private bool _normalFlipped = false;

    // --- Edge Variables ---

    /// Summary:
    ///     The indices of the edges considered to be sloped or gradient edges.
    ///     For walls: edges with different heights between points that are not mostly vertical.
    ///     For ramps: a single value referencing the edge that best defines the gradient direction.
    ///     Used primarily for visual purposes rather than physics interactions.
    [SerializeField] private int[] _gradientEdgeIndices = new int[1] { 0 };

    /// Summary:
    ///     The center point of the surface.
    [SerializeField] private Vector3 _center;

    ///
    /// --------- Public Properties ----------------------------------------------------------
    ///

    // --- Floor Plan Normals ---
    public Vector3 TrueNormal
    {
        get => FlipNormal ? -_trueNormal : _trueNormal;
        private set
        {
            _trueNormal = value;
        }
    }
    public Vector3 PhysicsNormal
    {
        get => _physicsNormal;
        private set
        {
            _physicsNormal = value;
        }
    }
    public float NormalTilt => _normalTilt;
    public bool FlipNormal { get; set; } = false;//flip normal direction

    /// --- Floor Plan Type Properties ---

    public bool IsGradient => _isGradient;
    public bool IsWall     => _isWall;
    public bool IsFloor    => _isFloor;

    /// --- Vertex Access ---

    public IReadOnlyList<Point> Vertices => _vertices;
    public IReadOnlyList<int>   GradientEdgeIndices => _gradientEdgeIndices;
    public Vector3              Center => _center;

    // Recomputes TrueNormal and PhysicsNormal from the current vertex positions.
    public void RecalculateNormal() => ValidatePoints();

    /// Summary:
    ///     Permanently negates the stored true normal and recomputes the physics normal.
    ///     Unlike the FlipNormal toggle (which only affects the getter), this writes
    ///     directly to _trueNormal so the change is serialized and persists.
    public void FlipStoredNormal()
    {
        _normalFlipped = !_normalFlipped;
        _trueNormal    = -_trueNormal;
        RecalculatePhysicsNormal(_normalTilt);
    }

    public void AddVertexAt(int index, Point point)
    {
        _vertices.Insert(index, point);
        if (_vertices.Count >= 3) ValidatePoints();
    }

    public void RemoveVertexAt(int index)
    {
        if (index < 0 || index >= _vertices.Count) return;
        _vertices.RemoveAt(index);
        if (_vertices.Count >= 3) ValidatePoints();
    }

    ///
    /// --------- Constructors ---------------------------------------------------------------
    ///

    // Required for Unity serialization.
    public Surface() { }

    /// Summary:
    ///     Creates a default triangular floor surface centred on the given world position.
    ///     Vertices are spaced 'size' units from the centre so the triangle spans 2*size.
    public Surface(Vector3 center, float size = 0.5f)
    {
        _isFloor    = true;
        _isGradient = false;
        _isWall     = false;
        _vertices   = new List<Point>
        {
            new(center.x - size, center.y - size, center.z),
            new(center.x + size, center.y - size, center.z),
            new(center.x,        center.y + size, center.z),
        };
        
        _pIndex = 0;
        _qIndex = 1;
        _rIndex = 2;
        
        ValidatePoints();
    }

    public Surface(Point a, Point b, Point c)
    {
        _isFloor    = true;
        _isGradient = false;
        _isWall     = false;
        _vertices   = new List<Point> { a, b, c };
        
        _pIndex = 0;
        _qIndex = 1;
        _rIndex = 2;
        
        ValidatePoints();
    }

    ///
    /// --------- Public Methods -------------------------------------------------------------
    ///

    /// Summary:
    ///     Determine the height of the entity based on the floor plan type and the entity's
    ///     position.
    /// 
    /// Parameters:
    ///     entityPosition: the position of the entity
    /// 
    /// Returns:
    ///     The height of the entity based on the floor plan type and the entity's position
    public float DetermineHeight(Vector4 position)
    {
        if (_isWall)
        {
            if (_gradientEdgeIndices.Length < 1)
                return _maxHeight;

            Vector2 queryXY = new(position.x, position.y);

            for (int i = 0; i < _gradientEdgeIndices.Length; i++)
            {
                int     iA  = _gradientEdgeIndices[i];
                int     iB  = (iA + 1) % _vertices.Count;
                Vector2 eA  = (Vector2)_vertices[iA].Position3D;
                Vector2 eB  = (Vector2)_vertices[iB].Position3D;

                // Project the query point onto the edge direction in XY rather than
                // comparing X alone — this correctly handles diagonal wall edges.
                Vector2 edgeDir = eB - eA;
                float   edgeLen = edgeDir.magnitude;
                if (edgeLen < 0.0001f) continue;

                float proj = Vector2.Dot(queryXY - eA, edgeDir / edgeLen);
                if (proj < 0f || proj > edgeLen) continue;

                // Interpolate between the two vertex heights, not global min/max,
                // so the result reflects the actual edge slope at this position.
                float t = proj / edgeLen;
                return Mathf.Clamp(
                    Mathf.Lerp(_vertices[iA].Z, _vertices[iB].Z, t),
                    _minHeight, _maxHeight
                );
            }

            return _maxHeight;
        }

        if (_isGradient)
            return Mathf.Clamp(GradientHeight(position), _minHeight, _maxHeight);

        return _height;
    }

    /// Summary:
    ///     Determine the height of the entity on the gradient based on the entity's foot
    ///     position.
    /// 
    /// Parameters:
    ///     entityFootPosition: 
    ///         the foot position of the entity (where's it's standing on the floor plan)
    /// 
    /// Returns:
    ///     The height of the entity on the gradient based on the entity's foot position
    /// Summary:
    ///     Returns the interpolated height (Z) at the given XY world position by
    ///     fan-triangulating the polygon from vertex 0 and using barycentric
    ///     coordinates inside whichever triangle contains the query point.
    ///
    ///     This is fully normal-independent — height is derived purely from vertex
    ///     positions, so it is stable even on nearly-vertical surfaces where the
    ///     old plane-equation approach (dividing by TrueNormal.z) became singular.
    ///
    ///     Falls back to the plane equation when the query point lies outside all
    ///     triangles (e.g. the entity is at the edge of the surface footprint).
    public float GradientHeight(Vector4 position)
    {
        float px = position.x;
        float py = position.y;

        // Fan triangulation: test each triangle formed by vertex[0], vertex[i], vertex[i+1].
        for (int i = 1; i < _vertices.Count - 1; i++)
        {
            Vector3 a = _vertices[0].Position3D;
            Vector3 b = _vertices[i].Position3D;
            Vector3 c = _vertices[i + 1].Position3D;

            if (BarycentricHeight(px, py, a, b, c, out float h))
                return Mathf.Clamp(h, _minHeight, _maxHeight);
        }

        // Fallback: query point is outside the polygon — extrapolate via plane equation.
        // Guard against near-vertical normals to avoid division instability.
        if (Mathf.Abs(_trueNormal.z) > 0.0001f)
        {
            Vector3 p0 = _vertices[_pIndex].Position3D;
            float   h  = p0.z
                       - (_trueNormal.x * (px - p0.x) + _trueNormal.y * (py - p0.y))
                       / _trueNormal.z;
            return Mathf.Clamp(h, _minHeight, _maxHeight);
        }

        return position.w;
    }

    // Barycentric point-in-triangle test in XY, returning the interpolated Z height.
    // Returns false if the point is outside the triangle or the triangle is degenerate.
    private static bool BarycentricHeight(float px, float py,
        Vector3 a, Vector3 b, Vector3 c, out float height)
    {
        height = 0f;

        float v0x = b.x - a.x, v0y = b.y - a.y;
        float v1x = c.x - a.x, v1y = c.y - a.y;
        float v2x = px  - a.x, v2y = py  - a.y;

        float d00 = v0x * v0x + v0y * v0y;
        float d01 = v0x * v1x + v0y * v1y;
        float d11 = v1x * v1x + v1y * v1y;
        float d20 = v2x * v0x + v2y * v0y;
        float d21 = v2x * v1x + v2y * v1y;

        float denom = d00 * d11 - d01 * d01;
        if (Mathf.Abs(denom) < 1e-8f) return false; // degenerate triangle

        float v = (d11 * d20 - d01 * d21) / denom;
        float w = (d00 * d21 - d01 * d20) / denom;
        float u = 1f - v - w;

        if (u < 0f || v < 0f || w < 0f) return false; // outside triangle

        height = u * a.z + v * b.z + w * c.z;
        return true;
    }

    /// Summary:
    ///     Compute the local-space AABB that encloses all floor plan points of this surface.
    ///     Called once by Collision.RecalculateBounds() to bake the Custom volume bounds.
    public Bounds GetLocalBounds()
    {
        if (_vertices.Count == 0) return new Bounds();

        Vector3 min = _vertices[0].Position3D;
        Vector3 max = _vertices[0].Position3D;

        for (int i = 1; i < _vertices.Count; i++)
        {
            Vector3 p = _vertices[i].Position3D;
            min = Vector3.Min(min, p);
            max = Vector3.Max(max, p);
        }

        Vector3 size   = max - min;
        Vector3 center = (min + max) * 0.5f;
        return new Bounds(center, size);
    }

    /// Summary:
    ///     Check whether a 2D ground-space point falls inside this surface's projected polygon.
    ///     Uses the Projection2D of each floor plan point (screen-space y adjusted for height).
    ///     Returns false if the surface has fewer than three points.
    public bool ContainsPoint(Vector2 point)
    {
        int n = _vertices.Count;
        if (n < 3) return false;

        bool inside = false;
        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            Vector2 pi = _vertices[i].Position3D;
            Vector2 pj = _vertices[j].Position3D;

            if ((pi.y > point.y) != (pj.y > point.y) &&
                point.x < (pj.x - pi.x) * (point.y - pi.y) / (pj.y - pi.y) + pi.x)
            {
                inside = !inside;
            }
        }
        return inside;
    }

    /// Summary:
    ///     Tests whether the segment from→to crosses this wall surface's plane and lands
    ///     inside the polygon. Only applies to wall surfaces — floors and gradients are
    ///     handled entirely by elevation snapping and must not use plane-crossing detection.
    ///
    ///     The plane normal is computed fresh from vertex geometry at call time so the
    ///     result is never gated by which direction _trueNormal happens to face.
    ///
    /// Parameters:
    ///     from:     segment start in 3D world space
    ///     to:       segment end in 3D world space
    ///     radius:   effective separation distance — shifts the detection plane outward so
    ///               a volume with this radius triggers before its center crosses
    ///     hitPoint: 3D point (sphere center at contact) where the crossing is detected
    ///     t:        parametric distance along the segment (0 = from, 1 = to)
    public bool IntersectsSegment(Vector3 from, Vector3 to, float radius,
        out Vector3 hitPoint, out float t)
    {
        hitPoint = Vector3.zero;
        t        = 0f;

        // Floors and gradients are resolved purely through elevation snapping.
        // Plane-crossing detection on these surfaces causes false wall blocks.
        if (_isFloor || _isGradient) return false;

        if (_vertices.Count < 3) return false;

        // Derive the plane normal geometrically from the first two polygon edges.
        // This is independent of _trueNormal so collision is never contingent on
        // which direction the normal was assigned or manually flipped.
        Vector3 e1 = _vertices[1].Position3D - _vertices[0].Position3D;
        Vector3 e2 = _vertices[2].Position3D - _vertices[0].Position3D;
        Vector3 n  = Vector3.Cross(e1, e2).normalized;
        if (n.sqrMagnitude < 0.0001f) return false;

        // Orient the geometric normal toward 'from' so the radius offset always
        // pushes the detection plane toward the approaching entity, not away from it.
        Vector3 p0 = _vertices[0].Position3D;
        if (Vector3.Dot(n, from - p0) < 0f) n = -n;

        float fromDist = Vector3.Dot(n, from - p0) - radius;
        float toDist   = Vector3.Dot(n, to   - p0) - radius;

        // Both endpoints on the same side — no crossing.
        if (fromDist * toDist >= 0f) return false;

        t        = fromDist / (fromDist - toDist);
        hitPoint = from + t * (to - from);

        return ContainsPointLocal(hitPoint, n);
    }

    ///
    /// --------- Private Methods ------------------------------------------------
    ///

    // Point-in-polygon test using principal-axis projection.
    // Identifies the dominant axis of the normal using absolute values and drops
    // it, projecting the polygon onto the remaining two axes. This is sign-independent
    // — flipping the normal produces the same result — making collision detection
    // robust regardless of normal direction.
    private bool ContainsPointLocal(Vector3 point, Vector3 normal)
    {
        float ax = Mathf.Abs(normal.x);
        float ay = Mathf.Abs(normal.y);
        float az = Mathf.Abs(normal.z);

        int  count  = _vertices.Count;
        bool inside = false;
        float px, py;

        if (az >= ax && az >= ay) // dominant Z — project onto XY
        {
            px = point.x; py = point.y;
            for (int i = 0, j = count - 1; i < count; j = i++)
            {
                float ix = _vertices[i].Position3D.x, iy = _vertices[i].Position3D.y;
                float jx = _vertices[j].Position3D.x, jy = _vertices[j].Position3D.y;
                if ((iy > py) != (jy > py) &&
                    px < (jx - ix) * (py - iy) / (jy - iy) + ix)
                    inside = !inside;
            }
        }
        else if (ax >= ay) // dominant X — project onto YZ
        {
            px = point.y; py = point.z;
            for (int i = 0, j = count - 1; i < count; j = i++)
            {
                float ix = _vertices[i].Position3D.y, iy = _vertices[i].Position3D.z;
                float jx = _vertices[j].Position3D.y, jy = _vertices[j].Position3D.z;
                if ((iy > py) != (jy > py) &&
                    px < (jx - ix) * (py - iy) / (jy - iy) + ix)
                    inside = !inside;
            }
        }
        else // dominant Y — project onto XZ
        {
            px = point.x; py = point.z;
            for (int i = 0, j = count - 1; i < count; j = i++)
            {
                float ix = _vertices[i].Position3D.x, iy = _vertices[i].Position3D.z;
                float jx = _vertices[j].Position3D.x, jy = _vertices[j].Position3D.z;
                if ((iy > py) != (jy > py) &&
                    px < (jx - ix) * (py - iy) / (jy - iy) + ix)
                    inside = !inside;
            }
        }

        return inside;
    }

    /// Summary:
    ///     Validate the PQR points used for calculating the plane equation and normal. Ensures 
    ///     that the points are within the bounds of the floor plan points array and are not 
    ///     equal to each other. After validating, it also recalculates the true normal based 
    ///     on the new PQR points.
    /// 
    /// Parameters:
    ///     p, q, r: the indeces of the points to use for calculating the plane equation and normal
    public void ValidatePoints()
    {
        if (_vertices.Count < 3)
        {
            Debug.LogError("Surface must have at least 3 vertices to calculate a normal.");
            return;
        }

        // With exactly 3 vertices the indices are always 0, 1, 2.
        if (_vertices.Count < 4)
        {
            _pIndex = 0; _qIndex = 1; _rIndex = 2;
            RecalculateTrueNormal();
            return;
        }

        // Always anchor P at index 0, then find Q and R as the first two vertices
        // whose Z differs from P. Vertices at the same Z still produce a valid
        // vertical normal when all points are coplanar. The previous implementation
        // skipped the current _qIndex and _rIndex before reassigning them, which
        // caused valid candidates to be missed when the polygon has 4+ points.
        _pIndex = 0;
        float pZ = _vertices[0].Z;
        int   q  = -1;
        int   r  = -1;

        for (int i = 1; i < _vertices.Count; i++)
        {
            if (Mathf.Approximately(_vertices[i].Z, pZ)) continue;
            if (q == -1) q = i;
            else         { r = i; break; }
        }

        // If all vertices share the same Z (flat surface), any three form a valid plane.
        if (q == -1) { _qIndex = 1; _rIndex = 2; }
        else
        {
            _qIndex = q;
            _rIndex = r != -1 ? r : (q == 1 ? 2 : 1);
        }

        RecalculateTrueNormal();
    }

    /// Summary:
    ///     Recalculate the _height, _minHeight, and _maxHeight variables based on the floor plan
    ///     type and the heights of the floor plan points. IsFloor plans will set all 3 values
    ///     to the max height of the points, gradients and walls will set the min and max
    ///     heights
    /// 
    ///     based on the min and max heights of the points.
    private void CalculateHeight()
    {
        if(_isFloor)
        {
            float maxHeight = float.MinValue;

            //recalculate the saved floor plan height, min height, and max height, based on points
            for (int i = 0; i < _vertices.Count; i++)
            {
                if (_vertices[i].Z > maxHeight)
                    maxHeight = _vertices[i].Z;
            }

            _height = maxHeight;
            _minHeight = maxHeight;
            _maxHeight = maxHeight;
        }
        
        else if (_isGradient)
        {
            //for gradient floor plans, calculate the min and max heights based on the points
            _minHeight = float.MaxValue;
            _maxHeight = float.MinValue;
            for (int i = 0; i < _vertices.Count; i++)
            {
                if (_vertices[i].Z < _minHeight)
                    _minHeight = _vertices[i].Z;
                if (_vertices[i].Z > _maxHeight)
                    _maxHeight = _vertices[i].Z;
            }
        }

        else if (_isWall)
        {
            _minHeight = float.MaxValue;
            _maxHeight = float.MinValue;
            for (int i = 0; i < _vertices.Count; i++)
            {
                if (_vertices[i].Z < _minHeight) _minHeight = _vertices[i].Z;
                if (_vertices[i].Z > _maxHeight) _maxHeight = _vertices[i].Z;
            }
        }
    }

    // Sets _isFloor/_isWall/_isGradient from the angle of TrueNormal relative to the
    // Z (up) axis. Must be called after TrueNormal is computed.
    //
    // Boundaries are anchored on 45° (cos45° ≈ 0.707), matching the floor/wall split
    // used in CollisionManager.ResolveCollision:
    //   Floor    |z| >= 0.707  — within 45° of vertical; entity can stand on it
    //   Gradient |z| in [0.5, 0.707) — 30°–45° from up; ramp behaviour
    //   Wall     |z| <  0.5   — normal more horizontal than vertical; lateral deflector,
    //                            accepts up to ~30° of tilt off perfectly vertical
    //
    // Falls back to hardcoded thresholds when CollisionManager is not yet available
    // (e.g. edit mode) so this never throws a NullReferenceException.
    private void DetermineSurfaceType()
    {
        float upDot          = Mathf.Abs(_trueNormal.z);
        float floorThreshold = CollisionManager.HasInstance ? CollisionManager.Instance.FloorThreshold : 0.707f;
        float wallThreshold  = CollisionManager.HasInstance ? CollisionManager.Instance.WallThreshold  : 0.5f;

        if (upDot >= floorThreshold)
        {
            _isFloor = true; _isWall = false; _isGradient = false;
        }
        else if (upDot < wallThreshold)
        {
            _isFloor = false; _isWall = true; _isGradient = false;
        }
        else
        {
            _isFloor = false; _isWall = false; _isGradient = true;
        }
    }

    // Populates _gradientEdgeIndices for wall surfaces by finding edges whose
    // endpoints differ in height, have meaningful XY travel, and aren't too steep.
    // No-op for non-walls.
    private void CheckGradientEdges()
    {
        if (!_isWall) return;

        var indices = new List<int>();
        int n = _vertices.Count;
        for (int i = 0; i < n; i++)
        {
            int     next   = (i + 1) % n;
            Vector3 delta  = _vertices[next].Position3D - _vertices[i].Position3D;
            float   deltaZ = Mathf.Abs(delta.z);
            Vector2 deltaXY = (Vector2)delta;
            float   xyDist  = deltaXY.magnitude;

            // Must have a height change.
            if (Mathf.Approximately(delta.z, 0f)) continue;

            // Purely vertical edge (no XY travel) — not a gradient edge.
            if (xyDist < 0.001f) continue;

            // Slope too steep: Z change dominates over XY travel (steeper than 65°).
            if (deltaZ / xyDist > 2.145f) continue;

            // XY direction must not be mostly perpendicular to the wall plane.
            float dot = Mathf.Abs(Vector2.Dot(deltaXY.normalized, Vector2.up));
            if (dot >= 0.95f) continue;

            indices.Add(i);
        }
        _gradientEdgeIndices = indices.ToArray();
    }

    // Flips _trueNormal (and recomputes PhysicsNormal) if the normal points toward
    // volumeCenter rather than away from it. Called from Collision.RecalculateBounds.
    public void EnsureOutwardNormal(Vector3 volumeCenter)
    {
        if (_normalFlipped) return;

        Vector3 outward = _center - volumeCenter;
        if (outward.sqrMagnitude < 0.0001f) return;
        if (Vector3.Dot(_trueNormal, outward) < 0f)
        {
            _trueNormal = -_trueNormal;
            RecalculatePhysicsNormal(_normalTilt);
        }
    }

    // Sets _center to the average position of all vertices.
    private void CalculateCenter()
    {
        if (_vertices.Count == 0) return;
        Vector3 sum = Vector3.zero;
        foreach (Point p in _vertices) sum += p.Position3D;
        _center = sum / _vertices.Count;
    }

    /// Summary:
    ///     Recalculate the true normal of the floor plan based on the current positions of
    ///     the floor plan points. This is based on the plane equation defined by three points
    ///     and used for physics interactions with the ramp.
    private void RecalculateTrueNormal()
    {
        // bounds check
        if (_pIndex >= _vertices.Count || _qIndex >= _vertices.Count || _rIndex >= _vertices.Count)
        {
            Debug.LogError("PQR index out of bounds of polygon collider points.");
            return;
        }

        // grab three points in 3D space and construct as Vector3 with z as height
        Vector3 p = _vertices[_pIndex].Position3D;
        Vector3 q = _vertices[_qIndex].Position3D;
        Vector3 r = _vertices[_rIndex].Position3D;

        // two edge vectors on the plane
        Vector3 pq = q - p;
        Vector3 pr = r - p;

        // cross product gives the normal
        Vector3 crossed = Vector3.Cross(pr, pq);
        if (crossed.sqrMagnitude < 1e-8f)
        {
            Debug.LogWarning("Surface has degenerate or collinear PQR vertices — normal cannot be computed.");
            return;
        }
        TrueNormal = crossed.normalized;

        // Floors and gradients are walkable — their normal must always face upward (Z > 0).
        // Use a hardcoded 0.5 threshold (the wall/gradient boundary) so this is safe
        // in edit mode where CollisionManager.Instance may not exist yet.
        // Walls (|Z| <= 0.5) are left alone; EnsureOutwardNormal handles their direction.
        if (Mathf.Abs(_trueNormal.z) > 0.5f && _trueNormal.z < 0f)
            _trueNormal = -_trueNormal;

        // Re-apply the manual flip so vertex edits don't silently discard it.
        if (_normalFlipped)
            _trueNormal = -_trueNormal;

        RecalculatePhysicsNormal(_normalTilt);
        DetermineSurfaceType();
        CalculateHeight();
        CheckGradientEdges();
        CalculateCenter();
    }

    /// Summary:
    ///     Recalculate the physics normal based on the true normal and the tilt. The physics
    ///     normal is NOT the true computational normal of the plane, but rather an altered 
    ///     version of the true normal for phsycics interactions. This is to cater towards
    ///     more interesting and fun gameplay, rather than wholely accurate scenarios.
    /// 
    /// Parameters:
    ///     tilt: the amount to tilt the normal by. Imaging raising the height axis of the
    ///     normal by some amount in order to affect the Z component of physics interactions
    ///     with the object.
    private void RecalculatePhysicsNormal(float tilt)
    {
        _normalTilt = tilt;

        Vector3 adjustedNormal = new (
            TrueNormal.x,
            TrueNormal.y,
            TrueNormal.z + tilt
        );

        PhysicsNormal = adjustedNormal.normalized;
    }
}
