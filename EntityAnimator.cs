/// Summary:
///     The intermediary for determining entity animation
using Unity.Mathematics;
using UnityEngine;

[RequireComponent(typeof(Animator))]
public class EntityAnimator : MonoBehaviour
{
    ///
    /// Private Components
    /// 

    /// The animator component for the attached entity.
    private Animator     Animator     { set; get; }
    private MovingEntity Entity       { set; get; }
    private KickHandler  _kickHandler;

    private void Awake()
    {
        Animator = GetComponent<Animator>();
        Entity   = GetComponent<MovingEntity>();
        TryGetComponent(out _kickHandler);
    }

    /// Summary:
    ///     Update animator for a moving entity.
    public void UpdateAnimations()
    {
        if (Animator == null) { return; }

        //if moving, set animator parameter
        if (Entity.IsMoving)
        {
            Animator.SetBool("isMoving", true);

            //update animator float parameters
            Animator.SetFloat("horizontal", Entity.Velocity.normalized.x);
            Animator.SetFloat("vertical", Entity.Velocity.normalized.y);
        }
        else
        {
            if (Animator.GetBool("isMoving"))
            {
                Animator.SetBool("isMoving", false);
            }
        }
    }

    public void PlayAnimation(string animationName)
    {
        if (Animator == null) { return; }

        Animator.Play(animationName);
    }

    /// Summary:
    ///     Set a boolean parameter in the animator, used for various animation states.
    /// 
    /// Parameters:
    ///     parameterName: 
    ///         The name of the boolean parameter to set.
    ///     value: 
    ///         The value to set the boolean parameter to.
    public void SetBool(string parameterName, bool value)
    {
        if (Animator == null) { return; }

        Animator.SetBool(parameterName, value);
    }

    /// Summary:
    ///     Set entity animation to the falling animation
    public void Falling()
    {

    }

    ///
    /// ------- Strike Animation Event Routing ----------------------------------------------
    ///
    /// These methods are called directly by Unity Animator events and forward to the
    /// entity's KickHandler. Living on EntityAnimator (not Player) keeps them reusable
    /// for any entity type that has a KickHandler (AI, player, etc.).
    ///

    /// Summary:
    ///     Called by an Animator event at the start of a swing.
    ///     strikeObj must be a Strike ScriptableObject dragged into the event's Object slot.
    public void OnStrikeBegin(UnityEngine.Object strikeObj)
    {
        if (_kickHandler == null) return;

        if (strikeObj is Strike strike)
            _kickHandler.BeginStrike(strike);
        else
            Debug.LogError($"[EntityAnimator] OnStrikeBegin: expected a Strike asset, got '{strikeObj?.GetType().Name ?? "null"}'. Check the Animator event's Object slot.");
    }

    /// Summary:
    ///     Called by an Animator event at each subsequent swing keyframe.
    ///     Advances the strike one frame and runs swept-sphere hit detection.
    public void OnStrikeNextFrame()
    {
        _kickHandler?.NextStrikeFrame();
    }

    /// Summary:
    ///     Called by an Animator event at the end of the swing.
    ///     Also called defensively from state Exit methods in case this event is skipped.
    public void OnStrikeEnd()
    {
        _kickHandler?.EndStrike();
    }
}
