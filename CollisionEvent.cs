using UnityEngine;

public readonly struct CollisionEvent
{
    public Position  ObjectPosition { get; }
    public Collision CollidedVolume { get; }
    public Vector3   HitPoint       { get; }
    public Normal    HitNormal      { get; }
    public GameObject gameObject => CollidedVolume.gameObject;
    public GameObject otherObject => ObjectPosition.gameObject;

    public CollisionEvent(Position pos, Collision col, Vector3 hitPoint, Normal hitNormal)
    {
        ObjectPosition = pos;
        CollidedVolume = col;
        HitPoint       = hitPoint;
        HitNormal      = hitNormal;
    }
}
