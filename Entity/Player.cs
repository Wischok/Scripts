using UnityEngine;

[RequireComponent(typeof(InputHandler))]
public class Player : MovingEntity
{
    /// 
    /// ------ Inspector Variables ------------------------------------------
    ///
    [Header("--- Dash Settings ---")]//Dash specific variables and settings
    [field: SerializeField] public float DashDuration { get; private set; } = 0.3f;
    [field: SerializeField] public float DashCooldown { get; private set; } = 1f;

    [Header("--- Headbutt Settings ---")]
    [field: SerializeField] public float HeadbuttDuration { get; private set; } = 1f;

    [Header("--- Spin Kick Settings ---")]
    [field: SerializeField] public float SpinKickDuration { get; private set; } = 0.6f;

    [Header("--- Slam Down Settings ---")]
    [field: SerializeField] public float SlamDownDuration { get; private set; } = 0.8f;

    [Header("--- Strike Chaining ---")]

    // FixedUpdate frames that strike inputs are rejected after a mistimed (pre-cancel-window)
    // attempt. Punishes mashing so players are incentivized to wait for the cancel window.
    [field: SerializeField] public int FumbleLockoutFrames { get; private set; } = 10;

    /// 
    /// ------- Public Variables ------------------------------------
    /// 
    
    //player can power kick if they are currently dashing
    public override bool CanPowerKick => m_stateMachine.GetCurrentState() is PlayerDash;

    //helper variable for checking if player dashed in the previous state, used for headbutt logic
    public bool Dashed => m_stateMachine.GetPreviousState() is PlayerDash;

    //return whether the player collided with the ball this frame
    public bool CollidedWithBall => _kickHandler.CollidedWithBall;

    //public reference to input handler for player states to check for input
    public InputHandler InputHandler => _inputHandler;

    //public reference to ball interactions handler for player states to check for ball interactions
    public KickHandler KickHandler => _kickHandler;

    // True while a fumble penalty is active — strike inputs are rejected until this clears.
    public bool IsFumbleLocked => _fumbleLockoutRemaining > 0;

    ///
    /// ------- Player Components ------------------------------------------------
    /// 
    
    /// Summary:
    ///     Component references from parent classes:
    /// 
    ///     MovingEntity:
    ///         - EntityPhysics: _entityPhysics
    ///         - EntityAnimator: _entityAnimator
    /// 
    ///     BaseGameEntity:
    ///         - SpriteRenderer: _spriteRenderer

    /// Summary:
    ///     The input handler component for the player, used to retrieve player 
    ///     input for actions and state changes.
    /// 
    /// Note: referenced for player input values
    private InputHandler _inputHandler;

    // Frames remaining in the current fumble lockout. Ticked down each FixedUpdate.
    private int _fumbleLockoutRemaining = 0;

    ///
    /// ------- Unity Methods ------------------------------------
    ///
    
    protected override void Awake()
    {
        base.Awake();

        //initialize statemachine again
        m_stateMachine = new StateMachine((Player)this);

        if(TryGetComponent(out InputHandler inputHandler))
            _inputHandler = inputHandler;
        else
            Debug.LogError("Player is missing InputHandler component!");
    }

    protected override void Start()
    {
        base.Start();
        
        //set player state to idle
        ChangeState(new PlayerIdle());
    }

    protected override void FixedUpdate()
    {
        base.FixedUpdate();

        if (_fumbleLockoutRemaining > 0) _fumbleLockoutRemaining--;

        //clear frame flags for components
        _kickHandler.ClearFrameFlags();
    }
    ///
    /// ------- Actionable / State Methods ------------------------------------
    /// 
    
    /// Summary:
    ///     Accumulate the forces for player movement based on the
    ///     relative speed of the entity. It also sets the animations of
    ///     the entity for movement
    public void Move()
    {
        //add movement force
        //if entity is not touching ground, reduce speed to 20% in air
        AccumulateForce((IsTouchingGround? 1f : 0.2f) * _entityPhysics.RelativeSpeed * _inputHandler.Movement);
    }

    public void Jump() => AccumulateForce(EntityPhysics.Up * _entityPhysics.JumpForce, ForceMode2D.Impulse);

    public void Dash()
    {
        Vector2 dashDir = _kickHandler.PredictDirection(_inputHandler.Movement.normalized);
        _entityPhysics.EnterCruiseControl(
            new Vector3(
                dashDir.x * _entityPhysics.DashSpeed,
                dashDir.y * _entityPhysics.DashSpeed,
                0f
            )
        );
    }
    
    public void DashExit()
    {
        //exit cruise control with 20% of the dash speed as the new velocity. This is
        _entityPhysics.ExitCruiseControl(20f);
    }

    public void Headbutt()
    {
        // Stop player movement immediately when the headbutt starts so the hitbox is more likely to connect with the ball.
        _entityPhysics.SetVelocity(Vector3.zero);

        // Signal dash combo before the animator fires OnStrikeBegin so KickHandler
        // has the flag set when the first NextStrikeFrame detects a hit.
        _kickHandler.SetDashCombo(Dashed);

        _entityAnimator.SetBool("headbutt", true);
        AccumulateForce(EntityPhysics.Up * _entityPhysics.JumpForce, ForceMode2D.Impulse);
        // Detection is driven by Animator events routed through EntityAnimator:
        //   OnStrikeBegin(headbuttStrike) at the wind-up keyframe
        //   OnStrikeNextFrame             at each active swing keyframe
        //   OnStrikeEnd                   at the recovery keyframe
    }

    public void HeadbuttExit()
    {
        _entityAnimator.SetBool("headbutt", false);
    }

    public void SpinKickLeft()
    {
        _kickHandler.SetDashCombo(Dashed);
        _entityAnimator.SetBool("spinKickLeft", true);
    }

    public void SpinKickRight()
    {
        _kickHandler.SetDashCombo(Dashed);
        _entityAnimator.SetBool("spinKickRight", true);
    }

    public void SpinKickExit()
    {
        _entityAnimator.SetBool("spinKickLeft",  false);
        _entityAnimator.SetBool("spinKickRight", false);
    }

    public void SlamDown()
    {
        _kickHandler.SetDashCombo(Dashed);
        _entityAnimator.SetBool("slamDown", true);
        // Drive the player downward so the hitbox reaches the ball beneath them.
        AccumulateForce(EntityPhysics.Down * _entityPhysics.JumpForce, ForceMode2D.Impulse);
    }

    public void SlamDownExit()
    {
        _entityAnimator.SetBool("slamDown", false);
    }

    ///
    /// ------- Strike Chaining ------------------------------------------------
    ///

    /// Summary:
    ///     Central strike-input router used by every state that may begin a strike
    ///     (PlayerIdle / PlayerMove for fresh starts; strike states for chaining).
    ///
    ///     Reads strike-input flags and decides whether to transition. Behavior:
    ///       - No strike input: returns false; caller continues normal logic.
    ///       - Fumble lockout active: returns false; input is dropped (penalty in effect).
    ///       - No active strike: returns the requested strike state.
    ///       - Active strike in cancel window: returns the requested strike state
    ///         (caller transitions; current strike's Exit cleans up).
    ///       - Active strike NOT yet in cancel window: triggers fumble penalty and
    ///         returns false. Current strike continues unaffected.
    public bool TryChainStrikeInput(out State next)
    {
        next = null;

        State requested = null;
        if      (_inputHandler.Headbutted)      requested = new PlayerHeadbutt();
        else if (_inputHandler.SpinKickedLeft)  requested = new PlayerSpinKickLeft();
        else if (_inputHandler.SpinKickedRight) requested = new PlayerSpinKickRight();
        else if (_inputHandler.SlammedDown)     requested = new PlayerSlamDown();

        if (requested == null) return false;

        if (IsFumbleLocked) return false;

        if (!_kickHandler.HasActiveStrike)
        {
            next = requested;
            return true;
        }

        if (_kickHandler.IsInCancelWindow)
        {
            next = requested;
            return true;
        }

        TriggerFumble();
        return false;
    }

    /// Summary:
    ///     Engages the fumble lockout for FumbleLockoutFrames and fires the FX hook.
    ///     Strike inputs are rejected while IsFumbleLocked is true.
    public void TriggerFumble()
    {
        _fumbleLockoutRemaining = FumbleLockoutFrames;
        FXManager.Instance.OnFumble(_entityPhysics);
    }
}