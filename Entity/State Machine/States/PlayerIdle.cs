using Unity.Mathematics;
using UnityEngine;

public class PlayerIdle : State
{
    public override void Enter(Player entity)
    {
    }

    public override void Execute(Player entity)
    {   
        //check inputs

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

        if(entity.InputHandler.Movement != Vector2.zero)
        {
            entity.ChangeState(new PlayerMove());
        }
    }

    public override void Exit(Player entity)
    {
    }
}
