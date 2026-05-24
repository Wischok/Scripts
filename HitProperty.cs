/// Summary:
///     Classifies a Strike by its combo role. Gates when the strike is allowed to
///     connect based on the ball's air state (Z height + vertical velocity).
///
///     Orthogonal to KickType: HitProperty controls validity (input gate),
///     KickType controls the resulting ball motion (output effect). A single
///     Strike can pair any HitProperty with any KickType.
///
///     Validity rules live in HitPropertyRules.IsValid.
public enum HitProperty
{
    // Default — connects regardless of ball state. Existing strikes left as Any
    // continue to behave exactly as before HitProperty was introduced.
    Any,

    // Combo-starter. Valid only when the ball is on the ground.
    Launcher,

    // Mid-combo juggle hit. Valid only when the ball is airborne.
    Aerial,

    // Slam-down. Valid only when the ball is airborne above a minimum height.
    Spike,

    // Low recovery hit. Valid only when the ball is airborne below a maximum height.
    PopUp,

    // Ender. Valid in any state. Closes the combo (handled by ComboHandler — future task).
    Bullet
}
