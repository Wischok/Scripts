using System.Collections.Generic;
using UnityEngine;

/// Summary:
///     Manages the combo window and juggle tracking for the SoccerBall.
///     Drives EntityPhysics.MovementScale to slow the ball during the window,
///     tracks who hit the ball and how many times during a juggle, and
///     provides hooks for bonus resolution when the juggle ends.
///
///     Hitstop (EntityPhysics.EnterHitstop) is a separate, shorter effect that
///     takes priority over MovementScale while active.
[RequireComponent(typeof(EntityPhysics))]
public class ComboHandler : MonoBehaviour
{
    ///
    /// ------- Inspector Variables ----------------------------------------------------------
    ///

    [Header("--- Combo Window ---")]

    [SerializeField] private int   _windowFrames    = 10;
    [SerializeField] private int   _extensionFrames = 10;
    [SerializeField] private float _windowScale     = 0.30f;

    [Header("--- Combo Chain ---")]

    // How long after a hit the chain remains alive. Intentionally longer than the window:
    // the slow-down may end but subsequent hits still count as chain continuations.
    [SerializeField] private int _decayFrames = 60;

    ///
    /// ------- Public Properties ------------------------------------------------------------
    ///

    public bool InComboWindow => _windowFramesRemaining > 0;

    // Chain state (consumed by UI, momentum multiplier, shot-routing).
    public int         ChainCount      => _chainCount;
    public HitProperty LastHitProperty => _lastHitProperty;
    public bool        InCombo         => _decayRemaining > 0;
    public float       DecayProgress   => _decayFrames > 0 ? (float)_decayRemaining / _decayFrames : 0f;

    ///
    /// ------- Private State ----------------------------------------------------------------
    ///

    private int _windowFramesRemaining = 0;

    // Juggle tracking — accumulated while the ball is airborne.
    private int          _juggleHits    = 0;
    private float        _juggleAirTime = 0f;
    private HashSet<int> _participants  = new();

    // Chain tracking — outlives the window; resets only when decay expires.
    private int         _chainCount      = 0;
    private int         _decayRemaining  = 0;
    private HitProperty _lastHitProperty = HitProperty.Any;

    private EntityPhysics _physics;

    ///
    /// ------- Unity Methods ----------------------------------------------------------------
    ///

    private void Awake() => _physics = GetComponent<EntityPhysics>();

    private void FixedUpdate()
    {
        if (_windowFramesRemaining > 0 && --_windowFramesRemaining == 0)
        {
            _physics.MovementScale = 1f;
            OnWindowExpired();
        }

        if (_decayRemaining > 0 && --_decayRemaining == 0)
            ResetChain();
    }

    ///
    /// ------- Public API -------------------------------------------------------------------
    ///

    /// Summary:
    ///     Opens (or refreshes) the combo window. The longer of the current remaining
    ///     frames and the requested duration wins. Called by FXManager.OnHit after a
    ///     confirmed power hit.
    public void OpenWindow(int frames = -1)
    {
        int f = frames > 0 ? frames : _windowFrames;
        _windowFramesRemaining   = Mathf.Max(_windowFramesRemaining, f);
        _physics.MovementScale   = _windowScale;
    }

    /// Summary:
    ///     Adds frames to an already-open window. Called by KickHandler.BeginStrike
    ///     when a new strike animation begins near the ball, giving the swing time
    ///     to complete before the window expires.
    ///     No-ops if no window is currently open.
    public void ExtendWindow()
    {
        if (_windowFramesRemaining <= 0) return;
        _windowFramesRemaining += _extensionFrames;
    }

    /// Summary:
    ///     Records a hit against this ball, refreshes the combo window, extends the
    ///     chain, and refreshes the chain decay timer.
    ///     Called by FXManager.OnHit so the juggle tracker knows who contributed.
    ///
    /// Parameters:
    ///     strikerInstanceId: GameObject.GetInstanceID() of the hitting entity.
    ///     property:          HitProperty role of the strike that landed.
    public void RegisterHit(int strikerInstanceId, HitProperty property)
    {
        _juggleHits++;
        _participants.Add(strikerInstanceId);

        _chainCount++;
        _lastHitProperty = property;
        _decayRemaining  = _decayFrames;

        OpenWindow();
    }

    /// Summary:
    ///     Called by SoccerBall_Juggle.Enter to begin tracking air time.
    public void OnJuggleBegin()
    {
        _juggleHits    = 0;
        _juggleAirTime = 0f;
        _participants.Clear();
    }

    /// Summary:
    ///     Called by SoccerBall_Juggle.Exit when the ball lands.
    ///     Accumulates air time and resolves juggle bonuses.
    public void OnJuggleEnd(float airTime)
    {
        _juggleAirTime += airTime;

        // TODO: resolve bonuses from _juggleHits, _juggleAirTime, _participants.
        //       Data is available here for scoring, VFX scaling, etc.
    }

    ///
    /// ------- Private Methods --------------------------------------------------------------
    ///

    private void OnWindowExpired()
    {
        // Window closed without the juggle ending — data persists for the ongoing juggle.
    }

    // Chain expired without a follow-up hit. Clears chain count and last property;
    // juggle stats are unaffected (they reset on OnJuggleBegin).
    private void ResetChain()
    {
        _chainCount      = 0;
        _lastHitProperty = HitProperty.Any;
    }
}
