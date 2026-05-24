using Unity.VisualScripting;
using UnityEngine;

public class PlayerJump : State
{
    public override void Enter(Player entity)
    {
        //jump function
        entity.Jump();
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

        //leave jump state if grounded
        if(entity.IsTouchingGround)
        {
            entity.RevertToPreviousState();
            return;
        }

        //if negative y velocity, transition to fall state
        if(entity.IsFalling)
        {
            entity.ChangeState(new PlayerFall());
            return;
        }

        //Update components
        entity.Move();
    }

    public override void Exit(Player entity)
    {
    }
}
