using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Position))]
public class BaseGameEntity : MonoBehaviour
{
    /// 
    /// State machine variables
    /// 
    protected StateMachine m_stateMachine;
    public StateMachine StateMachine => m_stateMachine;//retrieve statemachine

    /// Summary:
    ///     The sprite renderer component of the entity, used to adjust the sorting position
    ///     of the entity
    protected SpriteRenderer _spriteRenderer;
    public SpriteRenderer SpriteRenderer => _spriteRenderer;

    /// Summary:
    ///     The position component of the entity, used to adjust the world and sorting
    ///     position of the entity
    protected Position _position;
    public Position Position => _position;

    /// Summary:
    ///     Reference to the KickHandler for this entity, cached on Awake.
    ///     Used by subclasses to forward collision events for kick handling.
    protected KickHandler _kickHandler;

    /// 
    /// --------- Unity Methods ----------------------------------------------
    /// 

    protected virtual void Awake()
    {
        //initialize state machine
        m_stateMachine = new StateMachine(this);

        //get sprite renderer component
        _spriteRenderer = GetComponent<SpriteRenderer>();

        //get position component
        _position = GetComponent<Position>();

        //cache kick handler if one exists on this entity
        TryGetComponent(out _kickHandler);

        // Event subscription is handled entirely by OnEnable/OnDisable so that
        // enable/disable cycles stay balanced. Do not subscribe here.
    }

    protected virtual void Start()
    {
        //register to the entity manager
        BaseGameEntityManager.Instance.RegisterEntity(this);
    }

    protected virtual void OnEnable()
    {
        if (!TryGetComponent(out Collision col)) return;

        if (col.IsTrigger)
        {
            col.OnTriggerEnter += TriggerEnter;
            col.OnTriggerStay  += TriggerStay;
            col.OnTriggerExit  += TriggerExit;
        }
        else
        {
            col.OnCollisionEnter += CollisionEnter;
            col.OnCollisionStay  += CollisionStay;
            col.OnCollisionExit  += CollisionExit;
        }
    }

    protected virtual void OnDisable()
    {
        if (!TryGetComponent(out Collision col)) return;

        col.OnTriggerEnter   -= TriggerEnter;   col.OnCollisionEnter -= CollisionEnter;
        col.OnTriggerStay    -= TriggerStay;    col.OnCollisionStay  -= CollisionStay;
        col.OnTriggerExit    -= TriggerExit;    col.OnCollisionExit  -= CollisionExit;
    }

    protected virtual void FixedUpdate()
    {
        //update state machine
        m_stateMachine.Update();
    }

    protected virtual void Die()
    {
        //future extra stuff

        //remove from entity manager
        BaseGameEntityManager.Instance.DeRegisterEntity(this);
        Destroy(gameObject);//destroy
    }

    /// 
    /// Class Methods
    /// 

    protected virtual void TriggerEnter(CollisionEvent e)   {}
    protected virtual void TriggerStay(CollisionEvent e)    {}
    protected virtual void TriggerExit(CollisionEvent e)    {}
    protected virtual void CollisionEnter(CollisionEvent e) {}
    protected virtual void CollisionStay(CollisionEvent e)  {}
    protected virtual void CollisionExit(CollisionEvent e)  {}

    //change state
    public void ChangeState(State newState) => m_stateMachine.ChangeState(newState);

    // Queues a state to fire on the next ChangeState() call, for use in non-cancellable
    // states. The buffer expires after ttlFrames if no transition fires.
    public void BufferState(State state, int ttlFrames = 10) => m_stateMachine.BufferState(state, ttlFrames);

    //revert to previous state
    public void RevertToPreviousState() => m_stateMachine.RevertToPreviousState();

    private void OnDrawGizmosSelected()
    {
        DrawGizmos();
    }

    protected virtual void DrawGizmos()
    {
        
    }
}
