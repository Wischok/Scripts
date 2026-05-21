using UnityEngine;

public class PlayerSlamDown : State
{
    private float _timer = 0f;

    public override void Enter(Player entity)
    {
        entity.SlamDown();
    }

    public override void Execute(Player entity)
    {
        _timer += Time.deltaTime;
        if (_timer < entity.SlamDownDuration) return;

        entity.ChangeState(entity.IsTouchingGround
            ? (State)new PlayerMove()
            : new PlayerFall());
    }

    public override void Exit(Player entity)
    {
        entity.SlamDownExit();
        entity.KickHandler.EndStrike();
    }
}
