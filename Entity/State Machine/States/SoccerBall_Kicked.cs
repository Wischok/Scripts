using UnityEngine;

public class SoccerBall_Kicked : State
{
    SoccerBall ball;
    bool frameDelay = false;

    public override void Enter(BaseGameEntity entity)
    {
        ball = entity as SoccerBall;
    }

    public override void Execute(BaseGameEntity entity)
    {
        // Frame delay ensures execute waits one tick to let physics settle after the kick.
        if (!frameDelay)
        {
            frameDelay = true;
            return;
        }

        // Enter juggle state when the ball becomes airborne after a player kick.
        // Wall bounces do not reset WasKickedByPlayer, so juggle persists through redirects.
        if (!ball.IsTouchingGround && ball.WasKickedByPlayer)
        {
            ball.ChangeState(new SoccerBall_Juggle());
            return;
        }

        // Once the ball slows to a stop, return to idle.
        if (!ball.IsMoving)
            ball.ChangeState(new SoccerBall_Idle());
    }

    public override void Exit(BaseGameEntity entity)
    {
        
    }
}
