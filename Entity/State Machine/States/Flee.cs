using UnityEngine;

public class Flee : State
{
    public override void Enter(BaseGameEntity entity)
    {
        var e = entity as MovingEntity;
    }

    public override void Execute(BaseGameEntity entity)
    {
        var e = entity as MovingEntity;
        
        //implement flee behaviour
    }

    public override void Exit(BaseGameEntity entity)
    {
        var e = entity as MovingEntity;
    }
}
