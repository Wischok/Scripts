/// Description:
///     Manages sprite sorting order for all registered Positions.
///
///     Global base: every Position uses sortingOrder = round(-Pos_2D.y * scale),
///     where Pos_2D.y already encodes height (y + 0.5*(z+w)), so elevated entities
///     naturally sort behind ground-level ones at the same XY.
///
///     Local override: when a moving entity's XY footprint overlaps a registered
///     scene object's collision footprint AND that object has a walkable surface at
///     the entity's position, the entity is forced to sort in front of the object's
///     sprite — i.e. standing on terrain always draws the entity above it.
using System.Collections.Generic;
using UnityEngine;

public class CentralizedSortManager : MonoBehaviour
{
    ///
    /// --------- Singleton -------------------------------------------------------
    ///

    private static CentralizedSortManager _instance;
    public static CentralizedSortManager Instance => _instance;

    ///
    /// --------- Inspector -------------------------------------------------------
    ///

    // Multiply screen-Y by this to get sorting order integers.
    // Increase if entities at similar Y still share an order value.
    [SerializeField] private float _sortScale = 100f;

    ///
    /// --------- Private ---------------------------------------------------------
    ///

    private struct SortEntry
    {
        public Position       Pos;
        public SpriteRenderer Sprite;
    }

    // Entities that move and need re-sorting every LateUpdate.
    private readonly List<SortEntry> _entities = new();

    // Static/quasi-static scene objects (ramps, platforms) used for local overrides.
    private readonly List<SortEntry> _objects = new();

    public int EntityCount => _entities.Count;
    public int ObjectCount => _objects.Count;

    ///
    /// --------- Unity -----------------------------------------------------------
    ///

    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
    }

    private void LateUpdate()
    {
        // Sort every entity every frame — TransformChanged is unreliable here because
        // multiple FixedUpdates can run per display frame; the second FixedUpdate's
        // ClearFrameFlags call resets the flag before LateUpdate ever sees it.
        foreach (SortEntry entry in _entities)
            entry.Pos.SetSortingOrder(ComputeOrder(entry.Pos));
    }

    ///
    /// --------- Registration ----------------------------------------------------
    ///

    /// Summary:
    ///     Register a moving entity. SpriteRenderer is cached here so no
    ///     GetComponent call is needed every frame.
    public void RegisterEntity(Position pos, SpriteRenderer sprite)
    {
        if (_entities.Exists(e => e.Pos == pos)) return;
        _entities.Add(new SortEntry { Pos = pos, Sprite = sprite });
        pos.SetSortingOrder(ComputeOrder(pos));
    }

    public void UnregisterEntity(Position pos) =>
        _entities.RemoveAll(e => e.Pos == pos);

    /// Summary:
    ///     Register a static scene object whose collision volume affects entity
    ///     sorting (ramps, elevated platforms, etc.). Sets its own initial order
    ///     from the base formula.
    public void RegisterObject(Position pos, SpriteRenderer sprite)
    {
        if (_objects.Exists(o => o.Pos == pos)) return;
        _objects.Add(new SortEntry { Pos = pos, Sprite = sprite });
        sprite.sortingOrder = BaseOrder(pos.Pos_2D.y);
    }

    public void UnregisterObject(Position pos) =>
        _objects.RemoveAll(o => o.Pos == pos);

    ///
    /// --------- Sorting Logic ---------------------------------------------------
    ///

    private int ComputeOrder(Position pos)
    {
        int order = BaseOrder(pos.Pos_2D.y);

        foreach (SortEntry obj in _objects)
        {
            Collision col = obj.Pos.CollisionVolume;
            if (col == null) continue;

            // Broad check: do the two collision AABBs overlap?
            Collision entityCol = pos.CollisionVolume;
            Bounds entityBounds = entityCol != null ? entityCol.WorldBounds : new Bounds(pos.Pos_3D, Vector3.zero);
            if (!col.WorldBounds.Intersects(entityBounds)) continue;

            // Does the object have a walkable surface at the entity's XY position?
            if (!col.TryGetElevation(pos.Pos_4D, out float elevation, out _)) continue;

            float entityHeight = pos.Pos_4D.z + pos.Pos_4D.w;
            
            if (entityHeight < elevation - 0.5f)
                order = Mathf.Min(order, obj.Sprite.sortingOrder - 2); // entity is below surface — force behind
            else
                order = Mathf.Max(order, obj.Sprite.sortingOrder + 2); // entity is on surface — force in front            
        }

        return order;
    }

    private int BaseOrder(float screenY) => Mathf.RoundToInt(-screenY * _sortScale);

    ///
    /// --------- Editor Utility --------------------------------------------------
    ///

    [ContextMenu("Sort All")]
    private void SortAll()
    {
        foreach (SortEntry e in _entities)
            e.Pos.SetSortingOrder(ComputeOrder(e.Pos));
        foreach (SortEntry o in _objects)
            o.Sprite.sortingOrder = BaseOrder(o.Pos.Pos_2D.y);
    }
}
