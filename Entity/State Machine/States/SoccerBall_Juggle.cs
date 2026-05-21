using UnityEngine;

/// Summary:
///     The ball is airborne after a player kick. Tracks air time and delegates
///     hit registration to ComboHandler. Transitions back to SoccerBall_Idle
///     when the ball lands.
///
///     Wall bounces during juggle do not exit this state — only a ground contact ends it.
public class SoccerBall_Juggle : State
{
    private SoccerBall _ball;
    private float      _airTime;

    public override void Enter(BaseGameEntity entity)
    {
        _ball    = (SoccerBall)entity;
        _airTime = 0f;
        _ball.ComboHandler.OnJuggleBegin();
    }

    public override void Execute(BaseGameEntity entity)
    {
        _airTime += Time.fixedDeltaTime;

        if (_ball.IsTouchingGround)
        {
            _ball.ChangeState(new SoccerBall_Idle());
        }
    }

    public override void Exit(BaseGameEntity entity)
    {
        _ball.ComboHandler.OnJuggleEnd(_airTime);
    }
}
