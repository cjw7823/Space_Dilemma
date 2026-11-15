using System;
using System.Collections;
using SpaceDilemma.Hud;
using SpaceDilemma.Survival;
using StarterAssets;
using UnityEngine;
using UnityEngine.InputSystem;

static class AirSpeedClockChecks
{
    public static IEnumerator Run(MoonBasePlayer player, SurvivalSession session, LunarEnvironment environment,
        HudController hud, Action<Key[]> send, Action<bool,string> check)
    {
        var controller = player.GetComponent<CharacterController>();
        var motor = player.GetComponent<ThirdPersonController>();
        Vector3 origin = player.transform.position;
        Quaternion rotation = player.transform.rotation;
        Quaternion look = motor.CinemachineCameraTarget.transform.rotation;
        float oldVolume = AudioListener.volume;
        var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "Air speed verification floor";
        ground.transform.position = new Vector3(0,12000,0);
        ground.transform.localScale = new Vector3(200,1,200);
        ground.GetComponent<Renderer>().enabled = false;
        AudioListener.volume = 0;
        try
        {
            check(true,$"Air movement setup: gravity={motor.Gravity}, jump={motor.JumpHeight}, scale={Time.timeScale}, run={session.Run.Hours}, state={session.Run.Outcome}");
            Place(player,controller,motor,new Vector3(0,12000.55f,0));
            motor.CinemachineCameraTarget.transform.rotation = Quaternion.identity;
            motor.SyncLookDirection();
            player.SendMessage("OnApplicationFocus",true);
            player.SetControlCaptured(true);
            send(Array.Empty<Key>());
            yield return Wait(1);
            yield return JumpCase(true,player,controller,motor,send,check);
            yield return JumpCase(false,player,controller,motor,send,check);
        }
        finally
        {
            send(Array.Empty<Key>());
            player.transform.rotation = rotation;
            motor.CinemachineCameraTarget.transform.rotation = look;
            Place(player,controller,motor,origin);
            AudioListener.volume = oldVolume;
            UnityEngine.Object.DestroyImmediate(ground);
        }

        check(HudController.FormatElapsedTime(24)=="24:00:00" &&
            HudController.FormatElapsedTime(48)=="48:00:00", "Elapsed clock preserves the 24h and 48h boundaries");
        check(HudController.FormatElapsedTime(86399.0/3600)=="23:59:59" &&
            HudController.FormatElapsedTime(172799.0/3600)=="47:59:59", "Elapsed clock handles the second before each daily boundary");
        bool enabled = session.enabled;
        double savedHours = session.Run.Hours;
        session.enabled = false;
        try
        {
            environment.SetSessionClock(24,.02f);
            yield return Wait(.1f);
            check(hud.ClockLabel.text=="24:00:00", "Actual HUD shows 24:00:00 at hour 24");
            environment.SetSessionClock(48,.02f);
            yield return Wait(.1f);
            check(hud.ClockLabel.text=="48:00:00", "Actual HUD shows 48:00:00 at the time limit");
            check(true,$"Clock boundary setup: run={session.Run.Hours}, state={session.Run.Outcome}, scale={Time.timeScale}");
        }
        finally
        {
            environment.SetSessionClock(savedHours,.02f);
            session.enabled = enabled;
        }
    }

    static IEnumerator JumpCase(bool running,MoonBasePlayer player,CharacterController cc,
        ThirdPersonController motor,Action<Key[]> send,Action<bool,string> check)
    {
        send(running ? new[]{Key.W,Key.LeftShift}:new[]{Key.W});
        yield return Wait(1);
        float takeoffSpeed = Horizontal(cc.velocity);
        check(cc.isGrounded && Math.Abs(takeoffSpeed-(running?motor.SprintSpeed:motor.MoveSpeed))<.2,
            (running?"Sprint":"Walk")+$" reaches its ground speed before jumping: grounded={cc.isGrounded} speed={takeoffSpeed:F3}, y={player.transform.position.y:F3}");
        send(running ? new[]{Key.W,Key.LeftShift,Key.Space}:new[]{Key.W,Key.Space});
        yield return Wait(.12f);
        send(running ? new[]{Key.W}:new[]{Key.W,Key.LeftShift});
        yield return Wait(.4f);
        check(!cc.isGrounded && Math.Abs(Horizontal(cc.velocity)-takeoffSpeed)<.2,
            (running?"Releasing":"Pressing")+" Shift during flight preserves takeoff speed: "+Horizontal(cc.velocity).ToString("F3")+"m/s");
        send(running ? new[]{Key.W,Key.LeftShift}:new[]{Key.W});
        yield return Wait(.4f);
        check(!cc.isGrounded && Math.Abs(Horizontal(cc.velocity)-takeoffSpeed)<.2,
            "Toggling Shift a second time in flight cannot change "+(running?"sprint":"walk")+" jump speed");
        send(running ? new[]{Key.W}:new[]{Key.W,Key.LeftShift});
        yield return Wait(2.4f);
        check(cc.isGrounded && Math.Abs(Horizontal(cc.velocity)-(running?motor.MoveSpeed:motor.SprintSpeed))<.2,
            $"After landing, the current Shift state controls ground speed again: grounded={cc.isGrounded} speed={Horizontal(cc.velocity):F3}, y={player.transform.position.y:F3}, scale={Time.timeScale}");
        send(Array.Empty<Key>());
        yield return Wait(.15f);
    }

    static float Horizontal(Vector3 velocity)=>new Vector2(velocity.x,velocity.z).magnitude;
    static void Place(MoonBasePlayer player,CharacterController cc,ThirdPersonController motor,Vector3 location)
    {
        cc.enabled=false;
        player.transform.position=location;
        cc.enabled=true;
        motor.SyncLookDirection();
        Physics.SyncTransforms();
    }
    static IEnumerator Wait(float seconds)
    {
        float until=Time.realtimeSinceStartup+seconds;
        do {yield return null;}while(Time.realtimeSinceStartup<until);
    }
}
