using UnityEngine;

public class PlayerDash : State
{
    private float _timeElapsed = 0f;

    public override void Enter(Player entity)
    {
        entity.Dash();
    }

    public override void Execute(Player entity)
    {
        //check if player jumped
        if(entity.InputHandler.Jumped)
        {
            entity.ChangeState(new PlayerJump());
            return;
        }

        //check if player headbutted
        if(entity.InputHandler.Headbutted)
        {
            entity.ChangeState(new PlayerHeadbutt());
            return;
        }
        
        //if player dash duration has elapsed, update states
        if (_timeElapsed >= entity.DashDuration)
        {
            //if player input, change to moving state, otherwise idle
            if (entity.InputHandler.Movement.magnitude > 0.1f)
                entity.ChangeState(new PlayerMove());
            else
                entity.ChangeState(new PlayerIdle());
        }

        //if collides with soccerball, change to move or idle state
        if(entity.CollidedWithBall)
        {
            if (entity.InputHandler.Movement.magnitude > 0.1f)
                entity.ChangeState(new PlayerMove());
            else
                entity.ChangeState(new PlayerIdle());
        }

        _timeElapsed += Time.deltaTime;
    }

    public override void Exit(Player entity)
    {
        //end dash function
        entity.DashExit();
    }
}
