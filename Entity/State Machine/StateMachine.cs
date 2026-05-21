using UnityEngine;

public class StateMachine
{
    ///
    /// ------- Owner References -------------------------------------------------------------
    ///

    private readonly BaseGameEntity m_owner;  // generic / AI entity
    private readonly Player         m_player; // player entity

    // Returns whichever owner is active. Player IS a BaseGameEntity so the upcast is implicit.
    private BaseGameEntity ActiveOwner => m_player != null ? m_player : m_owner;

    ///
    /// ------- States -----------------------------------------------------------------------
    ///

    private State m_currentState;
    private State m_previousState;

    public State GetCurrentState()  => m_currentState;
    public State GetPreviousState() => m_previousState;
    public State State              => m_currentState;

    ///
    /// ------- Input Buffer -----------------------------------------------------------------
    ///
    /// States that cannot be cancelled call BufferState() instead of ChangeState() when
    /// detecting valid input. The buffer holds the intended next state for up to
    /// _bufferTTL frames. On any ChangeState() call, if the buffer is live and the buffered
    /// state is still valid, the buffer is used instead of the requested transition.
    ///

    private State _bufferedState;
    private int   _bufferTTL;

    ///
    /// ------- Constructors -----------------------------------------------------------------
    ///

    public StateMachine(Player owner)         { m_player = owner; }
    public StateMachine(BaseGameEntity owner) { m_owner  = owner; }

    ///
    /// ------- Update -----------------------------------------------------------------------
    ///

    public void Update()
    {
        TickBuffer();

        if (m_owner == null)
        {
            m_currentState?.Execute(m_player);
            return;
        }

        m_currentState?.Execute(m_owner);
    }

    ///
    /// ------- State Transitions ------------------------------------------------------------
    ///

    /// Summary:
    ///     Transitions to newState, unless a live buffered state takes priority.
    ///     The buffer is always cleared after a ChangeState call (fired or discarded).
    public void ChangeState(State newState)
    {
        // A live, valid buffered state takes priority over the requested transition.
        if (_bufferedState != null && _bufferTTL > 0 && _bufferedState.IsStillValid(ActiveOwner))
        {
            State buffered = _bufferedState;
            ClearBuffer();
            TransitionTo(buffered);
            return;
        }

        ClearBuffer();
        TransitionTo(newState);
    }

    /// Summary:
    ///     Queues a state to fire on the next ChangeState() call.
    ///     Called by non-cancellable states when valid input is detected — the input
    ///     is held for up to ttlFrames and fires as soon as the current state ends.
    public void BufferState(State state, int ttlFrames = 10)
    {
        _bufferedState = state;
        _bufferTTL     = ttlFrames;
    }

    public void RevertToPreviousState() => ChangeState(m_previousState);

    // Legacy direct setters kept for compatibility.
    public void SetCurrentState(State state)  => m_currentState  = state;
    public void SetPreviousState(State state) => m_previousState = state;

    ///
    /// ------- Private Helpers --------------------------------------------------------------
    ///

    private void TransitionTo(State newState)
    {
        if (m_owner == null)
        {
            m_previousState = m_currentState;
            m_currentState?.Exit(m_player);
            m_currentState = newState;
            m_currentState?.Enter(m_player);
            return;
        }

        m_previousState = m_currentState;
        m_currentState?.Exit(m_owner);
        m_currentState = newState;
        m_currentState?.Enter(m_owner);
    }

    private void TickBuffer()
    {
        if (_bufferTTL <= 0) return;
        if (--_bufferTTL == 0)
            _bufferedState = null;
    }

    private void ClearBuffer()
    {
        _bufferedState = null;
        _bufferTTL     = 0;
    }
}
