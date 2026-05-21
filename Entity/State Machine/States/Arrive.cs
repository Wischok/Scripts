using UnityEngine;

public class Arrive : State
{
    public override void Enter(BaseGameEntity entity)
    {
        var e = entity as MovingEntity;
        base.Enter(e); 
    }

    public override void Execute(BaseGameEntity entity)
    {
        var e = entity as MovingEntity;

        //implemtent arrive behavior
    }

    public override void Exit(BaseGameEntity entity)
    {
        var e = entity as MovingEntity;

        base.Exit(e); 
    }
}
