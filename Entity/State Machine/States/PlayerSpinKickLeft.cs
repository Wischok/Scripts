using UnityEngine;

public class PlayerSpinKickLeft : State
{
    private float _timer = 0f;

    public override void Enter(Player entity)
    {
        entity.SpinKickLeft();
    }

    public override void Execute(Player entity)
    {
        _timer += Time.deltaTime;
        if (_timer < entity.SpinKickDuration) return;

        entity.ChangeState(entity.IsTouchingGround
            ? (State)new PlayerMove()
            : new PlayerFall());
    }

    public override void Exit(Player entity)
    {
        entity.SpinKickExit();
        entity.KickHandler.EndStrike();
    }
}
