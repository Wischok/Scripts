/// Description:
///     This script is responsible for managing the position of the entities and objects in
///     the game world. It can be used independently with static objects to contain relative 
///     positioning information, including height, and how much space they take up. This is 
///     basically a radius of a circle they can stand within and a height to define a cylinder
///     of space they currently occupy.
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Transform))]
public class Position : MonoBehaviour
{
    ///
    /// -------- Inspector Variables ------------------------------------------------
    /// 
    [SerializeField] private float _groundedThreshold = 0.1f;//threshold for considering the entity grounded
    
    ///
    /// -------- Public Variables ---------------------------------------------------
    /// 
    
    // --- Position By Dimension ---
    public Vector2 Pos_2D => new (Pos_4D.x, Pos_4D.y + HEIGHT_PROJECTION_VECTOR *
        (Pos_4D.z + Pos_4D.w));
    public Vector3 Pos_3D => new (Pos_4D.x, Pos_4D.y, Pos_4D.z + Pos_4D.w);
    public Vector4 Pos_4D
    {
        get => _4D;
        set
        {
            //get new positional information and round it off to avoid strange interactions
            Vector4 new4D = new (value.x, value.y,
                Mathf.Round(Mathf.Max(value.z, 0f) * 1000f) * 0.001f,
                Mathf.Round(Mathf.Max(value.w, 0f) * 1000f) * 0.001f
            );

            //check to ensure the new position is valid and check for collisions
            CollisionManager.Instance.ValidateAndResolvePosition(this, ref new4D);

            //if distance from ground (z) has changed, update gravity state
            if (!Mathf.Approximately(_4D.z, value.z))
            {
                //feed vertical movement into elevation state tracking
                GravityState newState;
                if (new4D.z < _4D.z) newState = GravityState.Falling;
                else if (new4D.z > _4D.z) newState = GravityState.Rising;
                else newState = GravityState.Stable;

                //update elevation state
                UpdateGravityState(newState);
            }

            //check if elevation (w) has changed
            if (!Mathf.Approximately(_4D.w, value.w))
            {
                // Derive elevation direction from change
                ElevationState newState;
                if (new4D.w < _4D.w) newState = ElevationState.Descending;
                else if (new4D.w > _4D.w) newState = ElevationState.Ascending;
                else newState = ElevationState.Stable;

                UpdateElevationState(newState);
            }
            
            Vector3 prevCenter = _collision != null ? _collision.Center : Vector3.zero;
            TransformChanged = true;
            _4D = new4D;
            _transform.position = Pos_2D;

            //check if grounded state needs to be updated based on new position
            if(Pos_4D.z > _groundedThreshold)
                IsGrounded = false;
            else
            {
                //if close enough to ground, snap to it
                if (Pos_4D.z > 0)
                    _4D.z = 0;

                IsGrounded = true;
            }

            if (_collision != null)
            {
                _collision.ShiftSurfaces(Pos_3D - prevCenter);
                _collision.UpdateCenter(Pos_3D);
            }
        }
    }

    // Uses previous frame's elevation for projection — one frame lag is acceptable
    // since BeingStoodUpon snaps height to surface each frame, making it self-correcting
   
    // --- Elevation State Shorthand Properties ---
    public bool IsRising => _currentElevationState == ElevationState.Ascending;
    public bool IsFalling => _currentElevationState == ElevationState.Descending;
    public bool IsStable => _currentElevationState == ElevationState.Stable;

    // --- Gravity State Shorthand Properties ---
    public bool IsRisingGravity => _currentGravityState == GravityState.Rising;
    public bool IsFallingGravity => _currentGravityState == GravityState.Falling;
    public bool IsStableGravity => _currentGravityState == GravityState.Stable;
    public bool LandedThisFrame { get; private set; } = false;

    // --- Other Shorthand Properties ---
    public bool IsStatic { private set; get; } = true;

    public float DistanceFromGround => Pos_4D.z;

    public int SortingOrder => _spriteRenderer != null ? _spriteRenderer.sortingOrder : 0;

    public Vector4 PreviousPos_4D => _prev4D;

    public float BroadRadius => _collision != null ? _collision.BroadRadius : 0f;
    public Collision CollisionVolume => _collision;

    //whether transform has changed since last check or not
    public bool TransformChanged {private set; get;} = false;

    //has collision
    public bool HasCollision {private set; get;} = false;

    // Normal of the surface the entity is currently standing on. Defaults to world-up (0,0,1).
    public Vector3 GroundNormal { get; private set; } = new(0f, 0f, 1f);
    public void SetGroundNormal(Vector3 n) => GroundNormal = n.normalized;

    public bool IsGrounded
    {
        get => _isGrounded;
        set
        {
            if (value == _isGrounded)
            {
                _groundedFrameCounter = 0;
                return;
            }

            _groundedFrameCounter++;
            if (_groundedFrameCounter >= GROUNDED_FRAME_THRESHOLD)
            {
                _isGrounded = value;
                _groundedFrameCounter = 0;

                // If just became grounded 
                if (_isGrounded)
                {
                    //  snap to ground if close enough
                    if (Pos_4D.z > _groundedThreshold)
                        SetPosition(new Vector4(Pos_4D.x, Pos_4D.y, 0, Pos_4D.w));

                    //set landed flag for the frame (can be used for landing effects, etc.)
                    LandedThisFrame = true;
                }
                else // Just became ungrounded
                {
                    //if elevation is greater than 0, snap to air position to prevent hovering in place when stepping off a ledge
                    if (Pos_4D.w > 0)
                        SetPosition(new Vector4(Pos_4D.x, Pos_4D.y, Pos_4D.z + Pos_4D.w, 0));
                }
            }
        }
    }

    ///
    /// -------- Private Variables ---------------------------------------------------
    /// 
    
    // --- Base Position Dimension Positioning ---
    // _4D is serialized so the inspector values and editor-placed z/w survive domain reloads.
    // _prev4D is runtime-only (updated every frame) and intentionally not serialized.
    [SerializeField] private Vector4 _4D;
    private Vector4 _prev4D;

    // --- Elevation State (Changed by elevation (4d.w)) ---
    private ElevationState _currentElevationState = ElevationState.Stable;
    private ElevationState _previousElevationCandidate = ElevationState.Stable;//previous elevation state
    private int _elevationFrameCounter = 0;//counter for how many frames elevation has changed
    private const int ELEVATION_FRAME_THRESHOLD = 5;//elevation state change frame buffer

    // --- Gravity State (Changed by distance from ground (4d.z)) ---
    private GravityState _currentGravityState = GravityState.Stable;
    private GravityState _previousGravityCandidate = GravityState.Stable;//previous elevation state
    private int _gravityFrameCounter = 0;//counter for how many frames elevation has changed
    private const int GRAVITY_FRAME_THRESHOLD = 3;//elevation state change frame buffer

    // --- Height and Grounding ---
    private const float HEIGHT_PROJECTION_VECTOR = 0.5f;//projection vector for height to world height
    private bool _isGrounded = true;//if touching ground
    private int _groundedFrameCounter = 0;//counter for how many frames grounded state has changed
    private const int GROUNDED_FRAME_THRESHOLD = 3;//grounded state change frame buffer

    // --- Enums ---

    /// Summary:
    ///     The elevation states of the entity, used for tracking whether the
    ///     entity is rising, falling, or stable in elevation. This is used for various
    ///     checks and "forgiveness" buffers for determining entity height. For example, if
    ///     the entity is rising in elevation, its step tolerance for being considered
    ///     grounded on a floor plan is higher. This is supposed to be a bit of a prediction
    ///     in player intent and direction.
    private enum GravityState
    {
        Stable, //not moving vertically
        Rising, //moving upwards gravity wise
        Falling //moving downwards gravity wise
    }

    private enum ElevationState
    {
        Stable, //not moving vertically
        Ascending, //traveling upwards ground wise
        Descending //travelling downwards ground wise
    }

    ///
    /// -------- Components ---------------------------------------------
    /// 
    
    private Collision _collision;
    private Transform _transform;
    private SpriteRenderer _spriteRenderer;
    private SpriteRenderer _shadowSpriteRenderer;

    ///
    /// -------- Public Helper Functions ---------------------------------------------
    ///
    
    /// Summary:
    ///     Move position is the same as adding a position vector to the gameobject
    ///     in order to shift its positioning. If you want to set position, use
    ///     SetPosition.
    /// 
    /// Note: 
    ///     Can only be done with vector4 to properly track x,y,distancefromground,and
    ///     elevation properly     
    public void MovePosition(Vector4 shift)
    {
        //add the shift
        Pos_4D += shift;
    }

    public void MovePosition(Vector3 shift)
    {
        //create vector4 shift
        Vector4 newShift = new(shift.x, shift.y, shift.z, 0);

        MovePosition(newShift);
    }

    public void SetElevation(float value)
    {
        //create new vector4
        Vector4 newPos = new (Pos_4D.x, Pos_4D.y, Pos_4D.z, value);

        //set new position
        SetPosition(newPos);
    }

    /// Summary:
    ///     Set the position of the gameobject to the new position.
    public void SetPosition(Vector4 newPos)
    {
        //set position
        Pos_4D = newPos;
    }

    public void ForceSetGrounded(bool grounded)
    {
        _isGrounded = grounded;
        _groundedFrameCounter = 0;
    }

    public void SetSortingOrder(int order)
    {
        if (_shadowSpriteRenderer != null)
            _shadowSpriteRenderer.sortingOrder = order - 1;

        if (_spriteRenderer != null)
            _spriteRenderer.sortingOrder = order;
    }

    private bool _hasRecordedThisFrame = false;
    public void RecordPreviousFrameData()
    {
        if (_hasRecordedThisFrame) return;//prevent multiple recordings in the same frame

        _prev4D = Pos_4D;

        _hasRecordedThisFrame = true;
    }

    // Called at end of EntityPhysics.FixedUpdate to reset the guard
    public void ClearFrameFlags() 
    {
        _hasRecordedThisFrame = false;
        TransformChanged = false;
        LandedThisFrame = false;
    }

    ///
    /// -------- Private Functions --------------------------------------------------------
    /// 

    private void UpdateElevationState(ElevationState candidate)
    {
        if(!IsGrounded)
        {
            _elevationFrameCounter = 0;
            _previousElevationCandidate = ElevationState.Stable;
            _currentElevationState = ElevationState.Stable;
            return;
        }

        if (candidate == _currentElevationState)
        {
            _elevationFrameCounter = 0;
            _previousElevationCandidate = candidate;
            return;
        }

        // Candidate differs from current state
        if (candidate != _previousElevationCandidate)
        {
            // Direction changed again before threshold was reached — reset
            _elevationFrameCounter = 0;
            _previousElevationCandidate = candidate;
        }
        else
        {
            _elevationFrameCounter++;
            if (_elevationFrameCounter >= ELEVATION_FRAME_THRESHOLD)
            {
                _currentElevationState = candidate;
                _elevationFrameCounter = 0;
            }
        }
    }

    private void UpdateGravityState(GravityState candidate)
    {
        //if grounded, skip checking gravity state
        if(IsGrounded)
        {
            _gravityFrameCounter = 0;
            _previousGravityCandidate = GravityState.Stable;
            _currentGravityState = GravityState.Stable;
            return;
        }

        if (candidate == _currentGravityState)
        {
            _gravityFrameCounter = 0;
            _previousGravityCandidate = candidate;
            return;
        }

        // Candidate differs from current state
        if (candidate != _previousGravityCandidate)
        {
            // Direction changed again before threshold was reached — reset
            _gravityFrameCounter = 0;
            _previousGravityCandidate = candidate;
        }
        else
        {
            _gravityFrameCounter++;
            if (_gravityFrameCounter >= GRAVITY_FRAME_THRESHOLD)
            {
                _currentGravityState = candidate;
                _gravityFrameCounter = 0;
            }
        }
    }

    ///
    /// -------- Unity Functions -------------------------------------
    /// 

    
    private void Awake()
    {
        //check if collision script is attached, if so, save the component
        if(TryGetComponent(out Collision collision))
        {
            _collision = collision;
            HasCollision = true;
        }
        else
            HasCollision = false;

        //check if the object has a sprite renderer comp
        if(TryGetComponent(out SpriteRenderer sr))
            _spriteRenderer = sr;

        _transform = GetComponent<Transform>();

        // Seed x/y from the transform using the same reverse-projection as
        // SyncFromTransform so a non-zero serialized z/w doesn't corrupt y.
        _4D.x = _transform.position.x;
        _4D.y = _transform.position.y - HEIGHT_PROJECTION_VECTOR * (_4D.z + _4D.w);
    }

    private void Start()
    {
        //register with centralized sort manager
        if(IsStatic)
            CentralizedSortManager.Instance.RegisterObject(this, 
                GetComponent<SpriteRenderer>());
        else
            CentralizedSortManager.Instance.RegisterEntity(this, 
                GetComponent<SpriteRenderer>());

        var shadow = GetComponentInChildren<Shadow>();
        if(shadow != null)
            _shadowSpriteRenderer = shadow.GetComponent<SpriteRenderer>();
    }

    private void Reset()
    {
        //check if collision script is attached, if so, save the component
        if(TryGetComponent(out Collision collision))
        {
            _collision = collision;
            HasCollision = true;
        }
        else
            HasCollision = false;

        _transform = GetComponent<Transform>();

        //reset position to align with transform position
        Pos_4D = new Vector4(transform.position.x, transform.position.y, 0, 0);

        //check if the object has a MovingEntity component attached or a child of the moving entity script.
        if (GetComponent<MovingEntity>() != null || 
            GetComponentInParent<MovingEntity>() != null)
            IsStatic = false;
        else
            IsStatic = true;
    }

    private void OnValidate()
    {
        if (_collision == null)
        {
            if (TryGetComponent(out Collision col))
            {
                _collision = col;
                HasCollision = true;
            }
            else
                HasCollision = false;
        }

        if(_transform == null)
            _transform = GetComponent<Transform>();

        //check if the object has a MovingEntity component attached or a child of the moving entity script.
        if (GetComponent<MovingEntity>() != null ||
            GetComponentInParent<MovingEntity>() != null)
            IsStatic = false;
        else
            IsStatic = true;
    }

    ///
    /// -------- Editor --------------------------------------
    /// 

#if UNITY_EDITOR
    /// Summary:
    ///     Returns true if transform.position no longer matches the value Pos_4D projects to.
    ///     Used by PositionEditor to detect when the object is moved in the scene view.
    public bool TransformMovedExternally()
    {
        Vector2 expected = new(_4D.x, _4D.y + HEIGHT_PROJECTION_VECTOR * (_4D.z + _4D.w));
        return ((Vector2)transform.position - expected).sqrMagnitude > 0.0001f;
    }

    /// Summary:
    ///     Reverse-projects the current transform.position back into _4D x/y,
    ///     preserving z (distanceFromGround) and w (elevation) unchanged.
    ///     Writes directly to _4D to avoid the Pos_4D setter re-updating transform.
    public void SyncFromTransform()
    {
        Vector2 visual = (Vector2)transform.position;
        _4D.x = visual.x;
        _4D.y = visual.y - HEIGHT_PROJECTION_VECTOR * (_4D.z + _4D.w);
    }

    /// Summary:
    ///     Editor-safe position setter. Clamps z/w, shifts Custom collision surfaces by the
    ///     delta, and updates transform.position without going through the runtime Pos_4D
    ///     setter (which calls CollisionManager and reads _transform, both unavailable in
    ///     edit mode).
    public void SetPositionInEditor(Vector4 newPos, Collision col = null)
    {
        Vector4 clamped = new(
            newPos.x, newPos.y,
            Mathf.Round(Mathf.Max(newPos.z, 0f) * 1000f) * 0.001f,
            Mathf.Round(Mathf.Max(newPos.w, 0f) * 1000f) * 0.001f
        );

        if (col != null)
        {
            Vector3 oldCenter = col.Center;
            _4D = clamped;
            col.ShiftSurfaces(Pos_3D - oldCenter);
            col.UpdateCenter(Pos_3D);
        }
        else
        {
            _4D = clamped;
        }

        transform.position = Pos_2D;
    }
#endif
}
