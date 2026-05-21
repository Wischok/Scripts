/// Description:
///     The collision volume of an entity or object in 3D space. Defines the physical shape
///     used for collision detection and physics interactions. Can be a simple primitive
///     (Sphere, Cuboid, Cylinder) or a Custom shape defined by a list of Surfaces.
///
///     Primitives drive entity-vs-entity physics (Overlaps, ContainsPoint).
///     Custom volumes drive world-geometry physics (ApplyElevation, WallCollision).
///
///     All volume types carry a baked AABB (RecalculateBounds) for fast broad-phase rejection.
using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Position))]
public class Collision : MonoBehaviour
{
    ///
    /// -------- Inspector Variables -----------------------------------------------
    ///
    
    [SerializeField] private bool _naturalKinematics = false; // Whether to apply natural kinematics (slide along walls instead of stopping) for non-trigger volumes.

    [SerializeField] private VolumeType _volumeType = VolumeType.Cylinder;

    // Physical dimensions — which fields apply depends on VolumeType:
    //   Sphere   → _radius
    //   Cylinder → _radius, _height
    //   Cuboid   → _width,  _depth, _height
    //   Custom   → _surfaces
    [SerializeField] private float _radius = 0.5f;
    [SerializeField] private float _height = 1.0f;
    [SerializeField] private float _width  = 1.0f;
    [SerializeField] private float _depth  = 1.0f;

    ///
    /// -------- Enums -------------------------------------------------------------
    ///

    public enum VolumeType
    {
        Sphere,
        Cuboid,
        Cylinder,
        Custom
    }

    ///
    /// -------- Public Variables --------------------------------------------------
    ///

    public bool IsTrigger = false;

    public bool NaturalKinematics => _naturalKinematics;

    ///
    /// -------- Public Properties -------------------------------------------------
    ///

    public VolumeType Volume => _volumeType;
    public Vector3 Center => _center;
    public IReadOnlyList<Surface> Surfaces => _surfaces;

    public float Radius
    {
        get => _radius;
        set { _radius = Mathf.Max(0f, value); RecalculateBounds(); }
    }

    public float Height
    {
        get => _height;
        set { _height = Mathf.Max(0f, value); RecalculateBounds(); }
    }

    public float Width
    {
        get => _width;
        set { _width = Mathf.Max(0f, value); RecalculateBounds(); }
    }

    public float Depth
    {
        get => _depth;
        set { _depth = Mathf.Max(0f, value); RecalculateBounds(); }
    }

    /// Summary:
    ///     Cached local-space AABB. Baked from shape parameters (or Custom surface points)
    ///     by RecalculateBounds. Use WorldBounds for world-space queries.
    public Bounds LocalBounds => _localBounds;

    /// Summary:
    ///     World-space AABB — local bounds translated by the object's current position.
    ///     Custom volumes store vertex positions in world space so _localBounds is already
    ///     world-space and needs no translation. Primitives are centered at the origin in
    ///     local space and must be offset by _center.
    public Bounds WorldBounds
    {
        get
        {
            if (_volumeType == VolumeType.Custom) return _localBounds;
            Bounds b = _localBounds;
            b.center += _center + PrimitiveCenterOffset;
            return b;
        }
    }

    // Vertical offset from the foot/base position to the geometric center of the shape.
    // Keeps _localBounds centered at origin so BroadRadius stays correct;
    // the offset is added explicitly wherever world-space geometry is needed.
    private Vector3 PrimitiveCenterOffset => _volumeType switch
    {
        VolumeType.Sphere   => new Vector3(0f, 0f, _radius),
        VolumeType.Cylinder => new Vector3(0f, 0f, _height * 0.5f),
        VolumeType.Cuboid   => new Vector3(0f, 0f, _height * 0.5f),
        _                   => Vector3.zero,
    };

    /// Summary:
    ///     Bounding sphere radius derived from the AABB extents. Encloses the entire
    ///     volume regardless of shape for fast distance rejection.
    public float BroadRadius => _localBounds.extents.magnitude;

    /// Summary:
    ///     The Position component on this GameObject. Cached on Awake since
    ///     Position is a RequireComponent and will always be present.
    public Position Position { get; private set; }

    /// Summary:
    ///     Returns the support (half-extent) of this volume in the given unit direction —
    ///     i.e. the distance from the center to the surface along that direction.
    ///     Used by edge collision to get the correct shape boundary instead of an AABB
    ///     approximation, which produces rectangular collision on diagonal approaches.
    public float SupportRadius(Vector3 dir)
    {
        return _volumeType switch
        {
            VolumeType.Sphere   => _radius,
            VolumeType.Cylinder => _radius * Mathf.Sqrt(dir.x * dir.x + dir.y * dir.y)
                                 + _height * 0.5f * Mathf.Abs(dir.z),
            VolumeType.Cuboid   => Mathf.Abs(dir.x) * _width  * 0.5f
                                 + Mathf.Abs(dir.y) * _depth  * 0.5f
                                 + Mathf.Abs(dir.z) * _height * 0.5f,
            _                   => BroadRadius,
        };
    }

    ///
    /// -------- Private Variables -------------------------------------------------
    ///

    [SerializeField] private List<Surface> _surfaces = new();

    //the bounds of the volume
    private Bounds _localBounds;

    // Position.Pos_3D of this volume. Set in Reset() and kept in sync by Position.Pos_4D setter.
    // Must be serialized so it survives domain reloads — without it, OnEditorUpdate would
    // read (0,0,0) and ShiftSurfaces by the full object position after every recompile.
    [SerializeField] private Vector3 _center;

    ///
    /// -------- Bounds ------------------------------------------------------------
    ///

    /// Summary:
    ///     Recompute the AABB from the current shape parameters. Call this whenever
    ///     dimensions or surfaces change at runtime. Automatically called by Awake
    ///     and by all dimension setters.
    public void RecalculateBounds()
    {
        _localBounds = _volumeType switch
        {
            VolumeType.Sphere   => new Bounds(Vector3.zero, Vector3.one * (_radius * 2f)),
            VolumeType.Cylinder => new Bounds(Vector3.zero, new Vector3(_radius * 2f, _radius * 2f, _height)),
            VolumeType.Cuboid   => new Bounds(Vector3.zero, new Vector3(_width, _depth, _height)),
            VolumeType.Custom   => CalculateCustomBounds(),
            _                   => new Bounds()
        };

        if (_volumeType == VolumeType.Custom)
            foreach (Surface s in _surfaces)
                s.EnsureOutwardNormal(_center);
    }

    ///
    /// --------- Center Update --------------------------------------------------
    ///

    public void UpdateCenter(Vector3 position)
    {
        _center = position;
    }

    ///
    /// -------- Surface Management ------------------------------------------------
    ///

    public void AddSurface(Surface surface) { _surfaces.Add(surface);    RecalculateBounds(); }
    public void RemoveSurface(int index)    { _surfaces.RemoveAt(index); RecalculateBounds(); }

    // Translates every vertex in every Custom surface by delta. No-op for primitive volumes.
    public void ShiftSurfaces(Vector3 delta)
    {
        if (_volumeType != VolumeType.Custom) return;
        if (delta.sqrMagnitude < 0.00001f) return;
        foreach (Surface surface in _surfaces)
        {
            foreach (Point p in surface.Vertices)
            {
                p.X += delta.x;
                p.Y += delta.y;
                p.Z += delta.z;
            }
            surface.RecalculateNormal();
        }
        RecalculateBounds();
    }

    ///
    /// -------- Collision / Trigger Events ----------------------------------------
    ///

    public event Action<CollisionEvent> OnTriggerEnter;
    public event Action<CollisionEvent> OnTriggerStay;
    public event Action<CollisionEvent> OnTriggerExit;
    public event Action<CollisionEvent> OnCollisionEnter;
    public event Action<CollisionEvent> OnCollisionStay;
    public event Action<CollisionEvent> OnCollisionExit;

    public void TriggerEnter(CollisionEvent e)   => OnTriggerEnter?.Invoke(e);
    public void TriggerStay(CollisionEvent e)    => OnTriggerStay?.Invoke(e);
    public void TriggerExit(CollisionEvent e)    => OnTriggerExit?.Invoke(e);
    public void CollisionEnter(CollisionEvent e) => OnCollisionEnter?.Invoke(e);
    public void CollisionStay(CollisionEvent e)  => OnCollisionStay?.Invoke(e);
    public void CollisionExit(CollisionEvent e)  => OnCollisionExit?.Invoke(e);

    ///
    /// -------- Elevation Update --------------------------------------------------
    ///

    /// Summary:
    ///     Find which Custom surface the entity is standing over and snap their elevation
    ///     to the highest applicable surface. No-ops for primitive volumes.
    ///
    ///     Call after Position.Overlaps2D confirms the entity is over this volume.
    ///
    /// Parameters:
    ///     position: the entity's Position component
    ///
    /// Returns:
    ///     Physics normal of the surface the entity was snapped to,
    ///     or Vector3.forward if no matching surface was found.
    public Vector3 ApplyElevation(Position position)
    {
        if (_volumeType != VolumeType.Cuboid && _volumeType != VolumeType.Custom) return Vector3.forward;
        if (_volumeType == VolumeType.Custom && _surfaces.Count == 0) return Vector3.forward;

        //if a cuboid, we can just snap to the top face and return its normal
        if (_volumeType == VolumeType.Cuboid)
        {
            //snap the entity to the top face of the cuboid (the highest possible point on the volume)
            position.SetElevation(_localBounds.max.z);
            return Vector3.forward;
        }

        float   bestHeight = float.MinValue;
        Vector3 bestNormal = EntityPhysics.Up;
        bool    found      = false;

        foreach (var surface in _surfaces)
        {
            //if a wall, skip
            if(surface.IsWall) continue;

            if (!surface.ContainsPoint(position.Pos_4D)) continue;

            //get the height of the surface at the entity's current position
            float h = surface.DetermineHeight(position.Pos_4D);

            if (!found || h > bestHeight)
            {
                bestHeight = h;
                bestNormal = surface.PhysicsNormal;
                found      = true;
            }
        }

        if (found)
            position.SetElevation(bestHeight);

        return bestNormal;
    }

    ///
    /// -------- Movement Intersection ---------------------------------------------
    ///

    /// Summary:
    ///     Finds the closest surface the movement segment prevPos→currPos crosses,
    ///     accounting for the entity volume's physical extents.
    ///     Works for walls, floors, and ramps via per-surface 3D plane intersection.
    ///     Only tests Custom volumes.
    ///
    /// Parameters:
    ///     prevPos:       entity's committed 3D position (before the move)
    ///     currPos:       entity's proposed 3D position (after the move)
    ///     entityExtents: half-extents of the entity's AABB — projected onto each surface
    ///                    normal to produce the correct separation distance per surface,
    ///                    handling all primitive shapes accurately
    ///     hitPoint:      world-space point where the crossing occurred
    ///     hitNormal:     physics normal of the surface that was crossed
    public bool IntersectsMovement(Vector3 prevPos, Vector3 currPos, Vector3 entityExtents,
        out Vector3 hitPoint, out Vector3 hitNormal, Collision entityCol = null)
    {
        hitPoint  = Vector3.zero;
        hitNormal = Vector3.zero;

        if (_volumeType != VolumeType.Custom) return false;

        float closestT = float.MaxValue;
        bool  found    = false;

        foreach (var surface in _surfaces)
        {
            // Effective radius for this surface: use the entity's shape support function
            // when available (gives the correct circular boundary for cylinders/spheres),
            // or fall back to the AABB projection when no collision volume is present.
            Vector3 n = surface.TrueNormal;
            float effectiveRadius = entityCol != null
                ? entityCol.SupportRadius(n)
                : Mathf.Abs(entityExtents.x * n.x)
                + Mathf.Abs(entityExtents.y * n.y)
                + Mathf.Abs(entityExtents.z * n.z);

            if (!surface.IntersectsSegment(prevPos, currPos, effectiveRadius,
                    out Vector3 hit, out float t)) continue;
            if (t >= closestT) continue;

            closestT  = t;
            hitPoint  = hit;
            hitNormal = n;
            found     = true;
        }

        // Edge pass: catches corners where adjacent face planes leave a gap.
        // Uses the entity's actual shape support (SupportRadius) so a cylinder stays
        // round on diagonal approaches, rather than using the AABB projection.
        foreach (var surface in _surfaces)
        {
            // Gradients and floors are resolved by elevation snapping, not edge collision.
            if (surface.IsGradient || surface.IsFloor) continue;

            int vCount = surface.Vertices.Count;
            for (int i = 0; i < vCount; i++)
            {
                Vector3 edgeA = surface.Vertices[i].Position3D;
                Vector3 edgeB = surface.Vertices[(i + 1) % vCount].Position3D;

                // Gradient edges mark sloped surface boundaries managed by elevation
                // snapping — skip them unconditionally. XY-projection interpolation of
                // edgeZ is unreliable for diagonal edges, causing false blocks even when
                // the entity is correctly elevated. Non-gradient edges use the Z check below.
                bool isGradientEdge = false;
                foreach (int gi in surface.GradientEdgeIndices)
                    if (gi == i) { isGradientEdge = true; break; }
                if (isGradientEdge) continue;

                // Per-edge height check for non-gradient edges.
                {
                    Vector2 aXY     = (Vector2)edgeA;
                    Vector2 dirXY   = (Vector2)edgeB - aXY;
                    float   lenSqXY = dirXY.sqrMagnitude;
                    float   tEdge   = lenSqXY > 1e-6f
                        ? Mathf.Clamp01(Vector2.Dot((Vector2)prevPos - aXY, dirXY) / lenSqXY)
                        : 0f;
                    float edgeZ = Mathf.Lerp(edgeA.z, edgeB.z, tEdge);
                    if (prevPos.z >= edgeZ - 0.01f) continue;
                }
                Vector3 edgeVec   = edgeB - edgeA;
                float   edgeLenSq = edgeVec.sqrMagnitude;
                if (edgeLenSq < 1e-8f) continue;

                // Distance from entity start position to this edge.
                float   tFrom         = Mathf.Clamp01(Vector3.Dot(prevPos - edgeA, edgeVec) / edgeLenSq);
                Vector3 closestToFrom = edgeA + tFrom * edgeVec;
                float   distAtFrom    = (prevPos - closestToFrom).magnitude;

                // Closest approach between movement segment and edge.
                ClosestSegmentsApproach(prevPos, currPos, edgeA, edgeB,
                    out float sm, out _,
                    out Vector3 onMovement, out Vector3 onEdge);

                Vector3 delta  = onMovement - onEdge;
                float   distSq = delta.sqrMagnitude;
                if (distSq < 1e-8f) continue;

                Vector3 pushDir   = delta * (1f / Mathf.Sqrt(distSq));
                float   effRadius = entityCol != null
                    ? entityCol.SupportRadius(pushDir)
                    : Mathf.Abs(entityExtents.x * pushDir.x)
                    + Mathf.Abs(entityExtents.y * pushDir.y)
                    + Mathf.Abs(entityExtents.z * pushDir.z);

                if (distSq >= effRadius * effRadius) continue;
                if (distAtFrom <= effRadius) continue; // already inside — avoid stuck

                float distAtMin = Mathf.Sqrt(distSq);
                float tEntry    = sm > 1e-6f
                    ? Mathf.Clamp01(sm * (distAtFrom - effRadius) / (distAtFrom - distAtMin))
                    : 0f;

                if (tEntry >= closestT) continue;

                closestT  = tEntry;
                hitPoint  = prevPos + tEntry * (currPos - prevPos);
                hitNormal = pushDir;
                found     = true;
            }
        }

        return found;
    }

    ///
    /// -------- Elevation Query ---------------------------------------------------
    ///

    /// Summary:
    ///     Returns the elevation (w) the entity should snap to based on the highest
    ///     floor or gradient surface beneath their x/y footprint.
    ///     Pure query — does not modify position.
    ///
    /// Parameters:
    ///     pos:       entity's proposed Pos_4D
    ///     elevation: elevation (w) to snap to
    ///     normal:    physics normal of the matching surface
    public bool TryGetElevation(Vector4 pos, out float elevation, out Vector3 normal)
    {
        elevation = 0f;
        normal    = Vector3.forward;

        if (_volumeType == VolumeType.Cuboid)
        {
            elevation = _center.z + _localBounds.max.z;
            return true;
        }

        if (_volumeType != VolumeType.Custom || _surfaces.Count == 0) return false;

        float best  = float.MinValue;
        bool  found = false;

        foreach (var surface in _surfaces)
        {
            if (surface.IsWall) continue;
            if (!surface.ContainsPoint(pos)) continue; // implicit Vector4→Vector2 (x, y)

            float h = surface.DetermineHeight(pos);
            if (!found || h > best)
            {
                best   = h;
                normal = surface.PhysicsNormal;
                found  = true;
            }
        }

        elevation = best;
        return found;
    }

    ///
    /// -------- 3D Volume Queries -------------------------------------------------
    ///

    /// Summary:
    ///     Check whether a world-space sphere overlaps this volume.
    ///     Uses the same narrow-phase helpers as Overlaps for primitives.
    ///     Custom volumes fall back to WorldBounds containment (broad-phase approximation).
    ///
    /// Parameters:
    ///     center: world-space center of the query sphere.
    ///     radius: radius of the query sphere.
    public bool OverlapsSphere(Vector3 center, float radius)
    {
        switch (_volumeType)
        {
            case VolumeType.Sphere:
                return SphereSphereOverlap(center, radius, _center + PrimitiveCenterOffset, _radius);

            case VolumeType.Cylinder:
                return SphereCylinderOverlap(center, radius, _center + PrimitiveCenterOffset, this);

            case VolumeType.Cuboid:
            {
                Vector3 boxCenter = _center + PrimitiveCenterOffset;
                Vector3 clamped   = new Vector3(
                    Mathf.Clamp(center.x, boxCenter.x - _width  * 0.5f, boxCenter.x + _width  * 0.5f),
                    Mathf.Clamp(center.y, boxCenter.y - _depth  * 0.5f, boxCenter.y + _depth  * 0.5f),
                    Mathf.Clamp(center.z, boxCenter.z - _height * 0.5f, boxCenter.z + _height * 0.5f)
                );
                return (center - clamped).sqrMagnitude <= radius * radius;
            }

            default:
                // Custom volumes: WorldBounds AABB approximation only.
                return WorldBounds.Contains(center);
        }
    }

    /// Summary:
    ///     Check whether a 3D world-space point falls inside this volume.
    ///
    /// Parameters:
    ///     point:  the point to test
    ///     center: the world-space 3D center of this volume
    // basePos: the foot/base world position of this volume (Pos_3D of the owning entity).
    public bool ContainsPoint(Vector3 point, Vector3 basePos)
    {
        Vector3 center = basePos + PrimitiveCenterOffset;
        return _volumeType switch
        {
            VolumeType.Sphere   => SphereContains(point, center),
            VolumeType.Cylinder => CylinderContains(point, center),
            VolumeType.Cuboid   => CuboidContains(point, center),
            _                   => false
        };
    }

    /// Summary:
    ///     Check whether this volume overlaps another. Runs a broad-phase bounding
    ///     sphere check first, then a shape-specific narrow phase.
    ///
    /// Parameters:
    ///     other:       the other Collision volume
    ///     myCenter:    world-space 3D center of this volume
    ///     otherCenter: world-space 3D center of the other volume
    ///     normal:      contact normal pointing from otherCenter toward myCenter.
    ///                  Primitives: derived from the separation axis (XY-only for cylinders).
    ///                  Custom: PhysicsNormal of the surface most facing the separation direction.
    // myBase / otherBase: the foot/base world positions (Pos_3D). The offset to each
    // volume's geometric center is applied internally so callers always pass Pos_3D.
    public bool Overlaps(Collision other, Vector3 myBase, Vector3 otherBase, out Normal normal)
    {
        normal = new Normal(Vector3.zero, Vector3.zero);

        Vector3 myCenter    = myBase    + PrimitiveCenterOffset;
        Vector3 otherCenter = otherBase + other.PrimitiveCenterOffset;

        Vector3 sep      = myCenter - otherCenter;
        float   combined = BroadRadius + other.BroadRadius;
        if (sep.sqrMagnitude > combined * combined) return false;

        bool hit = (_volumeType, other.Volume) switch
        {
            (VolumeType.Sphere,   VolumeType.Sphere)   => SphereSphereOverlap(myCenter, _radius, otherCenter, other.Radius),
            (VolumeType.Cylinder, VolumeType.Cylinder) => CylinderCylinderOverlap(myCenter, otherCenter, other),
            (VolumeType.Sphere,   VolumeType.Cylinder) => SphereCylinderOverlap(myCenter, _radius, otherCenter, other),
            (VolumeType.Cylinder, VolumeType.Sphere)   => SphereCylinderOverlap(otherCenter, other.Radius, myCenter, this),
            _ => true // broad phase passed; mixed/custom types approximate to overlapping
        };

        if (!hit) return false;

        // Custom volumes: surface normals of the face most aligned with the separation direction.
        // Primitive volumes: computed separation axis used for both true and surface normal.
        if (other.Volume == VolumeType.Custom)
            normal = BestSurfaceNormal(other.Surfaces, sep);
        else if (_volumeType == VolumeType.Custom)
            normal = BestSurfaceNormal(_surfaces, sep);
        else if (_volumeType == VolumeType.Cylinder || other.Volume == VolumeType.Cylinder)
        {
            Vector2 xy  = (Vector2)sep;
            Vector3 n   = xy.sqrMagnitude > 0.0001f
                ? new Vector3(xy.x, xy.y, 0f).normalized
                : Vector3.right;
            normal = new Normal(n, n);
        }
        else
        {
            Vector3 n = sep.sqrMagnitude > 0.0001f ? sep.normalized : Vector3.right;
            normal = new Normal(n, n);
        }

        return true;
    }

    ///
    /// -------- Unity Functions ---------------------------------------------------
    ///

    private void OnValidate() => RecalculateBounds();

    private void Reset()
    {
        _center = GetComponent<Position>().Pos_3D;

        _surfaces.Clear();
        _surfaces.Add(new Surface(_center));

        RecalculateBounds();
    }

    private void Awake()
    {
        Position = GetComponent<Position>();
        _center  = Position.Pos_3D;
        RecalculateBounds();
        CollisionManager.Instance.RegisterVolume(this);
    }

    private void OnDestroy()
    {
        if (CollisionManager.HasInstance)
            CollisionManager.Instance.DeregisterVolume(this);
    }

    ///
    /// -------- Private Physics Helpers -------------------------------------------
    ///

    // --- Normal Helpers ---

    // Returns the Normal of the surface whose PhysicsNormal best aligns with 'direction'.
    private static Normal BestSurfaceNormal(IReadOnlyList<Surface> surfaces, Vector3 direction)
    {
        Vector3 dir     = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.right;
        float   bestDot = float.MinValue;
        Surface best    = null;

        foreach (var s in surfaces)
        {
            float dot = Vector3.Dot(s.PhysicsNormal, dir);
            if (dot > bestDot) { bestDot = dot; best = s; }
        }

        return best != null
            ? new Normal(best.TrueNormal, best.PhysicsNormal)
            : new Normal(dir, dir);
    }

    // --- Bounds ---

    private Bounds CalculateCustomBounds()
    {
        if (_surfaces.Count == 0) return new Bounds();

        Bounds combined = _surfaces[0].GetLocalBounds();
        for (int i = 1; i < _surfaces.Count; i++)
            combined.Encapsulate(_surfaces[i].GetLocalBounds());

        return combined;
    }

    // --- 3D Containment ---

    private bool SphereContains(Vector3 point, Vector3 center) =>
        (point - center).sqrMagnitude <= _radius * _radius;

    private bool CylinderContains(Vector3 point, Vector3 center)
    {
        float dx = point.x - center.x, dy = point.y - center.y;
        if (dx * dx + dy * dy > _radius * _radius) return false;
        return Mathf.Abs(point.z - center.z) <= _height * 0.5f;
    }

    private bool CuboidContains(Vector3 point, Vector3 center) =>
        Mathf.Abs(point.x - center.x) <= _width  * 0.5f &&
        Mathf.Abs(point.y - center.y) <= _depth  * 0.5f &&
        Mathf.Abs(point.z - center.z) <= _height * 0.5f;

    // --- 3D Narrow-Phase Overlap ---

    private static bool SphereSphereOverlap(Vector3 c1, float r1, Vector3 c2, float r2)
    {
        float r = r1 + r2;
        return (c1 - c2).sqrMagnitude <= r * r;
    }

    private bool CylinderCylinderOverlap(Vector3 myCenter, Vector3 otherCenter, Collision other)
    {
        float dx = myCenter.x - otherCenter.x, dy = myCenter.y - otherCenter.y;
        float r  = _radius + other.Radius;
        if (dx * dx + dy * dy > r * r) return false;

        float myBottom    = myCenter.z    - _height      * 0.5f;
        float myTop       = myCenter.z    + _height      * 0.5f;
        float otherBottom = otherCenter.z - other.Height * 0.5f;
        float otherTop    = otherCenter.z + other.Height * 0.5f;
        return myTop >= otherBottom && otherTop >= myBottom;
    }

    private static bool SphereCylinderOverlap(Vector3 sphereCenter, float sphereRadius,
        Vector3 cylCenter, Collision cyl)
    {
        float dx = sphereCenter.x - cylCenter.x, dy = sphereCenter.y - cylCenter.y;
        float r  = sphereRadius + cyl.Radius;
        if (dx * dx + dy * dy > r * r) return false;

        float sBottom = sphereCenter.z - sphereRadius;
        float sTop    = sphereCenter.z + sphereRadius;
        float cBottom = cylCenter.z    - cyl.Height * 0.5f;
        float cTop    = cylCenter.z    + cyl.Height * 0.5f;
        return sTop >= cBottom && cTop >= sBottom;
    }

    // --- 2D Ground Overlap Helpers ---

    private bool CircleCircleOverlap2D(Vector2 entityPos, float entityRadius)
    {
        Vector2 myPos = new(_center.x, _center.y);
        float   r     = _radius + entityRadius;
        return (entityPos - myPos).sqrMagnitude <= r * r;
    }

    private bool RectCircleOverlap2D(Vector2 entityPos, float entityRadius)
    {
        Vector2 center   = new(_center.x, _center.y);
        float   nearestX = Mathf.Clamp(entityPos.x, center.x - _width * 0.5f, center.x + _width * 0.5f);
        float   nearestY = Mathf.Clamp(entityPos.y, center.y - _depth * 0.5f, center.y + _depth * 0.5f);
        float   dx       = entityPos.x - nearestX;
        float   dy       = entityPos.y - nearestY;
        return dx * dx + dy * dy <= entityRadius * entityRadius;
    }

    private bool CustomGroundOverlaps(Vector2 shadowPos)
    {
        foreach (var surface in _surfaces)
            if (surface.ContainsPoint(shadowPos)) return true;
        return false;
    }

    // Closest-point parametric approach between two 3D segments.
    // sm : parametric position on the movement segment (p1→p2) at closest approach.
    // se : parametric position on the edge segment     (q1→q2) at closest approach.
    // Algorithm: Ericson "Real-Time Collision Detection" §5.1.9
    private static void ClosestSegmentsApproach(
        Vector3 p1, Vector3 p2,
        Vector3 q1, Vector3 q2,
        out float sm, out float se,
        out Vector3 closestOnMovement, out Vector3 closestOnEdge)
    {
        const float eps = 1e-8f;
        Vector3 d1 = p2 - p1;
        Vector3 d2 = q2 - q1;
        Vector3 r  = p1 - q1;
        float   a  = Vector3.Dot(d1, d1);
        float   e  = Vector3.Dot(d2, d2);
        float   f  = Vector3.Dot(d2, r);

        if (a <= eps && e <= eps)
        {
            sm = se = 0f;
        }
        else if (a <= eps)
        {
            sm = 0f;
            se = Mathf.Clamp01(f / e);
        }
        else
        {
            float c = Vector3.Dot(d1, r);
            if (e <= eps)
            {
                se = 0f;
                sm = Mathf.Clamp01(-c / a);
            }
            else
            {
                float b     = Vector3.Dot(d1, d2);
                float denom = a * e - b * b;
                sm = denom > eps ? Mathf.Clamp01((b * f - c * e) / denom) : 0f;

                se = (b * sm + f) / e;
                if (se < 0f)
                {
                    se = 0f;
                    sm = Mathf.Clamp01(-c / a);
                }
                else if (se > 1f)
                {
                    se = 1f;
                    sm = Mathf.Clamp01((b - c) / a);
                }
            }
        }

        closestOnMovement = p1 + sm * d1;
        closestOnEdge     = q1 + se * d2;
    }

    ///
    /// -------- Gizmos -----------------------------------------------------------
    ///

    private void OnDrawGizmos()
    {
        // Draw the WorldBounds AABB in raw 3D world-space so it matches Bounds.Intersects.
        Bounds  wb = WorldBounds;
        Vector3 c  = wb.center;
        Vector3 e  = wb.extents;

        float vcx = c.x;
        float vcy = c.y;
        float hex = e.x;
        float hey = e.y;

        Vector3 bl = new(vcx - hex, vcy - hey, 0f);
        Vector3 br = new(vcx + hex, vcy - hey, 0f);
        Vector3 tr = new(vcx + hex, vcy + hey, 0f);
        Vector3 tl = new(vcx - hex, vcy + hey, 0f);

        Gizmos.color = Color.red;
        Gizmos.DrawLine(bl, br);
        Gizmos.DrawLine(br, tr);
        Gizmos.DrawLine(tr, tl);
        Gizmos.DrawLine(tl, bl);

        Color volColor  = IsTrigger ? Color.green : Color.cyan;
        Color ringColor = new Color(0f, 0.55f, 0.55f, 0.9f);
        Color tintColor = new Color(volColor.r, volColor.g, volColor.b, 0.07f);

        // transform.position is the oblique-projected 2D view position (Pos_2D).
        // All gizmos are drawn in that same projected space so they match what the
        // game actually sees.
        Vector3 pos = transform.position;

        switch (_volumeType)
        {
            case VolumeType.Sphere:
            {
                // Centre is one radius above the foot; project that offset into view-Y (×0.5).
                Vector3 sphereCenter = new Vector3(pos.x, pos.y + _radius * 0.5f, 0f);
#if UNITY_EDITOR
                if (Camera.current != null)
                {
                    UnityEditor.Handles.color = tintColor;
                    UnityEditor.Handles.DrawSolidDisc(sphereCenter, Camera.current.transform.forward, _radius);
                }
#endif
                Gizmos.color = volColor;
                DrawGizmoCircle2D(sphereCenter, _radius);
                Gizmos.color = ringColor;
                DrawGizmoEllipse2D(sphereCenter, _radius, _radius * 0.35f);
                break;
            }

            case VolumeType.Cylinder:
            {
                float   cylHo     = _height * 0.25f; // = _height * 0.5 * 0.5
                Vector3 cylCenter = new Vector3(pos.x, pos.y + cylHo, 0f);
#if UNITY_EDITOR
                if (Camera.current != null)
                {
                    UnityEditor.Handles.color = tintColor;
                    UnityEditor.Handles.DrawSolidDisc(cylCenter, Camera.current.transform.forward, _radius);
                }
#endif
                Gizmos.color = volColor;
                DrawGizmoCylinder2D(pos);
                Gizmos.color = ringColor;
                DrawGizmoEllipse2D(cylCenter, _radius, _radius * 0.35f);
                break;
            }

            case VolumeType.Cuboid:
            {
                float hw    = _width  * 0.5f;
                float hd    = _depth  * 0.5f;
                float cubHo = _height * 0.25f;
#if UNITY_EDITOR
                if (Camera.current != null)
                {
                    float     cy    = pos.y + cubHo;
                    Vector3[] verts =
                    {
                        new Vector3(pos.x - hw, cy - hd, 0f),
                        new Vector3(pos.x + hw, cy - hd, 0f),
                        new Vector3(pos.x + hw, cy + hd, 0f),
                        new Vector3(pos.x - hw, cy + hd, 0f)
                    };
                    UnityEditor.Handles.DrawSolidRectangleWithOutline(verts, tintColor, Color.clear);
                }
#endif
                Gizmos.color = volColor;
                DrawGizmoCuboid2D(pos);
                Gizmos.color = ringColor;
                DrawGizmoCuboidRing2D(pos, hw, hd, cubHo);
                break;
            }

            // Custom surfaces are drawn by CollisionEditor.OnSceneGUI.
        }
    }

    // Draws a circle in the XY plane of the scene view (Z = 0).
    private void DrawGizmoCircle2D(Vector3 viewCenter, float radius, int segments = 24)
    {
        float   step = 2f * Mathf.PI / segments;
        Vector3 prev = viewCenter + new Vector3(radius, 0f, 0f);
        for (int i = 1; i <= segments; i++)
        {
            float   a  = i * step;
            Vector3 pt = viewCenter + new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f);
            Gizmos.DrawLine(prev, pt);
            prev = pt;
        }
    }

    // Draws an ellipse in the XY plane. radiusX is the full horizontal extent,
    // radiusY is the compressed vertical extent to suggest a horizontal ring on a sphere.
    private void DrawGizmoEllipse2D(Vector3 center, float radiusX, float radiusY, int segments = 32)
    {
        float   step = 2f * Mathf.PI / segments;
        Vector3 prev = center + new Vector3(radiusX, 0f, 0f);
        for (int i = 1; i <= segments; i++)
        {
            float   a  = i * step;
            Vector3 pt = center + new Vector3(Mathf.Cos(a) * radiusX, Mathf.Sin(a) * radiusY, 0f);
            Gizmos.DrawLine(prev, pt);
            prev = pt;
        }
    }

    // Draws a standing cylinder projected into the oblique 2D view.
    // viewFoot: the view-space position of the bottom of the cylinder (entity foot).
    private void DrawGizmoCylinder2D(Vector3 viewFoot)
    {
        // Full height in game Z maps to height * 0.5 in view-Y. Bottom at viewFoot, top 2*ho above.
        float   ho  = _height * 0.5f * 0.5f; // = _height * 0.25f
        Vector3 bot = new(viewFoot.x, viewFoot.y,          0f);
        Vector3 top = new(viewFoot.x, viewFoot.y + 2f * ho, 0f);

        DrawGizmoCircle2D(top, _radius);
        DrawGizmoCircle2D(bot, _radius);

        // Connect the four cardinal tangent points (left, right, front, back).
        for (int i = 0; i < 4; i++)
        {
            float cx = Mathf.Cos(i * Mathf.PI * 0.5f) * _radius;
            float cy = Mathf.Sin(i * Mathf.PI * 0.5f) * _radius;
            Gizmos.DrawLine(new Vector3(viewFoot.x + cx, viewFoot.y + cy,          0f),
                            new Vector3(viewFoot.x + cx, viewFoot.y + cy + 2f * ho, 0f));
        }
    }

    // Draws a cuboid's 12 edges projected into the oblique 2D view.
    // viewFoot: the view-space position of the bottom face (entity foot).
    private void DrawGizmoCuboid2D(Vector3 viewFoot)
    {
        float hw = _width  * 0.5f;
        float hd = _depth  * 0.5f;
        float ho = _height * 0.5f * 0.5f; // full height * HEIGHT_PROJECTION

        // Bottom face corners (at viewFoot — no downward offset)
        Vector3 bfl = new(viewFoot.x - hw, viewFoot.y - hd,          0f);
        Vector3 bfr = new(viewFoot.x + hw, viewFoot.y - hd,          0f);
        Vector3 bbl = new(viewFoot.x - hw, viewFoot.y + hd,          0f);
        Vector3 bbr = new(viewFoot.x + hw, viewFoot.y + hd,          0f);

        // Top face corners (full height above viewFoot)
        Vector3 tfl = new(viewFoot.x - hw, viewFoot.y - hd + 2f * ho, 0f);
        Vector3 tfr = new(viewFoot.x + hw, viewFoot.y - hd + 2f * ho, 0f);
        Vector3 tbl = new(viewFoot.x - hw, viewFoot.y + hd + 2f * ho, 0f);
        Vector3 tbr = new(viewFoot.x + hw, viewFoot.y + hd + 2f * ho, 0f);

        // Bottom face
        Gizmos.DrawLine(bfl, bfr); Gizmos.DrawLine(bfr, bbr);
        Gizmos.DrawLine(bbr, bbl); Gizmos.DrawLine(bbl, bfl);

        // Top face
        Gizmos.DrawLine(tfl, tfr); Gizmos.DrawLine(tfr, tbr);
        Gizmos.DrawLine(tbr, tbl); Gizmos.DrawLine(tbl, tfl);

        // Vertical edges
        Gizmos.DrawLine(bfl, tfl); Gizmos.DrawLine(bfr, tfr);
        Gizmos.DrawLine(bbl, tbl); Gizmos.DrawLine(bbr, tbr);
    }

    // Draws a rectangle in the oblique 2D view at a given vertical offset (mid-height ring).
    // The rectangle matches the cuboid's XY footprint — width in X, depth in Y — shifted up
    // by vOffset to sit at the visual mid-height of the volume.
    private static void DrawGizmoCuboidRing2D(Vector3 viewFoot, float hw, float hd, float vOffset)
    {
        float cy = viewFoot.y + vOffset;
        Vector3 fl = new(viewFoot.x - hw, cy - hd, 0f);
        Vector3 fr = new(viewFoot.x + hw, cy - hd, 0f);
        Vector3 bl = new(viewFoot.x - hw, cy + hd, 0f);
        Vector3 br = new(viewFoot.x + hw, cy + hd, 0f);
        Gizmos.DrawLine(fl, fr);
        Gizmos.DrawLine(fr, br);
        Gizmos.DrawLine(br, bl);
        Gizmos.DrawLine(bl, fl);
    }
}
