/// Summary:
///     Central definition of when each HitProperty is allowed to connect with a ball,
///     based on the ball's air state. Called by KickHandler.NextStrikeFrame after a
///     swept hit is geometrically confirmed but before the kick is applied.
///
///     "Height" here is per-surface jump height (Position.DistanceFromGround = Pos_4D.z),
///     not absolute world Z — a ball sitting on a raised platform reads height 0,
///     matching real-world intuition.
public static class HitPropertyRules
{
    public static bool IsValid(HitProperty property, SoccerBall ball)
    {
        switch (property)
        {
            case HitProperty.Any:      return true;
            case HitProperty.Bullet:   return true;
            case HitProperty.Launcher: return ball.IsTouchingGround;
            case HitProperty.Aerial:   return !ball.IsTouchingGround;
            case HitProperty.Spike:    return !ball.IsTouchingGround
                                           && ball.Position.DistanceFromGround >= ball.SpikeMinHeight;
            case HitProperty.PopUp:    return !ball.IsTouchingGround
                                           && ball.Position.DistanceFromGround <= ball.PopUpMaxHeight;
            default:                   return true;
        }
    }
}
