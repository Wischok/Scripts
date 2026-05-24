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

    // Combo role of this strike. Gates when the strike is allowed to connect based on
    // the struck ball's air state. See HitPropertyRules.IsValid. Default Any = no gate.
    public HitProperty Property = HitProperty.Any;

    // After this many NextStrikeFrame advances, the strike enters its cancel window —
    // a new strike input will interrupt this one and start the next strike immediately.
    // Set to -1 (default) to disable cancelling: the strike must fully recover before
    // another can begin, and early input triggers a fumble penalty on the player.
    public int CancelFromFrameIndex = -1;
}
