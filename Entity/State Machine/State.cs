using UnityEngine;

public class State
{
    //execute when entering the state
    public virtual void Enter(Player entity) { }
    public virtual void Enter(BaseGameEntity entity) { }

    //state update
    public virtual void Execute(Player entity) { }
    public virtual void Execute(BaseGameEntity entity) { }

    //execute when exiting the state
    public virtual void Exit(Player entity) { }
    public virtual void Exit(BaseGameEntity entity) { }

    /// Summary:
    ///     Called by StateMachine.ChangeState when this state is in the buffer at
    ///     transition time. Returns true if the conditions needed to enter this state
    ///     are still met. Default: always valid. Override in states that have specific
    ///     requirements that could become stale over the buffer's 10-frame lifetime
    ///     (e.g. PlayerJump requires the entity to be grounded).
    public virtual bool IsStillValid(BaseGameEntity entity) => true;
}
