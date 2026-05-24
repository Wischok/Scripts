using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

public class InputHandler : MonoBehaviour
{
    ///
    /// -------- Inspector Variables ----------------------------------------------------------
    /// 
    [Header("Input Settings")]//enable and disable player inputs for testing purposes
    [SerializeField] private float _bufferWindow = 0.1f;//the time window for input buffering and coyote time 

    [Header("Input Action Settings")]
    [SerializeField] private float _dashCooldown = 1f;//dash cooldown duration

    ///
    /// --------- Public Variables ----------------------------------------------------------
    /// 
    
    /// Summary:
    ///     Public player actions. Retrieved by player states to check for input and perform actions.
    public Vector2 Movement => _moveAction.ReadValue<Vector2>();
    public bool Jumped => ConsumeInput(_jumpAction);
    public bool Dashed
    {
        get
        {
            //check if dash is still on cooldown
            if(_timeElapsedSinceLastDash < _dashCooldown) return false;

            //check if dash input was pressed; return false if not
            if(!ConsumeInput(_dashAction)) return false;

            //if dash input was pressed and dash is off cooldown, reset dash cooldown timer
            //and return true
            _timeElapsedSinceLastDash = 0f;
            return true;
        }
    }
    public bool Headbutted => ConsumeInput(_headbuttAction);
    public bool SlammedDown => ConsumeInput(_slamDownAction);
    public bool SpawnedBall => ConsumeInput(_spawnBallAction);
    public bool SpinKickedRight => ConsumeInput(_kickSpinRightAction);
    public bool SpinKickedLeft => ConsumeInput(_kickSpinLeftAction);

    ///
    /// --------- Private Variables ----------------------------------------------------------
    /// 
    
    /// Summary:
    ///     The player input actions. Declared here and initialized in the awake function
    private InputAction _moveAction;//player movement
    private InputAction _jumpAction;//player jump action
    private InputAction _dashAction;//player dash action
    private InputAction _headbuttAction;//player headbutt action
    private InputAction _slamDownAction;//player slam down action
    private InputAction _spawnBallAction;//player spawn ball action
    private InputAction _kickSpinRightAction;//player kick spin right action
    private InputAction _kickSpinLeftAction;//player kick spin left action
    private InputAction _pauseAction;//player pause action
    private System.Action<InputAction.CallbackContext> _onPause;//pause action callback reference for unsubscribing
    private System.Action<InputAction.CallbackContext> _onSpawnBall;//spawn ball action callback reference for unsubscribing

    /// Summary:
    ///     A struct for buffering inputs. Tracks whether the input was pressed and the
    ///     timestamp of when the input was pressed, allowing for input buffering and coyote time.
    private struct BufferedInput
    {
        public bool Pressed;
        public float Timestamp;
    }

    /// Summary:
    ///     A dictionary for buffering inputs, mapping input actions to their 
    ///     buffered input states.
    private readonly Dictionary<InputAction, BufferedInput> _buffer = new();

    /// Summary:
    ///     a dictionary reference to action callbacks for unsubscribing when disabling the input handler
    private readonly Dictionary<InputAction, System.Action<InputAction.CallbackContext>> _actionCallbacks = new();

    /// Summary:
    ///     A timer to track time elapsed since the last dash
    private float _timeElapsedSinceLastDash = 1f;//timer to track dash cooldown

    ///
    /// --------- Unity Methods ------------------------------------------------------------------
    /// 
    
    private void Awake()
    {
        //intialize actions
        _moveAction = InputSystem.actions.FindAction("Move");
        _jumpAction = InputSystem.actions.FindAction("Jump");
        _pauseAction = InputSystem.actions.FindAction("Pause");
        _dashAction = InputSystem.actions.FindAction("Dash");
        _headbuttAction = InputSystem.actions.FindAction("Headbutt");
        _spawnBallAction = InputSystem.actions.FindAction("SpawnBall");
        _kickSpinRightAction = InputSystem.actions.FindAction("KickSpinRight");
        _kickSpinLeftAction = InputSystem.actions.FindAction("KickSpinLeft");
        _slamDownAction = InputSystem.actions.FindAction("SlamDown");
    }

    private void OnEnable()
    {
        //enable actions
        _pauseAction.Enable();
        _moveAction.Enable();
        _spawnBallAction.Enable();

        //register actions for buffering
        Register(_jumpAction);
        Register(_dashAction);
        Register(_headbuttAction);
        Register(_slamDownAction);
        Register(_spawnBallAction);
        Register(_kickSpinRightAction);
        Register(_kickSpinLeftAction);

        //set up pause action callback
        _onPause = ctx => GameManager.Instance.TogglePause();
        _pauseAction.performed += _onPause;

        //set up spawn ball action callback for testing purposes
        _onSpawnBall = ctx => Spawner.Instance.SpawnEntity(transform.position + (Vector3.down * 5));
        _spawnBallAction.performed += _onSpawnBall;
    }

    private void OnDisable()
    {
        Unregister(_jumpAction);
        Unregister(_dashAction);
        Unregister(_headbuttAction);
        Unregister(_kickSpinRightAction);
        Unregister(_kickSpinLeftAction);
        Unregister(_slamDownAction);
        
        //disable actions
        _moveAction.Disable();
        _pauseAction.Disable();
        _spawnBallAction.Disable();

        //unsubscribe from pause action callback
        _pauseAction.performed -= _onPause;
        _spawnBallAction.performed -= _onSpawnBall;
    }

    private void FixedUpdate() => UpdateBuffer();

    ///
    /// --------- Private Methods ----------------------------------------------------------------------
    ///

    /// Summary:
    ///     Updates the input buffer by checking each buffered input to see if it was pressed.
    ///     If it was pressed, check if the timestamp is within the buffer window. If not, "expire" the 
    ///     input by setting the buffer for that input to not pressed and a timestamp of 0.
    private void UpdateBuffer()
    {
        //loop through each buffered input
        foreach (var action in _buffer.Keys.ToList())
        {
            var bufferedInput = _buffer[action];//get action's buffered input
            if (bufferedInput.Pressed && Time.time - bufferedInput.Timestamp > _bufferWindow)//check expiration
                //expire input if outside buffer window
                _buffer[action] = new BufferedInput { Pressed = false, Timestamp = 0 };
        }

        //update flags and timers

        //dash cooldown
        if (_timeElapsedSinceLastDash < _dashCooldown)
            _timeElapsedSinceLastDash += Time.fixedDeltaTime;
    }

    /// Summary:
    ///     Registers an input action for buffering.
    /// 
    /// Parameters:
    ///     action: 
    ///         the input action to register for buffering
    private void Register(InputAction action)
    {
        //enable the action
        action.Enable();

        //add the action to the buffer with an initial state of not pressed and a timestamp of 0
        _buffer[action] = new BufferedInput { Pressed = false, Timestamp = 0 };

        //set up a callback for when the action is performed to update the buffer
        void callback(InputAction.CallbackContext ctx) => OnPressed(action);
        _actionCallbacks[action] = callback;
        action.performed += callback;
    }

    /// Summary:
    ///     Unregisters an input action from buffering.
    /// 
    /// Parameters:
    ///     action: 
    ///         the input action to unregister from buffering
    private void Unregister(InputAction action)
    {
        //unsubscribe from the action's callback
        if (_actionCallbacks.TryGetValue(action, out var callback))
        {
            action.performed -= callback;
            _actionCallbacks.Remove(action);
        }

        //disable the action
        action.Disable();

        //remove the action from the buffer
        _buffer.Remove(action);
    }

    /// Summary:
    ///     Callback for when a registered input action is performed. It updates 
    ///     the buffer for that action to indicate it was pressed and records the timestamp.
    /// 
    /// Parameters:
    ///     action: 
    ///         the input action that was performed
    private void OnPressed(InputAction action)
    {
        if (_buffer.ContainsKey(action))
            _buffer[action] = new BufferedInput { Pressed = true, Timestamp = Time.time };
    }

    /// Summary:
    ///     Checks if a registered input action is present and whether it was pressed. If so, 
    ///     it "consumes" the input by setting the buffer for that action to not pressed and 
    ///     a timestamp of 0, and returns true. Otherwise, it returns false.
    /// 
    /// Parameters:
    ///     action:
    ///        the input action to check and consume if present
    /// 
    /// Returns:
    ///     true if the input action was present and pressed, false otherwise
    private bool ConsumeInput(InputAction action)
    {
        //check if the action is in the buffer
        if(!_buffer.TryGetValue(action, out var bufferedInput))
        {
            //if not in buffer, log error and return false
            Debug.LogError($"Trying to consume input for action {action.name} which is not registered in the buffer.");
            return false;
        }

        //if button not pressed, return false
        if(!bufferedInput.Pressed) return false;

        //if action exists and was pressed, update buffer to not pressed and return true
        _buffer[action] = new BufferedInput { Pressed = false, Timestamp = 0 };
        return true;
    }

    ///
    /// --------- Additional Action Callbacks ----------------------------------------------------------------------
    /// 
    
}
