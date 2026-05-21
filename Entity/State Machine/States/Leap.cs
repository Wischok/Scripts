using UnityEngine;

public class Leap : State
{
    private Transform m_target;
    private Vector2 m_landingPosition;
    private float m_leapDuration = 0.12f;
    private float m_timeElapsed = 0f;
    private float m_leapCooldown = 2.0f;
    

    //constructor
    public Leap(Transform target)
    {
        m_target = target;
    }

    public override void Enter(BaseGameEntity entity)
    {
        //base enter
        base.Enter(entity);

        //record landing position
        m_landingPosition = m_target.position;
    }

    public override void Execute(BaseGameEntity entity)
    {

        //distance to target [soccer ball]
        float distance = Vector2.Distance(entity.transform.position, m_target.position);

        //check if target was reached
        // if (distance < entity.GetAttackRange())
        // {            
        //     var victimEntity = EntityTeamManager.Instance.GetClosestEntityInTeam(entity, entity.HostileTeams);
            
        //     if(victimEntity.CompareTag("Minnow"))
        //     {
        //         victimEntity.GetComponent<Capybara>().LostBall();
        //         entity.ChangeState(new Pursuit());
        //         return;
        //     }
        // }

        if(m_timeElapsed >= m_leapDuration + m_leapCooldown)
        {
            entity.ChangeState(new Pursuit());
        }

        //leap towards landing position using SmoothDamp
        //(MovingEntity)entity.

        //increment time elapsed
        m_timeElapsed += Time.deltaTime;

        return;
    }

    public override void Exit(BaseGameEntity entity)
    {
        base.Exit(entity); 
    }
}
