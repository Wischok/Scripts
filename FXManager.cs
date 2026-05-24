using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

public class FXManager : MonoBehaviour
{
    ///
    /// ------- Inspector Variables ----------------------------------------------------------
    ///

    [Header("--- Screen Shake ---")]

    [SerializeField] private float _defaultTrauma   = 0.5f;
    [SerializeField] private float _traumaDecayRate = 1f;
    [SerializeField] private float _maxShakeOffset  = 0.5f;

    [Header("--- Hitstop ---")]

    [SerializeField] private int _hitstopFramesMin = 3;
    [SerializeField] private int _hitstopFramesMax = 8;

    [SerializeField, Range(0f, 0.2f)] private float _hitstopTimeScale = 0.05f;

    [Header("--- Vibration ---")]

    // Low-frequency motor (heavy rumble). Scales with kick power.
    [SerializeField, Range(0f, 1f)] private float _vibrationLowMin  = 0.1f;
    [SerializeField, Range(0f, 1f)] private float _vibrationLowMax  = 0.8f;

    // High-frequency motor (fine buzz). Scales with kick power.
    [SerializeField, Range(0f, 1f)] private float _vibrationHighMin = 0.05f;
    [SerializeField, Range(0f, 1f)] private float _vibrationHighMax = 0.4f;

    [SerializeField] private float _vibrationDuration = 0.15f;

    [Header("--- References ---")]

    [SerializeField] private CinemachineCamera _mainCamera;

    ///
    /// ------- Private State ----------------------------------------------------------------
    ///

    private CinemachineFollow _cameraFollow;
    private Vector3           _cameraOrigin;
    private float             _currentTrauma        = 0f;
    private bool              _shakeActive          = false;
    private float             _vibrationTimeRemaining = 0f;

    private static FXManager _instance;
    public static  FXManager Instance => _instance;

    ///
    /// ------- Unity Methods ----------------------------------------------------------------
    ///

    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
    }

    private void Start()
    {
        _cameraFollow = _mainCamera.GetComponent<CinemachineFollow>();
        if (_cameraFollow != null)
            _cameraOrigin = _cameraFollow.FollowOffset;
    }

    // LateUpdate matches Cinemachine's default update phase so our writes are never
    // overridden by Cinemachine's own LateUpdate pass on the same frame.
    private void LateUpdate()
    {
        HandleShake();
        HandleVibration();
    }

    ///
    /// ------- Public API -------------------------------------------------------------------
    ///

    /// Summary:
    ///     Main entry point for all hit FX. Orchestrates hitstop on the striking entity
    ///     and the ball, applies camera trauma, triggers controller vibration, and
    ///     triggers impact particles.
    ///
    /// Parameters:
    ///     striker:          EntityPhysics of the entity that made contact.
    ///     ball:             EntityPhysics of the ball that was struck.
    ///     powerMultiplier:  0..1 where 1 is a perfect sweet-spot hit at maximum force.
    ///                       Scales hitstop duration, shake intensity, vibration, and particle density.
    ///     property:         HitProperty role of the strike — passed to ComboHandler so the
    ///                       chain remembers its last move (for shot routing / multipliers).
    public void OnHit(EntityPhysics striker, EntityPhysics ball, float powerMultiplier, HitProperty property)
    {
        int frames = Mathf.RoundToInt(Mathf.Lerp(_hitstopFramesMin, _hitstopFramesMax, powerMultiplier));

        striker.EnterHitstop(frames, _hitstopTimeScale, blockForces: false);
        ball.EnterHitstop(frames, _hitstopTimeScale, blockForces: true);

        // Record the hit — RegisterHit refreshes the window and extends the chain.
        if (ball.TryGetComponent<ComboHandler>(out var combo))
            combo.RegisterHit(striker.gameObject.GetInstanceID(), property);

        AddTrauma(_defaultTrauma * powerMultiplier);
        TriggerVibration(powerMultiplier);
        SpawnImpactParticles(ball.transform.position, powerMultiplier);
    }

    /// Summary:
    ///     Called when a strike's swept hit detection lands on a ball but the strike's
    ///     HitProperty was invalid for the ball's current air state (e.g. Spike attempted
    ///     while the ball is grounded). No kick is applied; this hook exists so a future
    ///     whiff sound + unsatisfying particle effect can be plugged in without
    ///     touching the gate logic in KickHandler.
    public void OnWhiff(EntityPhysics striker, EntityPhysics ball, HitProperty attemptedProperty)
    {
        _ = striker;
        _ = ball;
        _ = attemptedProperty;
        // TODO: whiff sound + low-quality particle effect.
    }

    /// Summary:
    ///     Called when the player attempts a new strike too early — before the active
    ///     strike's cancel window opens. The input is rejected and the player enters a
    ///     brief fumble lockout. This hook exists so a future fumble sound + visual
    ///     stagger / shake can be plugged in.
    public void OnFumble(EntityPhysics striker)
    {
        _ = striker;
        // TODO: fumble sound + brief stumble particle / shake.
    }

    ///
    /// ------- Private Methods --------------------------------------------------------------
    ///

    private void AddTrauma(float amount)
    {
        _currentTrauma = Mathf.Clamp01(_currentTrauma + amount);
    }

    private void HandleShake()
    {
        if (_currentTrauma <= 0f)
        {
            // Reset fires exactly once when trauma reaches zero. The _shakeActive flag
            // prevents redundant writes on every subsequent frame with no trauma.
            if (_shakeActive)
            {
                _cameraFollow.FollowOffset = _cameraOrigin;
                _shakeActive               = false;
            }
            return;
        }

        _shakeActive = true;

        float shake   = _currentTrauma * _currentTrauma;
        float offsetX = _maxShakeOffset * shake * (Mathf.PerlinNoise(Time.time * 10f, 0f)             * 2f - 1f);
        float offsetY = _maxShakeOffset * shake * (Mathf.PerlinNoise(0f,             Time.time * 10f) * 2f - 1f);

        _cameraFollow.FollowOffset = _cameraOrigin + new Vector3(offsetX, offsetY, 0f);

        _currentTrauma = Mathf.Clamp01(_currentTrauma - _traumaDecayRate * Time.deltaTime);
    }

    private void TriggerVibration(float t)
    {
        var gamepad = Gamepad.current;
        if (gamepad == null) return;

        float lowFreq  = Mathf.Lerp(_vibrationLowMin,  _vibrationLowMax,  t);
        float highFreq = Mathf.Lerp(_vibrationHighMin, _vibrationHighMax, t);

        gamepad.SetMotorSpeeds(lowFreq, highFreq);
        _vibrationTimeRemaining = _vibrationDuration;
    }

    private void HandleVibration()
    {
        if (_vibrationTimeRemaining <= 0f) return;

        _vibrationTimeRemaining -= Time.deltaTime;
        if (_vibrationTimeRemaining <= 0f)
            Gamepad.current?.ResetHaptics();
    }

    /// Summary:
    ///     Spawns impact particles at the hit position scaled by hit power.
    ///     Assign particle system prefabs in the inspector when particle assets are ready.
    private void SpawnImpactParticles(Vector3 position, float powerMultiplier)
    {
        _ = position;
        _ = powerMultiplier;
        // TODO: instantiate / play particle prefabs here based on powerMultiplier.
    }
}
