using UnityEngine;

public class Pursuit : State
{
    public override void Enter(BaseGameEntity entity)
    {
        var e = entity as MovingEntity;
    }

    public override void Execute(BaseGameEntity entity)
    {
        var e = entity as MovingEntity;

        Vector2 steeringForce = Vector2.zero;

        //determine target
        var target = e;//EntityTeamManager.Instance.GetClosestEntityInTeam(entity, entity.HostileTeams);

        //check if target is valid
        if (target == null)
        {
            return;
        }

        //get distance and direction to target
        Vector2 toTarget = target.transform.position - entity.transform.position;

        //leap if in range
        // if (toTarget.magnitude <= entity.GetAttackRange())
        // {
        //     entity.ChangeState(new Leap(target.transform));
        // }

        //ensure not exceeding max speed
        // if(e.GetRigidbody2D().linearVelocity.magnitude > e.GetMaxSpeed())
        // {
        //     e.GetRigidbody2D().linearVelocity = e.GetRigidbody2D().linearVelocity.normalized * e.GetMaxSpeed();
        // }
        
        //pursue the target
        if(target.CompareTag("SoccerBall"))
        {
            //pursue the soccer ball
            steeringForce = SteeringBehaviors.Pursuit(e, target.transform, target.GetComponent<Rigidbody2D>().linearVelocity);
            // steeringForce += SteeringBehaviors.Separation(e) * e.GetSeparationForce();        
            e.AccumulateForce(steeringForce);
            return;
        }

        /// Included steering behaviors:
        /// Pursuit
        /// Separation
        steeringForce = SteeringBehaviors.Pursuit(e, target.GetComponent<MovingEntity>());
        // steeringForce += SteeringBehaviors.Separation(e) * e.GetSeparationForce();
        e.AccumulateForce(steeringForce);
    }

    public override void Exit(BaseGameEntity entity)
    {
        var e = entity as MovingEntity;
    }
}