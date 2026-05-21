using UnityEngine;

/// Summary:
///     Pure data asset describing the full frame chain for one attack.
///     Shared and reusable across entities. All mutable runtime state
///     lives on KickHandler, not here.
///
///     Frame storage: all StrikeFrame objects are serialized inline in this
///     asset via [SerializeReference] on FirstFrame's NextFrame chain. No
///     separate list is required — the linked chain IS the storage.
///
///     Authoring: create via Assets > Create > Combat > Strike.
///     Set FirstFrame and link subsequent frames via NextFrame / PreviousFrame.
///     Drag this asset into a KickHandler StrikeBinding entry and into the
///     corresponding Animator event's Object parameter slot.
[CreateAssetMenu(fileName = "Strike", menuName = "Combat/Strike")]
public class Strike : ScriptableObject
{
    [SerializeReference] public StrikeFrame FirstFrame;
}
