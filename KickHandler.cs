using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Position))]
[RequireComponent(typeof(EntityPhysics))]
public class KickHandler : MonoBehaviour
{
    ///
    /// ------- Nested Types -----------------------------------------------------------------
    ///

    [System.Serializable]
    private struct StrikeBinding
    {
        public Strike   Strike;
        public float    BaseForce;
        [Range(0, 90)] public int BaseAngle;
        public KickType KickType;
    }

    ///
    /// ------- Inspector Variables ----------------------------------------------------------
    ///

    [Header("--- Kick Settings ---")]

    [SerializeField] private float minKickForce    = 5f;
    [SerializeField] private float maxKickForce    = 20f;
    [SerializeField] private float forceMultiplier = 1.2f;

    [Header("--- Strike Bindings ---")]

    [SerializeField] private List<StrikeBinding> _strikeBindings = new();

    [Header("--- Combo ---")]

    // Radius used in BeginStrike to find nearby balls whose combo windows should be extended.
    [SerializeField] private float _comboScanRadius = 3f;

    // Extra force added to a strike when the entity was dashing immediately before it.
    // Compensates for the dash kick that was missed and combines both into the headbutt.
    [SerializeField] private float _dashComboBonus = 15f;

    // Normalized force (0..1, mapped from min/maxKickForce) above which a collision
    // kick counts as a "power hit" and triggers hitstop / combo window / FX.
    [SerializeField, Range(0f, 1f)] private float _powerHitThreshold = 0.6f;

    // After a strike connects, suppress physical collision response between this entity
    // and the struck ball for this many FixedUpdate frames. Prevents the dashing entity
    // from chasing the freshly-launched ball and disrupting its trajectory.
    [SerializeField] private int _strikeIgnoreFrames = 15;

    // HitProperty attributed to a power dash kick (CollisionEnter path, not an animation
    // strike). Defaults to Launcher because dash kicks typically launch a grounded ball.
    [SerializeField] private HitProperty _dashKickProperty = HitProperty.Launcher;

    [Header("--- Prediction ---")]

    // How far to scan for a ball when skewing the dash/strike direction.
    [SerializeField] private float _predictionRadius = 5f;

    // Only balls within this angle of the input direction are considered.
    [SerializeField, Range(0f, 90f)] private float _predictionMaxAngle = 60f;

    // How far to lean the input direction toward the nearest qualifying ball (0 = off, 1 = snap).
    [SerializeField, Range(0f, 1f)] private float _predictionBlend = 0.35f;

    ///
    /// -------- Public Variables ------------------------------------------------------------
    ///

    public bool CollidedWithBall { get; private set; } = false;
    public bool HasActiveStrike  => _activeStrike != null;

    // True once the active strike has advanced past its CancelFromFrameIndex, meaning
    // a new strike input may interrupt this one cleanly. False before the window opens
    // (player must wait or fumble) and false when the strike has no configured window.
    public bool IsInCancelWindow
        => _activeStrike != null
        && _activeStrike.CancelFromFrameIndex >= 0
        && _frameIndex >= _activeStrike.CancelFromFrameIndex;

    ///
    /// -------- Components ------------------------------------------------------------------
    ///

    private Position      _position;
    private EntityPhysics _ep;
    private Collision     _collision;

    ///
    /// ------- Strike Runtime State ---------------------------------------------------------
    ///

    private Strike      _activeStrike;
    private StrikeFrame _currentFrame;

    // Counts NextStrikeFrame advances within the active strike. 0 at BeginStrike,
    // increments on each NextStrikeFrame call. Reset on EndStrike. Used by
    // IsInCancelWindow to gate strike-to-strike chaining.
    private int _frameIndex;

    // Normalized XY direction the entity was facing at BeginStrike.
    // The XY components of each frame's LocalOffset are rotated by this direction so
    // hitboxes track the attack regardless of which way the entity is facing.
    // Open item: currently derived from _ep.LinearVelocity.xy — replace with the
    // project's authoritative facing vector once one is established.
    private Vector2 _facingDir;

    private readonly HashSet<int>   _ballsHit      = new();
    private readonly List<Position> _overlapBuffer = new();

    private Dictionary<Strike, StrikeBinding> _bindingLookup;

    // Set by the owning entity before BeginStrike is called. Cleared on EndStrike.
    private bool _isDashCombo = false;

    ///
    /// ------- Public Helpers ---------------------------------------------------------------
    ///

    public void ClearFrameFlags()
    {
        CollidedWithBall = false;
    }

    /// Summary:
    ///     Predictive check used by CollisionManager.ResolveCollision before applying the
    ///     physical push response. Returns true if a contact between this entity and the
    ///     given ball, on this frame, would be processed by the dash-kick path as a power
    ///     kick. CollisionManager skips the push when this is true so the contact-frame
    ///     bounce doesn't disrupt the about-to-fire kick.
    ///
    ///     Mirrors the gating logic in CollisionEnter exactly: not mid-strike, and
    ///     normalized force from current velocity meets _powerHitThreshold.
    public bool WouldHitBeAPowerKick(SoccerBall ball)
    {
        if (HasActiveStrike) return false;
        if (ball == null)    return false;

        float force           = Mathf.Clamp(_ep.LinearVelocity.magnitude * forceMultiplier, minKickForce, maxKickForce);
        float normalizedPower = Mathf.InverseLerp(minKickForce, maxKickForce, force);
        return normalizedPower >= _powerHitThreshold;
    }

    /// Summary:
    ///     Signals that the next strike should apply a dash combo bonus force.
    ///     Call this before the animator fires OnStrikeBegin — Player.Headbutt()
    ///     is the right call site, passing Player.Dashed as the value.
    public void SetDashCombo(bool value) => _isDashCombo = value;

    /// Summary:
    ///     Skews inputDir toward the nearest soccer ball within the prediction cone.
    ///     Used to nudge dash velocity and strike facing direction toward a nearby ball.
    ///     Returns inputDir unchanged if no ball qualifies.
    public Vector2 PredictDirection(Vector2 inputDir)
    {
        if (inputDir.sqrMagnitude < 0.01f) return inputDir;

        Vector2 inputNorm = inputDir.normalized;
        float   cosThresh = Mathf.Cos(_predictionMaxAngle * Mathf.Deg2Rad);

        float   bestSqDist = float.MaxValue;
        Vector2 bestDir    = Vector2.zero;
        bool    found      = false;

        _overlapBuffer.Clear();
        CollisionManager.Instance.OverlapSphere(_position.Pos_3D, _predictionRadius, _overlapBuffer);

        foreach (Position candidate in _overlapBuffer)
        {
            if (!candidate.TryGetComponent<SoccerBall>(out _)) continue;

            // Compare in the horizontal plane only (XY); ignore height differences.
            Vector2 toCandidate = (Vector2)(candidate.Pos_3D - _position.Pos_3D);
            float   sqDist      = toCandidate.sqrMagnitude;
            if (sqDist < 0.01f) continue;

            Vector2 dirToCandidate = toCandidate / Mathf.Sqrt(sqDist);
            if (Vector2.Dot(inputNorm, dirToCandidate) < cosThresh) continue;

            if (sqDist < bestSqDist)
            {
                bestSqDist = sqDist;
                bestDir    = dirToCandidate;
                found      = true;
            }
        }

        if (!found) return inputDir;

        return Vector2.Lerp(inputNorm, bestDir, _predictionBlend).normalized;
    }

    ///
    /// ------- Unity Methods ----------------------------------------------------------------
    ///

    private void Awake()
    {
        if (TryGetComponent(out Position pos))
            _position = pos;

        if (TryGetComponent(out EntityPhysics ep))
            _ep = ep;

        if (TryGetComponent(out Collision col))
            _collision = col;

        _bindingLookup = new Dictionary<Strike, StrikeBinding>(_strikeBindings.Count);
        foreach (StrikeBinding b in _strikeBindings)
        {
            if (b.Strike != null)
                _bindingLookup[b.Strike] = b;
        }
    }

    private void OnEnable()
    {
        if (_collision != null && !_collision.IsTrigger)
            _collision.OnCollisionEnter += CollisionEnter;
    }

    private void OnDisable()
    {
        if (_collision != null && !_collision.IsTrigger)
            _collision.OnCollisionEnter -= CollisionEnter;
    }

    ///
    /// ------- Public Strike Methods --------------------------------------------------------
    ///

    /// Summary:
    ///     Starts a strike sequence. Sets the current frame to the strike's first frame
    ///     (wind-up anchor), captures the entity's facing direction for offset rotation,
    ///     and clears the per-strike hit registry. No hit detection occurs on this call —
    ///     the first frame serves as the origin of the first swept segment.
    ///     Called by EntityAnimator.OnStrikeBegin via animation event.
    public void BeginStrike(Strike strike)
    {
        if (!_bindingLookup.ContainsKey(strike))
        {
            Debug.LogError($"[KickHandler] BeginStrike: no binding for Strike '{strike.name}'. Add it to the Strike Bindings list.");
            return;
        }

        _activeStrike = strike;
        _currentFrame = strike.FirstFrame;
        _frameIndex   = 0;
        _ballsHit.Clear();

        // Derive facing from horizontal velocity then skew toward the nearest ball in range.
        Vector2 vel2D = _ep.LinearVelocity;
        _facingDir = vel2D.magnitude > 0.1f ? vel2D.normalized : Vector2.right;
        _facingDir = PredictDirection(_facingDir);

        // Extend the combo window on any ball in range that already has one open.
        // This gives the strike animation time to complete before the window expires.
        _overlapBuffer.Clear();
        CollisionManager.Instance.OverlapSphere(_position.Pos_3D, _comboScanRadius, _overlapBuffer);
        foreach (Position candidate in _overlapBuffer)
        {
            if (candidate.TryGetComponent<ComboHandler>(out var combo) && combo.InComboWindow)
                combo.ExtendWindow();
        }
    }

    /// Summary:
    ///     Advances to the next frame and runs swept-sphere hit detection between the
    ///     previous and current frame world positions. The sweep covers the full arc
    ///     traveled between the two frames, so fast-moving balls cannot tunnel through.
    ///
    ///     With N strike frames: N-1 sweeps fire, which is by design — the first frame
    ///     is the wind-up anchor and produces no backward detection; each subsequent call
    ///     checks the space traveled since the last frame.
    ///     Called by EntityAnimator.OnStrikeNextFrame via animation event.
    public void NextStrikeFrame()
    {
        if (!HasActiveStrike) return;

        StrikeFrame prevFrame = _currentFrame;
        _currentFrame = _currentFrame.NextFrame;
        _frameIndex++;

        if (_currentFrame == null)
        {
            EndStrike();
            return;
        }

        // prevFrame == null would mean the head frame had no position — shouldn't happen
        // with a well-formed chain, but guard defensively.
        if (prevFrame == null) return;

        Vector3 segA = FrameWorldPos(prevFrame);
        Vector3 segB = FrameWorldPos(_currentFrame);

        // Broad-phase: sphere at the midpoint large enough to enclose the swept capsule.
        Vector3 midpoint    = (segA + segB) * 0.5f;
        float   segHalfLen  = Vector3.Distance(segA, segB) * 0.5f;
        float   broadRadius = _currentFrame.Radius + segHalfLen;

        _overlapBuffer.Clear();
        CollisionManager.Instance.OverlapSphere(midpoint, broadRadius, _overlapBuffer);

        if (!_bindingLookup.TryGetValue(_activeStrike, out StrikeBinding binding)) return;

        foreach (Position candidate in _overlapBuffer)
        {
            if (!candidate.TryGetComponent<SoccerBall>(out SoccerBall ball)) continue;

            int ballId = ball.gameObject.GetInstanceID();
            if (_ballsHit.Contains(ballId)) continue;

            float ballRadius   = candidate.CollisionVolume != null ? candidate.CollisionVolume.Radius : 0f;
            float hitThreshold = _currentFrame.Radius + ballRadius;
            float sqDist       = SqDistPointSegment(segA, segB, ball.Position.Pos_3D);

            if (sqDist > hitThreshold * hitThreshold) continue;

            _ballsHit.Add(ballId);

            // HitProperty gate: even with geometric contact, the strike only connects if
            // the ball's air state matches the strike's combo role. A whiff consumes the
            // swing for this ball (already added to _ballsHit) but applies no kick or FX.
            if (!HitPropertyRules.IsValid(_activeStrike.Property, ball))
            {
                FXManager.Instance.OnWhiff(_ep, ball.Physics, _activeStrike.Property);
                continue;
            }

            // Quality is measured at the current frame's world position, not along the segment.
            float d     = Vector3.Distance(ball.Position.Pos_3D, FrameWorldPos(_currentFrame));
            float rs    = _currentFrame.SweetSpotRadius;
            float ro    = _currentFrame.Radius;
            float range = ro - rs;
            float t     = range > 0f ? Mathf.Clamp01((d - rs) / range) : 0f;
            float power = binding.BaseForce * _currentFrame.FallOffCurve.Evaluate(t);

            float totalPower = power + (_isDashCombo ? _dashComboBonus : 0f);

            // SlamDown overrides direction to straight down; all other types use the
            // standard trajectory + elevation angle computation.
            Vector3 kickDir = binding.KickType == KickType.SlamDown
                ? EntityPhysics.Down
                : ComputeKickDirection(ball, binding.BaseAngle);

            ball.Kick(kickDir, totalPower, binding.KickType);

            float normalizedPower = binding.BaseForce > 0f ? Mathf.Clamp01(totalPower / binding.BaseForce) : 0f;
            FXManager.Instance.OnHit(_ep, ball.Physics, normalizedPower, _activeStrike.Property);
            CollisionManager.Instance.IgnoreCollisionPair(_position, ball.Position, _strikeIgnoreFrames);
        }
    }

    /// Summary:
    ///     Clears all strike runtime state. Safe to call as a safety net (e.g. from
    ///     a state's Exit) if the animator's End event was skipped due to an
    ///     interrupted animation.
    public void EndStrike()
    {
        _activeStrike = null;
        _currentFrame = null;
        _frameIndex   = 0;
        _ballsHit.Clear();
        _isDashCombo  = false;
    }

    ///
    /// ------- Private Methods --------------------------------------------------------------
    ///

    private void CollisionEnter(CollisionEvent e)
    {
        // Strike has exclusive authority while active — suppress regular collision kicks.
        if (HasActiveStrike) return;

        if (!e.otherObject.CompareTag("SoccerBall")) return;
        if (!e.otherObject.TryGetComponent<SoccerBall>(out var ball)) return;

        Vector3 trajectory = DetermineTrajectory(ball);
        float   force      = Mathf.Clamp(_ep.LinearVelocity.magnitude * forceMultiplier, minKickForce, maxKickForce);

        ball.Kick(trajectory, force);
        CollidedWithBall = true;

        // Power threshold for FX/combo eligibility — only "real" hits open the window.
        // A power dash kick is treated as a strike: same FX pipeline + same collision
        // ignore so the dashing entity doesn't follow through and disrupt the kick.
        float normalizedPower = Mathf.InverseLerp(minKickForce, maxKickForce, force);
        if (normalizedPower >= _powerHitThreshold)
        {
            FXManager.Instance.OnHit(_ep, ball.Physics, normalizedPower, _dashKickProperty);
            CollisionManager.Instance.IgnoreCollisionPair(_position, ball.Position, _strikeIgnoreFrames);
        }
    }

    /// Summary:
    ///     Returns the world-space position of the given frame using the entity's
    ///     current Pos_3D. Evaluated at call time (never cached) so the hitbox
    ///     follows the entity's live position, including jump and elevation changes.
    ///
    ///     XY: LocalOffset rotated in the horizontal plane by the facing angle
    ///         captured at BeginStrike.
    ///     Z:  LocalOffset.z added on top of Pos_3D.z (entity's current height,
    ///         which already encodes both jump height and surface elevation).
    private Vector3 FrameWorldPos(StrikeFrame frame)
    {
        float angle = Mathf.Atan2(_facingDir.y, _facingDir.x);
        float cos   = Mathf.Cos(angle);
        float sin   = Mathf.Sin(angle);

        return _position.Pos_3D + new Vector3(
            frame.LocalOffset.x * cos - frame.LocalOffset.y * sin,
            frame.LocalOffset.x * sin + frame.LocalOffset.y * cos,
            frame.LocalOffset.z
        );
    }

    /// Summary:
    ///     Tilts DetermineTrajectory's horizontal result upward by baseAngle degrees.
    ///     Total elevation is clamped to just under 90° so the direction can never
    ///     reverse, even if the base trajectory is already angled upward before the tilt.
    private Vector3 ComputeKickDirection(SoccerBall ball, int baseAngleDeg)
    {
        Vector3 horizontal = DetermineTrajectory(ball);
        float   horzMag    = ((Vector2)horizontal).magnitude;

        float existingElevation = Mathf.Atan2(horizontal.z, horzMag);
        float totalElevation    = Mathf.Min(
            existingElevation + baseAngleDeg * Mathf.Deg2Rad,
            Mathf.PI * 0.5f - 0.01f
        );

        // Degenerate case: ball is directly above or below the entity.
        Vector3 horzDir = horzMag > 0.0001f
            ? new Vector3(horizontal.x / horzMag, horizontal.y / horzMag, 0f)
            : Vector3.right;

        float cosEl = Mathf.Cos(totalElevation);
        float sinEl = Mathf.Sin(totalElevation);

        return new Vector3(horzDir.x * cosEl, horzDir.y * cosEl, sinEl);
    }

    /// Summary:
    ///     Blends the entity-to-ball direction with the entity's movement direction
    ///     based on the angle between them.
    ///
    ///     ≤10°:  pure entity-to-ball direction.
    ///     ≥90°:  pure entity movement direction.
    ///     10–90°: weighted blend; movement influence grows with angle.
    private Vector3 DetermineTrajectory(SoccerBall ball)
    {
        Vector3 playerToBall   = (ball.Position.Pos_3D - _position.Pos_3D).normalized;
        Vector3 entityVelocity = _ep.LinearVelocity;

        if (entityVelocity.magnitude < 0.1f) return playerToBall;
        entityVelocity = entityVelocity.normalized;

        float dot   = Vector3.Dot(playerToBall, entityVelocity);
        float angle = Mathf.Acos(Mathf.Clamp(dot, -1f, 1f)) * Mathf.Rad2Deg;

        if (angle >= 90f) return entityVelocity;
        if (angle <= 10f) return playerToBall;

        float weight = (angle - 10f) / (90f - 10f);
        return Vector3.Lerp(playerToBall, entityVelocity, weight).normalized;
    }

    /// Summary:
    ///     Returns the squared distance from point p to the nearest point on segment ab.
    ///     Used for swept-sphere hit detection in NextStrikeFrame.
    private static float SqDistPointSegment(Vector3 a, Vector3 b, Vector3 p)
    {
        Vector3 ab     = b - a;
        float   abLenSq = ab.sqrMagnitude;
        if (abLenSq < 1e-8f) return (p - a).sqrMagnitude;
        float   t       = Mathf.Clamp01(Vector3.Dot(p - a, ab) / abLenSq);
        Vector3 closest = a + t * ab;
        return (p - closest).sqrMagnitude;
    }
}
