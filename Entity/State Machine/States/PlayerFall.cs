using Unity.Mathematics;
using UnityEngine;

public class PlayerFall : State
{
    public override void Enter(Player entity)
    {
    }

    public override void Execute(Player entity)
    {
        //check if player dashed
        if(entity.InputHandler.Dashed)
        {
            entity.ChangeState(new PlayerDash());
            return;
        }

        //check if player headbutted
        if(entity.InputHandler.Headbutted)
        {
            entity.ChangeState(new PlayerHeadbutt());
            return;
        }

        //check if player spin kicked right
        if(entity.InputHandler.SpinKickedRight)
        {
            entity.ChangeState(new PlayerSpinKickRight());
            return;
        }

        //check if player spin kicked left
        if(entity.InputHandler.SpinKickedLeft)
        {
            entity.ChangeState(new PlayerSpinKickLeft());
            return;
        }

        //check if player slammed down
        if(entity.InputHandler.SlammedDown)
        {
            entity.ChangeState(new PlayerSlamDown());
            return;
        }

        //if no longer falling, return to previous state
        if(entity.IsTouchingGround)
        {
            entity.ChangeState(new PlayerMove());
            return;
        }
        
        entity.Move();
    }

    public override void Exit(Player entity)
    {
    }
}
