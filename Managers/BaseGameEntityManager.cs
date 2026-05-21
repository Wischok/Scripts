using System.Collections.Generic;
using UnityEngine;

public class BaseGameEntityManager : MonoBehaviour
{
    //Dictionary of all entities
    private Dictionary<int, BaseGameEntity> m_entities = new ();


    //singleton
    private BaseGameEntityManager m_instance;
    public static BaseGameEntityManager Instance {get; private set;}

    private void Awake()
    {
        if(Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void RegisterEntity(BaseGameEntity entity)
    {
        m_entities.Add(entity.GetInstanceID(), entity);
    }

    public void DeRegisterEntity(BaseGameEntity entity)
    {
        m_entities.Remove(entity.GetInstanceID());
    }
    
    //get entity from id
    public BaseGameEntity GetEntityFromID(int id) {return m_entities[id];}
    //get closest entity to position
    public Transform GetClosestEntity(Vector2 position, BaseGameEntity requester)
    {
        Transform closestEntity = null;
        float closestDistance = float.MaxValue;

        foreach (var entity in m_entities.Values)
        {
            //skip self
            if (entity == requester) continue;

            //check if entity is SoccerBall
            if (entity.gameObject.tag == "SoccerBall")
            {
                float distance = Vector2.Distance(requester.transform.position, entity.transform.position);
                if( distance < closestDistance)
                {
                    closestDistance = distance;
                    closestEntity = entity.transform;
                }
            }
        }
        return closestEntity;
    }
}
