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
        if (entity.TryChainStrikeInput(out State next))
        {
            entity.ChangeState(next);
            return;
        }

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
