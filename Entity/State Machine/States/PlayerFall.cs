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
