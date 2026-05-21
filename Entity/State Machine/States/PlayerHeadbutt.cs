using UnityEngine;

public class PlayerHeadbutt : State
{
    private float headbuttTimer = 0f;

    public override void Enter(Player entity)
    {
        //headbutt enter function
        entity.Headbutt();
    }

    public override void Execute(Player entity)
    {
        if(headbuttTimer < entity.HeadbuttDuration)
            headbuttTimer += Time.deltaTime;
            
        else
        {
            //after headbutt duration, return to previous state
            if (entity.IsTouchingGround)
                entity.ChangeState(new PlayerMove());
            else
                entity.ChangeState(new PlayerFall());
        }
    }

    public override void Exit(Player entity)
    {
        entity.HeadbuttExit();
        // Safety net: clears strike state if the Animator's OnStrikeEnd event was
        // skipped due to an interrupted animation (e.g. player was knocked back mid-swing).
        entity.KickHandler.EndStrike();
    }
}
