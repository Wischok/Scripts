using Unity.Mathematics;
using UnityEngine;

public class PlayerMove : State
{
    public override void Enter(Player entity)
    {
    }

    public override void Execute(Player entity)
    {
        //check if player jumped
        if(entity.InputHandler.Jumped)
        {
            entity.ChangeState(new PlayerJump());
            return;
        }

        //check if player dashed
        if(entity.InputHandler.Dashed)
        {
            entity.ChangeState(new PlayerDash());
            return;
        }

        // Strike inputs route through the chain helper so fumble lockout is respected
        // even when starting a fresh strike (not just when chaining mid-strike).
        if (entity.TryChainStrikeInput(out State next))
        {
            entity.ChangeState(next);
            return;
        }

        //if entity is not touching the ground
        if(!entity.IsTouchingGround)
        {
            //change state to falling
            entity.StateMachine.ChangeState(new PlayerFall());
            return;
        }

        //check if still moving
        if(!entity.IsMoving && !(entity.InputHandler.Movement != Vector2.zero))
        {
            entity.ChangeState(new PlayerIdle());
            return;
        }

        //Update components
        entity.Move();
    }

    public override void Exit(Player entity)
    {
    }
}
