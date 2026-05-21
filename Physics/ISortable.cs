/// Description:
///    This interface is used to mark objects that can be sorted by the CentralizedSortManager.
///    It is primarily used for entities, but can be used for other objects as well if needed.
using UnityEngine;

[RequireComponent(typeof(Position))]
public abstract class ISortable : MonoBehaviour
{
    private Position _pos;
    protected virtual void Awake() => _pos = GetComponent<Position>();

    /// Summary:
    ///     The effective Y position of the entity / object for sorting purposes
    protected int GetEffectiveY() => (int)_pos.Pos_2D.y;

    /// Summary:
    ///     Set the sorting order of the entity / object's sprite renderer.
    public abstract void SetSortOrder(int order);

    /// Summary:
    ///     Whether the entity / object is dynamic or static for sorting purposes.
    ///     Dynamic sortables will be sorted during play, whereas static sortables will
    ///     be sorted during edit.
    public bool IsDynamic { get; }

    /// Summary:
    ///     Whether the entity / object has an occlusion boundary.
    public bool HasCollision => _pos.HasCollision;

    /// Summary:
    ///     The entity / object transform
    public bool HasMoved => _pos.TransformChanged;

    /// Summary:
    ///     Get whether the entity is in front of or behind a static object with an occlusion
    ///     boundary.
    public abstract bool IsInFront(Position other);
}
