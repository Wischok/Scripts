using System;
using UnityEngine;

[RequireComponent(typeof(EntityPhysics))]//for movement and physics
[RequireComponent(typeof(EntityAnimator))]//for animations
public class MovingEntity : BaseGameEntity
{ 
    ///
    /// ------- Public Variables ------------------------------------------
    /// 

    [SerializeField] private GameObject _shadow;

    /// Entity Physics Variables
    public bool IsMoving => _entityPhysics.IsMoving;
    public Vector2 Velocity => _entityPhysics.LinearVelocity;
    public bool IsFalling => _entityPhysics.LinearVelocity.y < 0;
    
    /// Height Manager Variables
    public bool IsTouchingGround => _entityPhysics.IsTouchingGround;//entity is falling

    /// Entity kick variables
    /// Whether entity can power kick or not. Defined in individual entity classes
    public virtual bool CanPowerKick => false;

    public EntityPhysics Physics => _entityPhysics;

    ///
    /// ------- Entity Components ------------------------------------------------------------
    /// 

    /// Summary: 
    ///     The Sorting transform position of the entity. Used to dynamically sort the entity
    ///     based on its transform positions (x & y) and the entity's height, if it 
    ///     has an attached physics component.
    protected EntityPhysics _entityPhysics;

    /// Summary:
    ///     The animator component for the entity, rather than the
    ///     traditional component.
    protected EntityAnimator _entityAnimator;

    /// 
    /// -------- Unity Methods ---------------------------------------------------------------
    /// 

    protected override void Awake()
    {
        //base awake
        base.Awake();

        //grab components
        if(TryGetComponent(out EntityAnimator animator))
            _entityAnimator = animator;

        //check if the entity has an attached physics component
        if(TryGetComponent(out EntityPhysics physics))
            _entityPhysics = physics;
    }

    protected override void Start()
    {
        base.Start();

        if(_shadow != null)
        {
            Shadow s = _shadow.AddComponent<Shadow>();
            s.SetPosition(_position);
        }
            
    }

    protected override void FixedUpdate()
    {
        base.FixedUpdate();

        //update entity physics and animations
        
        _entityPhysics.UpdateVelocity();
        _entityAnimator.UpdateAnimations();
    }

    ///
    /// --------- Physics Methods -----------------------------------------------------------
    /// 

    /// Summary:
    ///     Adds the given force to the entity physics system.
    /// 
    /// Parameters:
    ///     force:
    ///         The force to be added
    public virtual void AccumulateForce(Vector3 force) => _entityPhysics.AddForce(force);

    /// Summary:
    ///     Adds the given force to the entity physics system with a forcemode
    /// 
    /// Parameters:
    ///     force:
    ///         The force to be added
    public virtual void AccumulateForce(Vector3 force, ForceMode2D forceMode) => _entityPhysics.AddForce(force, forceMode);
}