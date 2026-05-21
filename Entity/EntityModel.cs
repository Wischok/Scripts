using UnityEngine;

/// Summary:
///     Orients a 3D model child to match the game's oblique-projection camera angle
///     and rotates it to face the entity's movement direction each frame.
///
///     Attach to the entity root. Assign the 3D model's Transform to _modelRoot.
///
///     The model lies in the camera's view plane, achieved by a single X-axis tilt
///     (_worldTiltDeg). Facing direction is a Z rotation that spins within that plane.
///     Unity applies Euler in ZXY order, so Euler(tilt, 0, facing) correctly spins
///     first then tilts — no gimbal issues.
///
///     Tuning: set _worldTiltDeg to the negative of the camera's elevation angle
///     (e.g. -60 for a 60° elevation camera). Negate _worldTiltDeg if the top of
///     the model tilts away from the camera instead of toward it.
[RequireComponent(typeof(EntityPhysics))]
public class EntityModel : MonoBehaviour
{
    [SerializeField] private Transform _modelRoot;

    [Tooltip("X-axis tilt to align the model with the camera's view plane. " +
             "Negative tilts the top toward the camera. " +
             "Match to the camera elevation angle (e.g. -60 for a 60° camera).")]
    [SerializeField] private float _worldTiltDeg = -60f;

    [SerializeField] private float _rotationSpeed = 10f;

    [Tooltip("When false (default), facing uses physics velocity. " +
             "When true, uses InputHandler.Movement — snappier but player-only.")]
    [SerializeField] private bool _useInputDirection = false;

    private EntityPhysics _physics;
    private InputHandler  _inputHandler;

    private void Awake()
    {
        _physics = GetComponent<EntityPhysics>();
        TryGetComponent(out _inputHandler);
    }

    private void Start()
    {
        if (_modelRoot != null)
            _modelRoot.rotation = Quaternion.Euler(_worldTiltDeg, 0f, 0f);
    }

    private void LateUpdate()
    {
        if (_modelRoot == null) return;

        Vector2 dir = _useInputDirection && _inputHandler != null
            ? _inputHandler.Movement
            : (Vector2)_physics.LinearVelocity;

        if (dir.sqrMagnitude < 0.01f) return;

        // Angle within the view plane from screen-up rotating toward screen-right.
        // Negated so positive-X input rotates the model clockwise (toward right) as seen
        // from the camera. Flip the sign here if the model spins the wrong direction.
        float      facingZ   = -Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
        Quaternion targetRot = Quaternion.Euler(_worldTiltDeg, 0f, facingZ);
        _modelRoot.rotation  = Quaternion.Slerp(_modelRoot.rotation, targetRot, _rotationSpeed * Time.deltaTime);
    }
}
