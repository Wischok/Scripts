using UnityEngine;

public class PlayerSpinKickRight : State
{
    private float _timer = 0f;

    public override void Enter(Player entity)
    {
        entity.SpinKickRight();
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
