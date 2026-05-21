///
/// Title: Applied Entity Physics
/// Description: Adds together forces applied to an entity
///     and produces a velocity utilizing Time.fixedDeltaTime. Utilize forcemode
///     impulse in order to add the full force at time of application
/// 
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Position))]
public class EntityPhysics : MonoBehaviour
{
    /// 
    /// ------- Inspector Variables ----------------------------------------------------------
    ///
    
    [Header("--- Movement Settings ---")]

    /// Summary: 
    ///     The relative speed of the entity
    [field: SerializeField]
    public float RelativeSpeed { get; set; } = 5f;

    /// Summary:
    ///     The max speed of the game object. A value of -1 means
    ///     there is no max speed
    [field: SerializeField]
    public float MaxSpeed { get; set; } = 25f;

    /// Summary:
    ///     The Jump force of the entity
    [field: SerializeField]
    public float JumpForce { get; set; } = 40f;
    
    /// Summary:
    ///     The Ground friction to be applied to the gameobject
    [field: SerializeField]
    public float GroundFriction { get; set; } = 0f;

    /// Summary:
    ///     The air friction to be applied to the game object
    [field: SerializeField]
    public float AirFriction { get; set; } = 0f;

    /// Summary:
    ///     The friction needed to enable sliding down slopes. This only comes into 
    ///     effect if the entity is on a sloped surface and they have NaturalKinematics enabled.
    [field: SerializeField] 
    public float SlopeForceCatalystThreshold { get; set; } = 1f;

    [Header("--- Ability Settings ---")]

    /// Summary:
    ///     The Dash force of the entity
    [field: SerializeField]
    public float DashSpeed { get; set; } = 60f;

    [Header("--- Physics Interaction Settings ---")]

    /// Summary:
    ///     The recoil rate of the entity. The higher the recoil, the higher
    ///     the entity will bounce off the floor when its height reaches 0.
    ///     Recoil rate works as a percentage. A recoil rate of 30 means 30% of
    ///     the downward velocity will be reflected back as a 'bounce'.
    [field: SerializeField][Min(0)]
    public int RecoilRate { get; set; } = 40;

    /// Summary:
    ///     A boolean for whether the entity reflects off of surfaces naturally or not
    [field: SerializeField]
    public bool NaturalKinematics { get; set; } = false;

    /// Summary:
    ///     The bounce threshold of the entity. It determines where the cut off is for
    ///     bouncing. A higher threshold means bounces stop sooner, where as a lower threshold
    ///     allows for more bounces.
    [field: SerializeField][Min(0)]
    private float BounceThreshold { get; set; } = 2f;

    /// Summary:
    ///     Fraction of velocity retained on each bounce (0–1). Only used when NaturalKinematics
    ///     is true. e.g. 0.7 means 30% energy is lost per bounce.
    [field: SerializeField][Range(0f, 1f)]
    private float BounceMultiplier { get; set; } = 0.7f;

    /// Summary:
    ///     The Gravity Modifier for the entity. This allows seperate control of gravity
    ///     outside of adjusting the entity mass.
    [field: SerializeField] private float _gravityModifier = 1f;

    /// Summary:
    ///     The mass of the object
    [field: SerializeField]
    private float _mass = 1f;

    ///
    /// ------- Public Variables -------------------------------------------------------------
    /// 
    
    /// Summary:
    ///     whether or not the entity is currently touching the ground
    public bool IsTouchingGround => _position.IsGrounded;

    /// Summary:
    ///     The current speed of the gameobject. Calculated by getting the
    ///     magnitude of the current velocity.
    public float Speed => LinearVelocity.magnitude;

    /// Summary:
    ///     A flag for whether the entity is moving or not
    public bool IsMoving => LinearVelocity.magnitude > 0.1f;

    /// Summary:
    ///     Check if the entity is falling or not
    public bool IsFalling => LinearVelocity.z < 0;

    /// Summary:
    ///    The down vector for the entity
    public static Vector3 Down => new(0, 0, -1);

    /// Summary:
    ///     gravity
    public static Vector3 Gravity => Down * 9.81f;

    /// Summary:
    ///     The up vector for the entity
    public static Vector3 Up => new(0, 0, 1);

    /// Summary:
    ///     The change in z position for the entity. This is used for the height manager and
    ///     is calculated in the UpdateVelocity function through velocity * time.
    ///     fixedDeltaTime
    public float DeltaZ { get; private set; } = 0f;

    //gravity modifier accessor
    public float GravityModifier
    {
        get => _gravityModifier;
        set
        {
            if(value < 1f) 
                _gravityModifier = 1f;
            else
                _gravityModifier = value;
        }
    }

    //entity mass
    public float Mass { get => _mass; set => _mass = value; }

    public Vector3 LinearVelocity
    {
        get => _linearVelocity;
        private set
        {
            //check if the entity has a max speed
            if(MaxSpeed < 0)
            {
                //if no max speed, set the value and continue
                _linearVelocity = value;
                return;
            }

            //if a max speed exists, ensure it is not exceeded
            if(value.magnitude > MaxSpeed)
            {
                _linearVelocity = value.normalized * MaxSpeed;
                return;
            }

            //check if the value is close enough to 0 that we can just set it to 0
            if(value.magnitude < 0.1)
            {
                _linearVelocity = Vector3.zero;
                return;
            }

            //set the value
            _linearVelocity = value;
        }
    }

    /// Summary:
    ///     The X component to the Vector 3 linear velocity. The variable
    ///     can be individually changed with an updated Vector3 taking the place
    ///     of the linear velocity
    public float LinearVelocityX
    {
        get
        {
            return LinearVelocity.x;
        }
        set
        {
            _linearVelocity.x = value;
        }
    }

    /// Summary:
    ///     The y component to the Vector 3 linear velocity. The variable
    ///     can be individually changed with an updated Vector3 taking the place
    ///     of the linear velocity
    public float LinearVelocityY
    {
        get
        {
            return LinearVelocity.y;
        }
        set
        {
            _linearVelocity.y = value;
        }
    }

    /// Summary:
    ///     The Z component to the Vector 3 linear velocity. The variable
    ///     can be individually changed with an updated Vector3 taking the place
    ///     of the linear velocity
    public float LinearVelocityZ
    {
        get
        {
            return LinearVelocity.z;
        }
        set
        {
            _linearVelocity.z = value;
        }
    }

    /// 
    /// ------ Private Variables -------------------------------------------------------------
    /// 

    /// Summary:
    ///     A queue of forces. An Accumulation of applied forces onto
    ///     the target during this cycle.
    ///
    private Queue<Vector3> linearForces = new();

    /// Summary:
    ///     The linear velocity of the attached gameobject
    private Vector3 _linearVelocity = Vector3.zero;

    /// Summary:
    ///     Set flag to freeze z velocity to allow for specific movements without falling or
    ///     rising
    private bool freezeZ = false;

    /// Summary:
    ///     Set flag as to whether the entity can process new forces or not
    private bool allowNewForces = true;

    /// Summary:
    ///     Whether the entity is currently in cruise control or not. Cruise control is a
    ///     state where the entity is set to a specific velocity for a period of time, and is
    ///     not allowed to be affected by new forces during that time. e.g. player dash.
    private bool cruiseControl = false;

    // Hitstop: short impact freeze (4-8 frames, ~5% velocity). Pure feel — forces blocked on ball.
    private int   _hitstopFrames      = 0;
    private float _hitstopTimeScale   = 0.05f;
    private bool  _hitstopBlockForces = false;

    // External velocity scale driven by ComboHandler (or any gameplay system that needs
    // to slow this entity without a timer). Hitstop overrides this while active.
    // Default 1 = no modification. ComboHandler sets ~0.3 while a combo window is open.

    ///
    /// ------- Components ------------------------------------------------------------------
    /// 

    /// Summary:
    ///     Entity Height Manager for determining entity height based on elevation from
    ///     floor plans and for checking whether the entity is touching the ground or not
    private Position _position;

    ///
    /// ------- Unity Methods ----------------------------------------------------------------
    /// 

    private void Awake()
    {

        if(TryGetComponent(out Position position))
            _position = position;
    }

    ///
    /// ------- Public Methods ---------------------------------------------------------------
    /// 

    /// Summary:
    ///     Add the given force to a queue of forces to be applied on
    ///     the next cycle
    /// 
    /// Parameters:
    ///     force: 
    ///         The force to be enqueued
    public void AddForce(Vector3 force) 
    {
        if(!allowNewForces) return;//if new forces are not allowed, do not add the force
        
        linearForces.Enqueue(force);//add force
    }

    /// Summary:
    ///     Add the given IMPULSE force to instantly to the velocity
    /// 
    /// Parameters:
    ///     force: 
    ///         The IMPULSE force to be enqueued
    public void AddForce(Vector3 force, ForceMode2D forceMode)
    {   
        if(!allowNewForces) return;//if new forces are not allowed, do not add the force

        //if the force is impulse, add to velocity
        if(forceMode == ForceMode2D.Impulse)
        {
            LinearVelocity += force / Mass;
            
            //if there is a positive z velocity, the entity has been given an
            //explosive upwards force that should register it as no longer grounded
            if(force.z > 0)
                _position.ForceSetGrounded(false);
        }
        else AddForce(force);//else, add traditional way
    }

    /// Summary:
    ///     Set the velocity of the entity directly, ignoring forces and resetting any prior
    ///     velocity. This is used for specific scenarios such as cruise control and specific
    ///     hits on ball entities.
    /// 
    /// Parameters:
    ///     velocity:
    ///         The velocity to set for the entity
    public void SetVelocity(Vector3 velocity)
    {
        LinearVelocity = velocity;
    }

    /// Summary (Percentage Version):
    ///     Set the velocity of the entity directly, ignoring forces and resetting any prior
    ///     velocity. This is used for specific scenarios such as cruise control and specific
    ///     hits on ball entities.
    /// 
    /// Parameters:
    ///     percentage:
    ///         A float from 0 to 1 representing the percentage of the entity's current 
    ///         speed to set the velocity to.
    public void SetVelocity(float percentage)
    {
        LinearVelocity *= Mathf.Clamp01(percentage);
    }

    /// Summary:
    ///     Update the linear velocity of the game object based on current net force.
    /// 
    /// Parameters:
    ///     velocity:
    ///         The current linear velocity of the game object. This value should change 
    ///         based on the given net force
    /// 
    /// A vector variable meant to be used in the following function. It is just a temporary
    /// storage variable for operations. It is added here to save cost on having to produce
    /// and remove a new variable every cycle;
    public void UpdateVelocity()
    {
        TickHitstop();

        // CheckFloorCollisions reads LandedThisFrame, which is set during the previous
        // frame's MovePosition. ClearFrameFlags must run AFTER so it isn't wiped first.
        CheckFloorCollisions();
        _position.ClearFrameFlags();

        //Process Collisions with surfaces
        ProcessGravity();
        UpdateFrictionAndDamping();

        //process added forces and update velocity accordingly
        ProcessForces();

        //update the position based on the new velocity and time
        float   tScale    = _hitstopFrames <= 0 ? MovementScale : _hitstopTimeScale;
        Vector3 frameMove = Time.fixedDeltaTime * tScale * LinearVelocity;

        // Sub-step fast movement to prevent tunneling. The frame's total displacement is
        // split into chunks no larger than the entity's bounding radius, so the swept
        // segment test in ValidateAndResolvePosition can never skip past geometry that is
        // at least as thick as the radius. Cap at 8 sub-steps to bound the cost.
        const float minStepSize = 0.05f;
        float       stepSize    = Mathf.Max(_position.BroadRadius, minStepSize);
        float       moveDist    = frameMove.magnitude;
        int         subSteps    = moveDist > 0.001f
                                    ? Mathf.Clamp(Mathf.CeilToInt(moveDist / stepSize), 1, 8)
                                    : 1;
        Vector3 stepVec = frameMove / subSteps;
        for (int s = 0; s < subSteps; s++)
            _position.MovePosition(stepVec);

        _position.RecordPreviousFrameData();
    }

    ///
    /// ------- Non Standard Physics Methods -------------------------------------------------
    /// 

    /// Summary:
    ///     Sets entity velocity to match an adjusted velocity vector fro cruise control
    ///     movement. This is used for consistent, unchanging movement, such as a player
    ///     dash. The prior velocity does not need to be stored. As long as the entity's
    ///     rigidbody is updated with the new velocity, the variable LinearVelocity can be
    ///     used to return the entity back to its prior velocity after exiting cruise control
    /// 
    /// Parameters:
    ///    velocity:
    ///        The velocity to set the entity to for cruise control movement. This should be
    ///        a fully calculated velocity vector.
    public void EnterCruiseControl(Vector3 velocity)
    {
        //enter cruise control with the given velocity
        SetVelocity(velocity);

        //set cruise control flag to true
        allowNewForces = false;
        cruiseControl = true;

        //clear any current forces to prevent interference with cruise control movement
        linearForces.Clear();
    }

    /// Summary:
    ///     Exit cruise control and allow for forces to be applied again.
    /// 
    /// Parameters:
    ///     speedToMaintain:
    ///         The percentage of speed to maintain after exiting cruise control. 
    ///         This is used to prevent sudden drops in speed after exiting cruise control
    ///         during specific scenarios. A value of 0 means no speed will be maintained.
    public void ExitCruiseControl(float speedToMaintain = 0f)
    {
        //if speed to maintain is greater than 100, set to 100
        if(speedToMaintain > 100f)
            speedToMaintain = 100f;

        //set linear velocity to zero
        SetVelocity(LinearVelocity * (speedToMaintain / 100f));

        //set cruise control flag to false
        allowNewForces = true;
        cruiseControl = false;
    }

    ///
    /// ------- Hitstop Methods --------------------------------------------------------------
    ///

    public bool  InHitstop     => _hitstopFrames > 0;
    public float MovementScale { get; set; } = 1f;

    /// Summary:
    ///     Enters a per-entity hitstop for the given number of FixedUpdate frames. The
    ///     position update is scaled to timeScale for the duration, creating a localised
    ///     freeze without affecting any other entity's physics.
    ///
    ///     If a hitstop is already running, the longer of the two durations wins so a
    ///     second hit never cuts an ongoing hitstop short.
    ///
    /// Parameters:
    ///     frames:      FixedUpdate ticks to hold the hitstop.
    ///     timeScale:   fraction of normal movement applied during hitstop (default 0.05).
    ///     blockForces: when true, new forces (including gravity) are rejected for the
    ///                  duration — use on the ball to preserve the kick trajectory.
    public void EnterHitstop(int frames, float timeScale = 0.05f, bool blockForces = false)
    {
        _hitstopFrames      = Mathf.Max(_hitstopFrames, frames);
        _hitstopTimeScale   = timeScale;
        _hitstopBlockForces = blockForces;
        if (blockForces && !cruiseControl)
            allowNewForces = false;
    }

    ///
    /// ------- Utility Methods --------------------------------------------------------------
    ///

    // Decrements the hitstop counter each FixedUpdate tick. Restores forces when the
    // counter reaches zero, unless cruise control is also active.
    private void TickHitstop()
    {
        if (_hitstopFrames <= 0) return;
        if (--_hitstopFrames == 0 && _hitstopBlockForces && !cruiseControl)
            allowNewForces = true;
    }

    /// Summary:
    ///     Calculate and return the net forces applied to the gameobject.
    ///     This also automatically clears the queue as they are dequeued.
    ///     
    /// Parameters:
    ///     forces:
    ///         The queue of forces to be dequeued and added into a total net force.
    ///         The object is passed by reference to clear the queue as it is worked
    ///         through.
    /// 
    /// Returns:
    ///     The global variable 'netForce' by reference after it is altered
    private Vector3 NetForce(Queue<Vector3> forces)
    {
        //clear out the net force before adding to it
        Vector3 netForce = Vector3.zero;

        //add each force to the net force
        while(forces.Count > 0)
            netForce += forces.Dequeue();

        //if the z velocity is frozen, remove any vertical forces and set z velocity to 0
        if(freezeZ)
            netForce.z = 0;

        return netForce;
    }

    private void ProcessGravity()
    {
        //if the entity is not touching the ground, apply gravity
        if (!IsTouchingGround)
            AddForce(GravityModifier * Mass * Gravity);
        else
        //if the entity is touching the ground and is moving downwards
        //set z velocity to 0 to prevent sinking into the ground
        {
            if(LinearVelocityZ < 0)
                LinearVelocityZ = 0;
        }
            
    }

    private void CheckFloorCollisions()
    {
        if (!IsTouchingGround) return;
        if (cruiseControl) return;

        Vector3 normal = _position.GroundNormal;

        if (_position.LandedThisFrame)
        {
            if (NaturalKinematics)
            {
                float bounceVelocity = Mathf.Abs(LinearVelocityZ) * BounceMultiplier;
                if (bounceVelocity >= BounceThreshold)
                    Bounce(bounceVelocity, normal);
                else
                    LinearVelocityZ = 0f;
                return;
            }
            
            if (RecoilRate > 0)
            {
                Bounce(Mathf.Abs(LinearVelocityZ) * (RecoilRate / 100f), normal);
                return;
            }
        }

        if (!NaturalKinematics) return;

        Vector3 slopeForce = Gravity - Vector3.Dot(Gravity, normal) * normal;
        if (slopeForce.magnitude <= GroundFriction + SlopeForceCatalystThreshold) return;
        Vector3 frictionReducedForce = slopeForce * (1f - Mathf.Clamp01(GroundFriction / slopeForce.magnitude));
        AddForce(frictionReducedForce);
    }

    /// Frictions:
    ///     slow down previous linear forces based on air friction and ground friction,
    ///     both of which are independent variables. Friction reduces the overall magnitude
    ///     of the linear velocity over time. A zero value indicates that no friction
    ///     should be applied, whereas a greater value is an increase in friction.
    /// 
    /// Note:
    ///     0 values result in no change, no if statement is needed
    private void UpdateFrictionAndDamping()
    {
        if(cruiseControl) return;//if in cruise control, do not apply friction or damping

        if (IsTouchingGround) 
        {
            //ground friction
            if(LinearVelocity.magnitude > 0.01f)
                LinearVelocity *= 1.0f / (1.0f + Time.fixedDeltaTime * GroundFriction);
        }
        //air friction
        else if(LinearVelocity.magnitude > 0.01f)
                LinearVelocity *= 1.0f / (1.0f + Time.fixedDeltaTime * AirFriction);
    }

    /// Forces:
    ///     Get the summation of all forces applied to entity and apply them to
    ///     the velocity.
    /// 
    /// Note:
    ///     Velocity = Sigma(Forces) * time.fixedDeltaTime / mass
    private void ProcessForces()
    {
        //add new forces if they exist
        if(linearForces.Count > 0)
            LinearVelocity += NetForce(linearForces) * Time.fixedDeltaTime / Mass;
    }

    /// Summary:
    /// Reflects the entity off of a surface with a given normal.
    ///     Parameters:
    ///         normal:
    ///             The normal vector of the surface to reflect off of. This should be a
    ///             normalized vector.
    private void ReflectEntity(Vector3 normal)
    {
        //reflect entity off the normal with the current speed as the magnitude of the reflection force
        Vector3 vector = Vector3.Reflect(LinearVelocity, normal.normalized);

        //ensure the reflection force doesn't exceed the current linear velocity
        if(vector.magnitude > LinearVelocity.magnitude)
            vector = vector.normalized * LinearVelocity.magnitude;

        //reset velocity before applying reflection force to the entity
        LinearVelocity = vector;
    }

    /// Summary:
    /// Reflects the entity off of a surface with a given normal.
    ///     Parameters:
    ///         normal:
    ///             The normal vector of the surface to reflect off of. This should be a normalized
    ///         magnitude:
    ///             The magnitude of the reflection force to apply.
    private void Bounce(float magnitude, Vector3 normal)
    {
        //reflect the entity off the ground normal with the given magnitude
        Vector3 vector = Vector3.Reflect(LinearVelocity, normal).normalized * magnitude;
        LinearVelocity = Vector3.zero;
        AddForce(vector, ForceMode2D.Impulse);
    }

    /// Summary:
    ///     Stops the x and y movement of the entity while allowing z movement to continue. 
    ///     This is used for 'sticking' to walls when not using natural reflection.
    private void SlideAlongWall(Vector3 wallNormal)
    {
        // work in XY only — Z is unaffected by wall collision
        Vector2 wallNormal2D = new(wallNormal.x, wallNormal.y);
        Vector2 velocity2D = new(LinearVelocityX, LinearVelocityY);

        //remove the component pushing into the wall, keep parallel copmonent
        Vector2 slideVeloicty = velocity2D - Vector2.Dot(velocity2D, wallNormal2D) * wallNormal2D;

        LinearVelocityX = slideVeloicty.x;
        LinearVelocityY = slideVeloicty.y;
    }
}
