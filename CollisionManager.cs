using System.Collections.Generic;
using UnityEngine;

public class CollisionManager : MonoBehaviour
{
    ///
    /// --------- Inspector Variables ------------------------------------
    /// 

    [SerializeField] private float _floorThreshold = 0.707f;
    [SerializeField] private float _wallThreshold = 0.5f;

    ///
    /// --------- Public Variables -------------------------------------
    ///

    public static CollisionManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<CollisionManager>();
                if (_instance == null)
                {
                    GameObject pm = new("CollisionManager");
                    _instance = pm.AddComponent<CollisionManager>();
                }
            }
            return _instance;
        }
    }

    // Safe guard for OnDestroy: avoids auto-creating a new instance during scene teardown.
    public static bool HasInstance => _instance != null;

    public float FloorThreshold => _floorThreshold;
    public float WallThreshold => _wallThreshold;

    ///
    /// --------- Private Variables ------------------------------------
    ///

    private static CollisionManager _instance;

    // All Collision volumes registered in the scene.
    private readonly List<Collision> _volumes = new();

    // Trigger overlap tracking. Not readonly so the two dicts can be swapped each frame.
    private Dictionary<Collision, Dictionary<Position, CollisionEvent>> _currentTriggers  = new();
    private Dictionary<Collision, Dictionary<Position, CollisionEvent>> _previousTriggers = new();

    // Solid collision tracking — same swap pattern as triggers, no stationary re-check needed.
    private Dictionary<Collision, Dictionary<Position, CollisionEvent>> _currentCollisions  = new();
    private Dictionary<Collision, Dictionary<Position, CollisionEvent>> _previousCollisions = new();

    // Pairs whose physical push response is temporarily suppressed. Events still fire so
    // CollisionEnter doesn't re-trigger when the ignore expires. Key is the unordered
    // (instanceID, instanceID) of the two Positions. Value is FixedUpdate frames remaining.
    private readonly Dictionary<(int, int), int> _ignorePairs = new();
    private readonly List<(int, int)>            _ignoreExpiredScratch = new();

    ///
    /// ---------- Unity Functions ---------------------------------------------------
    ///

    private void Awake()
    {
        if (_instance == null)
            _instance = this;
        else if (_instance != this)
            Destroy(gameObject);
    }

    private void FixedUpdate()
    {
        TickIgnorePairs();
        ProcessTriggerEvents();
    }

    private void ProcessTriggerEvents()
    {
        // Re-check previous overlaps for entities that didn't move this frame.
        foreach (var (volume, prevMap) in _previousTriggers)
        {
            if (!_currentTriggers.TryGetValue(volume, out var currMap))
                currMap = null;

            foreach (var (pos, _) in prevMap)
            {
                if (currMap != null && currMap.ContainsKey(pos)) continue;
                if (!pos.HasCollision) continue;

                Vector3 center      = pos.Pos_3D;
                Bounds  entityBounds = new(center, Vector3.one * (pos.BroadRadius * 2f));
                if (!entityBounds.Intersects(volume.WorldBounds)) continue;
                if (!pos.CollisionVolume.Overlaps(volume, center, volume.Center, out Normal normal)) continue;

                if (currMap == null) _currentTriggers[volume] = currMap = new();
                currMap[pos] = new CollisionEvent(pos, volume, center, normal);
            }
        }

        // TriggerEnter / TriggerStay.
        foreach (var (volume, currMap) in _currentTriggers)
        {
            _previousTriggers.TryGetValue(volume, out var prevMap);
            foreach (var (pos, evt) in currMap)
            {
                if (prevMap == null || !prevMap.ContainsKey(pos))
                {
                    volume.TriggerEnter(evt);
                    if (pos.HasCollision && !HasReverseTrigger(pos, volume))
                        pos.CollisionVolume.TriggerEnter(new CollisionEvent(volume.Position, pos.CollisionVolume, evt.HitPoint, evt.HitNormal.Flipped()));
                }
                else
                {
                    volume.TriggerStay(evt);
                    if (pos.HasCollision && !HasReverseTrigger(pos, volume))
                        pos.CollisionVolume.TriggerStay(new CollisionEvent(volume.Position, pos.CollisionVolume, evt.HitPoint, evt.HitNormal.Flipped()));
                }
            }
        }

        // TriggerExit.
        foreach (var (volume, prevMap) in _previousTriggers)
        {
            _currentTriggers.TryGetValue(volume, out var currMap);
            foreach (var (pos, evt) in prevMap)
            {
                if (currMap != null && currMap.ContainsKey(pos)) continue;
                volume.TriggerExit(new CollisionEvent(pos, volume, pos.Pos_3D, evt.HitNormal));
                if (pos.HasCollision && !HasReverseTrigger(pos, volume))
                    pos.CollisionVolume.TriggerExit(new CollisionEvent(volume.Position, pos.CollisionVolume, pos.Pos_3D, evt.HitNormal.Flipped()));
            }
        }

        // Swap trigger dictionaries; clear the new "current" for next frame.
        (_currentTriggers, _previousTriggers) = (_previousTriggers, _currentTriggers);
        foreach (var map in _currentTriggers.Values) map.Clear();
        _currentTriggers.Clear();

        // CollisionEnter / CollisionStay.
        foreach (var (volume, currMap) in _currentCollisions)
        {
            _previousCollisions.TryGetValue(volume, out var prevMap);
            foreach (var (pos, evt) in currMap)
            {
                if (prevMap == null || !prevMap.ContainsKey(pos))
                {
                    volume.CollisionEnter(evt);
                    // Only fire bidirectional if the reverse entry doesn't exist — meaning
                    // pos is not itself a registered volume that will fire its own event.
                    // When both entities move, each records the other as a volume and fires
                    // naturally; bidirectional would double-fire in that case.
                    if (pos.HasCollision && !HasReverseCollision(pos, volume))
                        pos.CollisionVolume.CollisionEnter(new CollisionEvent(volume.Position, pos.CollisionVolume, evt.HitPoint, evt.HitNormal.Flipped()));
                }
                else
                {
                    volume.CollisionStay(evt);
                    if (pos.HasCollision && !HasReverseCollision(pos, volume))
                        pos.CollisionVolume.CollisionStay(new CollisionEvent(volume.Position, pos.CollisionVolume, evt.HitPoint, evt.HitNormal.Flipped()));
                }
            }
        }

        // CollisionExit.
        foreach (var (volume, prevMap) in _previousCollisions)
        {
            _currentCollisions.TryGetValue(volume, out var currMap);
            foreach (var (pos, evt) in prevMap)
            {
                if (currMap != null && currMap.ContainsKey(pos)) continue;
                volume.CollisionExit(new CollisionEvent(pos, volume, pos.Pos_3D, evt.HitNormal));
                if (pos.HasCollision && !HasReverseCollision(pos, volume))
                    pos.CollisionVolume.CollisionExit(new CollisionEvent(volume.Position, pos.CollisionVolume, pos.Pos_3D, evt.HitNormal.Flipped()));
            }
        }

        // Swap collision dictionaries; clear the new "current" for next frame.
        (_currentCollisions, _previousCollisions) = (_previousCollisions, _currentCollisions);
        foreach (var map in _currentCollisions.Values) map.Clear();
        _currentCollisions.Clear();
    }

    ///
    /// --------- Public Functions -----------------------------------------------------
    ///

    public void RegisterVolume(Collision volume)   => _volumes.Add(volume);
    public void DeregisterVolume(Collision volume) => _volumes.Remove(volume);

    /// Summary:
    ///     Temporarily suppresses the physical push response between two Positions for
    ///     `frames` FixedUpdate ticks. Collision events (Enter/Stay/Exit) continue to
    ///     fire as normal — only the position resolution is skipped, so a chasing entity
    ///     can pass through a freshly-launched ball without disrupting its trajectory.
    ///     Re-registering an existing pair takes the longer of the two durations.
    public void IgnoreCollisionPair(Position a, Position b, int frames)
    {
        if (a == null || b == null || frames <= 0) return;
        (int, int) key = PairKey(a, b);
        if (_ignorePairs.TryGetValue(key, out int existing))
            _ignorePairs[key] = Mathf.Max(existing, frames);
        else
            _ignorePairs[key] = frames;
    }

    /// Summary:
    ///     Queries all registered Collision volumes for overlap with a world-space sphere.
    ///     Appends the Position of each overlapping volume to results (caller must clear
    ///     results before calling to avoid stale entries across frames).
    ///
    ///     Broad-phase: AABB vs query sphere bounds.
    ///     Narrow-phase: delegates to Collision.OverlapsSphere per volume type.
    ///     Custom volumes use AABB as an approximation (no precise surface test).
    ///
    /// Parameters:
    ///     center:  world-space center of the query sphere.
    ///     radius:  radius of the query sphere.
    ///     results: list to append matching Positions to (not cleared internally).
    public void OverlapSphere(Vector3 center, float radius, List<Position> results)
    {
        Bounds queryBounds = new(center, Vector3.one * (radius * 2f));

        foreach (Collision volume in _volumes)
        {
            if (!queryBounds.Intersects(volume.WorldBounds)) continue;
            if (volume.OverlapsSphere(center, radius))
                results.Add(volume.Position);
        }
    }

    /// Summary:
    ///     Called by Position.Pos_4D before the new value is committed.
    ///     Tests the proposed position against every registered volume and modifies
    ///     newPos in-place to resolve any collisions before they are applied.
    ///     All checks are in straight 3D space — no height projection.
    public void ValidateAndResolvePosition(in Position pos, ref Vector4 newPos)
    {
        if (_volumes.Count == 0) return;

        // Two passes: the first resolves primary collisions; the second catches any new
        // penetrations created by the first response (e.g. sliding into a second wall).
        // This is the same iterative-constraint pattern used by most game physics engines.
        const int   passes         = 2;
        bool        anyElevation   = false;
        float       r              = pos.BroadRadius;

        for (int pass = 0; pass < passes; pass++)
        {
            Vector3 proposedCenter = new(newPos.x, newPos.y, newPos.z + newPos.w);
            Bounds  entityBounds   = new(pos.Pos_3D, Vector3.one * (r * 2f));
            entityBounds.Encapsulate(new Bounds(proposedCenter, Vector3.one * (r * 2f)));

            bool elevationSet = false;

            foreach (Collision volume in _volumes)
            {
                if (volume.gameObject == pos.gameObject) continue;
                if (!entityBounds.Intersects(volume.WorldBounds)) continue;
                ResolveCollision(pos, volume, ref newPos, proposedCenter, ref elevationSet);
            }

            if (elevationSet) anyElevation = true;
        }

        // If no surface claimed the entity across all passes it has left all elevated surfaces.
        if (!anyElevation && newPos.w > 0f)
        {
            newPos.z += newPos.w;
            newPos.w  = 0f;
        }
    }

    ///
    /// --------- Private Functions -----------------------------------------------------
    ///

    // Returns true if pos's own Collision is already recorded as a volume that volume's
    // Position entered this frame — meaning the reverse collision exists and will fire
    // its own event, so bidirectional firing would double-fire.
    private bool HasReverseCollision(Position pos, Collision volume) =>
        pos.HasCollision
        && volume.Position != null
        && _currentCollisions.TryGetValue(pos.CollisionVolume, out var m)
        && m.ContainsKey(volume.Position);

    // Unordered pair key so IgnoreCollisionPair(a,b) and (b,a) hit the same entry.
    private static (int, int) PairKey(Position a, Position b)
    {
        int idA = a.gameObject.GetInstanceID();
        int idB = b.gameObject.GetInstanceID();
        return idA < idB ? (idA, idB) : (idB, idA);
    }

    private bool IsPairIgnored(Position a, Position b)
    {
        if (a == null || b == null || _ignorePairs.Count == 0) return false;
        return _ignorePairs.ContainsKey(PairKey(a, b));
    }

    // True when one side of the pair is a ball that's currently intangible (hitstop or
    // juggle) AND the other side is a MovingEntity. Walls/floors (no MovingEntity) still
    // push the ball normally, so this only suppresses entity↔ball physical interference.
    private bool IsIntangibleEntityPair(Position a, Position b)
    {
        if (a == null || b == null) return false;

        bool aBallIntangible = a.TryGetComponent<SoccerBall>(out var ballA) && ballA.IsIntangible;
        bool bBallIntangible = b.TryGetComponent<SoccerBall>(out var ballB) && ballB.IsIntangible;
        if (!aBallIntangible && !bBallIntangible) return false;

        Position other = aBallIntangible ? b : a;
        return other.TryGetComponent<MovingEntity>(out _);
    }

    // True when one side carries a KickHandler whose next contact with the ball on the
    // other side would be processed as a power dash kick. Skipping the push on this
    // frame lets the kick fire cleanly from the CollisionEnter event without an early
    // bounce that would disrupt the kicked trajectory.
    private bool IsImminentPowerKick(Position a, Position b)
    {
        if (a == null || b == null) return false;

        if (a.TryGetComponent<KickHandler>(out var khA)
            && b.TryGetComponent<SoccerBall>(out var ballB)
            && khA.WouldHitBeAPowerKick(ballB))
            return true;

        if (b.TryGetComponent<KickHandler>(out var khB)
            && a.TryGetComponent<SoccerBall>(out var ballA)
            && khB.WouldHitBeAPowerKick(ballA))
            return true;

        return false;
    }

    // Decrement every ignore entry by one frame; remove expired entries. Called from
    // FixedUpdate before ProcessTriggerEvents so this frame's resolution sees the
    // updated counts.
    private void TickIgnorePairs()
    {
        if (_ignorePairs.Count == 0) return;

        // Snapshot keys into the scratch buffer so we can mutate the dictionary while iterating.
        _ignoreExpiredScratch.Clear();
        foreach (var key in _ignorePairs.Keys) _ignoreExpiredScratch.Add(key);

        foreach (var key in _ignoreExpiredScratch)
        {
            int remaining = _ignorePairs[key] - 1;
            if (remaining <= 0) _ignorePairs.Remove(key);
            else                _ignorePairs[key] = remaining;
        }
    }

    private bool HasReverseTrigger(Position pos, Collision volume) =>
        pos.HasCollision
        && volume.Position != null
        && _currentTriggers.TryGetValue(pos.CollisionVolume, out var m)
        && m.ContainsKey(volume.Position);

    private void ResolveCollision(in Position pos, in Collision volume, ref Vector4 newPos,
        Vector3 proposedCenter, ref bool elevationSet)
    {
        switch (volume.Volume)
        {
            case Collision.VolumeType.Custom:
            {
                bool    contacted  = false;
                Vector3 hitNormal  = Vector3.up;

                // Wall / ceiling crossing — snap x/y (wall) or elevation (floor/ceiling).
                // Push a small epsilon along the normal so next frame fromDist > 0,
                // preventing the entity from tunneling when it starts exactly on the plane.
                Vector3 entityExtents = pos.HasCollision
                    ? pos.CollisionVolume.LocalBounds.extents
                    : Vector3.zero;

                if (volume.IntersectsMovement(pos.Pos_3D, proposedCenter, entityExtents,
                        out Vector3 hitPt, out Vector3 crossNorm,
                        pos.HasCollision ? pos.CollisionVolume : null))
                {
                    contacted = true;
                    hitNormal = crossNorm;

                    if (Mathf.Abs(crossNorm.z) > 0.7f)
                    {
                        // Floor / ceiling — snap elevation regardless of trigger state.
                        newPos.w = hitPt.z;
                    }
                    else if (!volume.IsTrigger)
                    {
                        // Solid wall — slide or stop based on NaturalKinematics.
                        const float epsilon = 0.001f;
                        if (volume.NaturalKinematics)
                        {
                            // Slide: project the full movement onto the wall plane,
                            // keeping only the tangential (non-penetrating) component.
                            Vector3 movement   = proposedCenter - pos.Pos_3D;
                            Vector3 tangential = movement - Vector3.Dot(movement, crossNorm) * crossNorm;
                            newPos.x = hitPt.x + crossNorm.x * epsilon + tangential.x;
                            newPos.y = hitPt.y + crossNorm.y * epsilon + tangential.y;
                        }
                        else
                        {
                            // Stop: snap to wall boundary, no tangential carry-over.
                            newPos.x = hitPt.x + crossNorm.x * epsilon;
                            newPos.y = hitPt.y + crossNorm.y * epsilon;
                        }
                    }
                    // Triggers: position unmodified — entity passes through.
                }

                // Maintain floor contact regardless of whether a crossing occurred.
                // Guard: only snap elevation if the entity is at or near the surface height.
                // Without this, side-entry into a ramp's XY footprint falsely triggers
                // elevation snapping, launching the entity to the top of the ramp.
                if (volume.TryGetElevation(newPos, out float elevation, out Vector3 floorNorm))
                {
                    float currentHeight = newPos.z + newPos.w;

                    // Snap tolerance: how far below the surface the entity can be and
                    // still be pulled onto it. Derived from the entity's collision height
                    // so taller entities have a proportionally larger step-up range.
                    // Prevents false snaps when the entity enters a surface's XY footprint
                    // from the side at a much lower elevation.
                    float snapTolerance = pos.HasCollision
                        ? pos.CollisionVolume.Height * 0.5f + 0.5f
                        : 2.5f;

                    if (currentHeight >= elevation - snapTolerance)
                    {
                        newPos.w = elevation;
                        newPos.z = Mathf.Max(0f, currentHeight - elevation);
                        elevationSet = true;
                        pos.SetGroundNormal(floorNorm);
                        if (!contacted) { contacted = true; hitNormal = floorNorm; }
                    }
                }

                if (!contacted) break;

                // Record for Enter / Stay / Exit events (same pattern as primitives).
                Normal customNormal = new(hitNormal, hitNormal);
                if (volume.IsTrigger)
                {
                    if (!_currentTriggers.TryGetValue(volume, out var tMap))
                        _currentTriggers[volume] = tMap = new();
                    tMap[pos] = new CollisionEvent(pos, volume, proposedCenter, customNormal);
                }
                else
                {
                    if (!_currentCollisions.TryGetValue(volume, out var cMap))
                        _currentCollisions[volume] = cMap = new();
                    cMap[pos] = new CollisionEvent(pos, volume, proposedCenter, customNormal);
                }
                break;
            }

            case Collision.VolumeType.Sphere:
            case Collision.VolumeType.Cylinder:
            case Collision.VolumeType.Cuboid:

                if (!pos.HasCollision) break;
                if (!pos.CollisionVolume.Overlaps(volume, proposedCenter, volume.Center,
                        out Normal contactNormal)) break;

                if (volume.IsTrigger)
                {
                    // Record overlap — Enter/Stay/Exit determined in FixedUpdate.
                    if (!_currentTriggers.TryGetValue(volume, out var map))
                        _currentTriggers[volume] = map = new();
                    map[pos] = new CollisionEvent(pos, volume, proposedCenter, contactNormal);
                    break;
                }

                // Record the contact first — Enter/Stay/Exit determined in FixedUpdate.
                // Done before the push so ignored pairs (below) still fire events normally,
                // preventing a spurious CollisionEnter when the ignore window expires.
                if (!_currentCollisions.TryGetValue(volume, out var colMap))
                    _currentCollisions[volume] = colMap = new();
                colMap[pos] = new CollisionEvent(pos, volume, proposedCenter, contactNormal);

                // Skip the physical push when any layer applies:
                //   1. Pair is temporarily ignored (post-strike follow-through window).
                //   2. One side is a ball currently intangible (hitstop or juggle) and the
                //      other is a MovingEntity — only strikes may affect a ball in flight.
                //   3. Contact would fire as a power dash kick this same frame; skipping
                //      the push lets the kick apply cleanly without an early bounce.
                // The contact event was recorded above so CollisionEnter won't refire when
                // suppression ends.
                if (IsPairIgnored(pos, volume.Position))         break;
                if (IsIntangibleEntityPair(pos, volume.Position)) break;
                if (IsImminentPowerKick(pos, volume.Position))    break;

                // Push entity out along the true normal (computationally correct axis).
                // Use the entity's actual shape radius (not bounding sphere) so the push
                // lands exactly at the contact boundary; epsilon prevents next-frame re-entry.
                const float primEpsilon = 0.001f;
                float entityRadius = pos.CollisionVolume.Radius;
                float minDist      = entityRadius + volume.Radius + primEpsilon;

                Vector3 delta = proposedCenter - volume.Center;
                float   dist  = ((Vector2)delta).magnitude;

                if (dist > 0.0001f)
                {
                    Vector2 pushDir = (Vector2)contactNormal.trueNormal;
                    Vector2 pushed  = (Vector2)volume.Center + pushDir * minDist;
                    newPos.x = pushed.x;
                    newPos.y = pushed.y;
                }
                else
                {
                    newPos.x += minDist;
                }
                break;
        }
    }
}