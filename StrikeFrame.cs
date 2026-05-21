using UnityEngine;

/// Summary:
///     One frame in a Strike sequence. Holds the local-space offset and hitbox
///     geometry for a single point along an attack's swing path.
///
///     Frames form a doubly-linked list:
///       PreviousFrame == null  →  head (wind-up anchor; no sweep segment yet)
///       NextFrame == null      →  tail (end of chain)
///
///     LocalOffset is authored in the canonical facing-right direction (+X forward).
///     At runtime, KickHandler rotates the XY components to align with the entity's
///     actual facing direction. The Z component is added directly on top of the
///     entity's current height (Pos_3D.z), so hitboxes rise and fall with jumps.
[System.Serializable]
public class StrikeFrame
{
    /// Summary:
    ///     Human-readable name shown in the inspector and scene view label.
    ///     Optional — purely for authoring clarity.
    public string Label = "";

    /// Summary:
    ///     Outer radius of the spherical hitbox. Any part of the ball overlapping
    ///     this radius along the swept segment registers a hit.
    public float Radius = 0.5f;

    /// Summary:
    ///     Inner radius of the sweet spot. Hits within this radius evaluate
    ///     FallOffCurve at t=0 (should be 1.0 — full power).
    public float SweetSpotRadius = 0.2f;

    /// Summary:
    ///     Maps the normalized distance band from sweet-spot edge to hit-radius
    ///     edge (t: 0..1) to a power multiplier applied on top of BaseForce.
    ///     t=0 (sweet-spot edge) → 1.0 recommended.
    ///     t=1 (hit-radius edge) → desired floor, e.g. 0.3.
    public AnimationCurve FallOffCurve = AnimationCurve.Linear(0f, 1f, 1f, 0.3f);

    /// Summary:
    ///     Offset from the entity's Pos_3D in local space, authored for canonical
    ///     facing-right (+X forward). At runtime:
    ///       XY — rotated in the horizontal plane to match the entity's facing direction.
    ///       Z  — added directly to the entity's current height, so the hitbox
    ///            rises and falls with the entity (e.g. during a jumping headbutt).
    public Vector3 LocalOffset;

    [SerializeReference] public StrikeFrame NextFrame;
    [SerializeReference] public StrikeFrame PreviousFrame;
}
