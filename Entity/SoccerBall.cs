using UnityEngine;

[RequireComponent(typeof(AudioSource))]
[RequireComponent(typeof(ComboHandler))]
public class SoccerBall : MovingEntity
{
    ///
    /// ------- Private Variables ------------------------------------------------------------
    ///

    // Surfaces whose normal has |z| above this value are treated as ramps/floors — no wall reflection.
    // 0 = reflect off everything; ~0.3 = skip gentle-to-moderate slopes; 1 = skip all surfaces.
    [SerializeField] private float _slopeReflectThreshold = 0.3f;

    [Header("--- Spin Settings ---")]

    // Lateral force applied each FixedUpdate during the spin (non-impulse, so units are
    // force/frame — tune alongside EntityPhysics mass and gravity).
    [SerializeField] private float _spinForce      = 15f;

    // How long the spin force holds at full strength before decaying.
    [SerializeField] private float _spinHoldTime   = 0.12f;

    // Multiplier applied to the spin force each FixedUpdate after the hold ends.
    // 0.65 = ~65% retained per tick — sharp falloff in ~8–10 frames.
    [SerializeField, Range(0f, 1f)] private float _spinDecayRate = 0.65f;

    // Spin force is cancelled once its magnitude drops below this threshold.
    [SerializeField] private float _spinStopThreshold = 0.5f;

    private AudioSource  _audioSource;
    private ComboHandler _comboHandler;

    // Runtime spin state — direction × current magnitude, decayed each tick.
    private Vector3 _currentSpinForce   = Vector3.zero;
    private float   _spinTimeElapsed    = 0f;
    private bool    _spinActive         = false;

    ///
    /// ------- Public Properties ------------------------------------------------------------
    ///

    public ComboHandler ComboHandler => _comboHandler;

    /// Summary:
    ///     True when the ball was last put into motion by a player kick (as opposed to a
    ///     wall reflection). Used by SoccerBall_Kicked to determine whether becoming
    ///     airborne should enter the juggle state.
    public bool WasKickedByPlayer { get; private set; }

    ///
    /// ------- Unity Methods ----------------------------------------------------------------
    ///

    protected override void Awake()
    {
        base.Awake();
        _audioSource  = GetComponent<AudioSource>();
        _comboHandler = GetComponent<ComboHandler>();
    }

    protected override void FixedUpdate()
    {
        TickSpin();
        base.FixedUpdate();
    }

    ///
    /// ------- Actionable Methods -----------------------------------------------------------
    ///

    /// Summary:
    ///     Kick the soccer ball in the provided direction and force.
    ///     Velocity is set directly (not accumulated) so existing momentum — e.g. a
    ///     falling ball's downward Z — never cancels or dilutes the kick force.
    ///     Spin types activate a decaying force perpendicular to the kick direction,
    ///     preserving the kick's Z elevation so spin combos layer with prior moves.
    public void Kick(Vector3 direction, float force, KickType kickType = KickType.Regular)
    {
        _entityPhysics.SetVelocity(direction * force);

        // Reset any prior spin before applying the new one.
        _currentSpinForce = Vector3.zero;
        _spinTimeElapsed  = 0f;
        _spinActive       = false;

        if (kickType == KickType.SpinLeft || kickType == KickType.SpinRight)
        {
            // Rotate the kick's XY component 90° for the lateral spin axis, then
            // reattach the kick's Z so the spin preserves any elevation from
            // preceding moves (headbutt height, prior spin, etc.).
            Vector2 kickXY = (Vector2)direction;
            if (kickXY.sqrMagnitude < 0.0001f) kickXY = Vector2.right;
            kickXY = kickXY.normalized;

            Vector3 spinDir = kickType == KickType.SpinLeft
                ? new Vector3(-kickXY.y,  kickXY.x, direction.z)  // CCW — left of kick
                : new Vector3( kickXY.y, -kickXY.x, direction.z); // CW  — right of kick

            _currentSpinForce = spinDir.normalized * _spinForce;
            _spinActive       = true;
        }

        WasKickedByPlayer = true;
        _audioSource.Play();
        ChangeState(new SoccerBall_Kicked());
    }

    // Applies the lateral spin force each FixedUpdate. Holds at full strength for
    // _spinHoldTime seconds, then decays geometrically until magnitude drops below
    // the stop threshold. Called before base.FixedUpdate() so the force is already
    // queued when EntityPhysics.ProcessForces() runs this same tick.
    private void TickSpin()
    {
        if (!_spinActive) return;

        _entityPhysics.AddForce(_currentSpinForce);

        _spinTimeElapsed += Time.fixedDeltaTime;
        if (_spinTimeElapsed >= _spinHoldTime)
        {
            _currentSpinForce *= _spinDecayRate;
            if (_currentSpinForce.magnitude < _spinStopThreshold)
            {
                _currentSpinForce = Vector3.zero;
                _spinActive       = false;
            }
        }
    }

    ///
    /// ------- Collision -------------------------------------------------------------------
    ///

    protected override void CollisionEnter(CollisionEvent e) => Reflect(e);
    protected override void CollisionStay(CollisionEvent e)  => Reflect(e);

    // The bidirectional event fires with HitNormal.Flipped(), so the normal points INTO
    // the surface rather than outward. With a flipped normal, Dot(v_toward_wall, n) > 0,
    // so we skip when dot <= 0 (already moving away) and reflect when dot > 0 (toward wall).
    // Wall bounces do NOT reset WasKickedByPlayer — juggle persists through redirects.
    private void Reflect(CollisionEvent e)
    {
        if (!e.ObjectPosition.gameObject.CompareTag("Object")) return;

        Vector3 velocity = _entityPhysics.LinearVelocity;
        Vector3 normal   = e.HitNormal.surfaceNormal.normalized;

        if (Mathf.Abs(e.HitNormal.trueNormal.z) > _slopeReflectThreshold) return;

        float dot = Vector3.Dot(velocity, normal);
        if (dot <= 0f) return;

        _entityPhysics.SetVelocity(velocity - 2f * dot * normal);
    }
}
