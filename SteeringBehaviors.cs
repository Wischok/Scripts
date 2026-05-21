using System.Collections.Generic;
using UnityEngine;

public static class SteeringBehaviors
{

    //seek
    public static Vector2 Seek(MovingEntity entity, Vector2 targetPosition)
    {
        // Vector2 desiredVelocity = (targetPosition - (Vector2)entity.transform.position).normalized * entity.GetMaxSpeed();
        // return desiredVelocity - entity.GetRigidbody2D().linearVelocity;
        return Vector2.zero;
    }

    //flee
    public static Vector2 Flee(MovingEntity entity, Vector2 threatPosition)
    {
        // Vector2 desiredVelocity = ((Vector2)entity.transform.position - threatPosition).normalized * entity.GetMaxSpeed();
        // return desiredVelocity - entity.GetRigidbody2D().linearVelocity;
        return Vector2.zero;
    }

    //pursuit
    public static Vector2 Pursuit(MovingEntity entity, MovingEntity target)
    {
        // Vector2 toTarget = (Vector2)target.transform.position - (Vector2)entity.transform.position;
        // float relativeHeading = Vector2.Dot(entity.GetHeadingDirection(), target.GetHeadingDirection());

        // if (Vector2.Dot(toTarget, entity.GetHeadingDirection()) > 0 && relativeHeading < -0.95f)
        // {
        //     return Seek(entity, target.transform.position);
        // }

        // float lookAheadTime = toTarget.magnitude / (entity.GetMaxSpeed() + target.GetRigidbody2D().linearVelocity.magnitude);
        // return Seek(entity, (Vector2)target.transform.position + target.GetRigidbody2D().linearVelocity * lookAheadTime);
        return Vector2.zero;
    }

    //pursuit for player controlled entities
    public static Vector2 Pursuit(MovingEntity entity, Player target)
    {
        Vector2 toTarget = (Vector2)target.transform.position - (Vector2)entity.transform.position;

        // float lookAheadTime = toTarget.magnitude / (entity.GetMaxSpeed() + target.GetRigidbody2D().linearVelocity.magnitude);
        // return Seek(entity, (Vector2)target.transform.position + target.GetRigidbody2D().linearVelocity * lookAheadTime);
        return Vector2.zero;
    }

    //pursuit for moving objects
    public static Vector2 Pursuit(MovingEntity entity, Transform target, Vector2 targetVelocity)
    {
        Vector2 toTarget = (Vector2)target.position - (Vector2)entity.transform.position;

        // float lookAheadTime = toTarget.magnitude / (entity.GetMaxSpeed() + targetVelocity.magnitude);
        // return Seek(entity, (Vector2)target.position + targetVelocity * lookAheadTime);
        return Vector2.zero;
    }

    //Arrive
    public static Vector2 Arrive(MovingEntity entity, Vector2 targetPosition, float slowingDistance = 1f)
    {
        Vector2 toTarget = targetPosition - (Vector2)entity.transform.position;
        float distance = toTarget.magnitude;

        if (distance > 0)
        {
            float speed = distance / slowingDistance;
            // speed = Mathf.Min(speed, entity.GetMaxSpeed());

            Vector2 desiredVelocity = toTarget.normalized * speed;
            // return desiredVelocity - entity.GetRigidbody2D().linearVelocity;
        }
        return Vector2.zero;
    }

    //Evade
    public static Vector2 Evade(MovingEntity entity, MovingEntity pursuer)
    {
        // Vector2 toPursuer = (Vector2)pursuer.transform.position - (Vector2)entity.transform.position;
        // float lookAheadTime = toPursuer.magnitude / (entity.GetMaxSpeed() + pursuer.GetRigidbody2D().linearVelocity.magnitude);
        // return -Seek(entity, (Vector2)pursuer.transform.position + pursuer.GetRigidbody2D().linearVelocity * lookAheadTime);
        return Vector2.zero;
    }

    //Wander
    public static Vector2 Wander(MovingEntity entity, ref float wanderAngle, float wanderRadius, float wanderDistance, float wanderJitter)
    {
        wanderAngle += Random.Range(-1f, 1f) * wanderJitter;

        // Vector2 circleCenter = entity.GetRigidbody2D().linearVelocity.normalized * wanderDistance;
        Vector2 displacement = new Vector2(Mathf.Cos(wanderAngle), Mathf.Sin(wanderAngle)) * wanderRadius;

        // Vector2 wanderForce = circleCenter + displacement;
        // return wanderForce;
        return Vector2.zero;
    }

    //wall avoidance
    public static Vector2 WallAvoidance(MovingEntity entity, LayerMask wallLayer, float feelerLength, Vector2 headingDirection)
    {
        Vector2[] feelers = new Vector2[3];
        Vector2 heading = headingDirection.normalized;

        feelers[0] = (Vector2)entity.transform.position + heading * feelerLength;
        feelers[1] = entity.transform.position + Quaternion.Euler(0, 0, 30) * heading * feelerLength * 0.7f;
        feelers[2] = entity.transform.position + Quaternion.Euler(0, 0, -30) * heading * feelerLength * 0.7f;

        foreach (var feelerEnd in feelers)
        {
            RaycastHit2D hit = Physics2D.Linecast(entity.transform.position, feelerEnd, wallLayer);
            if (hit.collider != null)
            {
                Vector2 overShoot = feelerEnd - hit.point;
                // return overShoot.magnitude * entity.GetWallAvoidanceMultiplier() * hit.normal;
            }
        }

        return Vector2.zero;
    }

    //separation
    public static Vector2 Separation (MovingEntity entity)
    {
        Vector2 steeringForce = Vector2.zero;

        //get neighbor entities apart if same team
        //List<BaseGameEntity> neighbors = EntityTeamManager.Instance.GetEntitiesInRadius(entity, entity.FriendlyTeams.ToArray());

        // foreach(var neighbor in neighbors)
        // {
        //     //direciton vector to neighbor
        //     Vector2 toAgent = (Vector2)neighbor.transform.position - (Vector2)entity.transform.position;

        //     //scale the force inversely propertional to the agents 
        //     // distance from its neighbor
        //     steeringForce += toAgent.normalized / toAgent.magnitude;
        // }

        return steeringForce;
    }
}
