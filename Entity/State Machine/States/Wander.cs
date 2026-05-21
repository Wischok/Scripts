using UnityEngine;

public class Wander : State
{
    public override void Enter(BaseGameEntity entity)
    {
        var e = entity as MovingEntity;
    }

    public override void Execute(BaseGameEntity entity)
    {
        var e = entity as MovingEntity;

        //wander behavior
    }

    public override void Exit(BaseGameEntity entity)
    {
        var e = entity as MovingEntity;
    }
}
