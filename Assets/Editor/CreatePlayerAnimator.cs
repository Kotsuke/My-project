using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.IO;

// ============================================================
//  CREATE PLAYER ANIMATOR (REFACTORED)
//  Perbaikan dari versi lama:
//  1. AnyState → Jump/Slide difilter agar tidak trigger dari
//     Dying/Stunned/Landing state (mencegah konflik transisi)
//  2. Semua transisi punya duration yang tepat
//  3. WallJump state ditambahkan
//  4. Transition order diprioritaskan dengan benar
// ============================================================
public static class CreatePlayerAnimator
{
    [MenuItem("Tools/Create Player Animator Controller (Refactored)")]
    public static void CreateController()
    {
        const string folderPath     = "Assets/Animations";
        const string controllerPath = folderPath + "/PlayerAnimatorController.controller";

        if (!Directory.Exists(folderPath))
            Directory.CreateDirectory(folderPath);

        var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        if (controller == null)
        {
            Debug.LogError("Gagal membuat Animator Controller di: " + controllerPath);
            return;
        }

        // ── Parameters ──
        AddParam(controller, "Speed",          AnimatorControllerParameterType.Float);
        AddParam(controller, "IsGrounded",     AnimatorControllerParameterType.Bool);
        AddParam(controller, "VerticalVelocity", AnimatorControllerParameterType.Float);
        AddParam(controller, "WallRunSide",    AnimatorControllerParameterType.Int);
        AddParam(controller, "Jump",           AnimatorControllerParameterType.Trigger);
        AddParam(controller, "DoubleJump",     AnimatorControllerParameterType.Trigger);
        AddParam(controller, "Slide",          AnimatorControllerParameterType.Trigger);
        AddParam(controller, "Roll",           AnimatorControllerParameterType.Trigger);
        AddParam(controller, "HardLanding",    AnimatorControllerParameterType.Trigger);
        AddParam(controller, "LandToRun",      AnimatorControllerParameterType.Trigger);
        AddParam(controller, "Stunned",        AnimatorControllerParameterType.Bool);
        AddParam(controller, "Die",            AnimatorControllerParameterType.Trigger);

        var rootSM = controller.layers[0].stateMachine;

        // ── Load Clips ──
        var idle        = LoadClip("Assets/motion animation/Breathing Idle.fbx");
        var run         = LoadClip("Assets/motion animation/Fast Run.fbx");
        var sprint      = LoadClip("Assets/motion animation/Sprint.fbx");
        var falling     = LoadClip("Assets/motion animation/Falling Idle.fbx");
        var land        = LoadClip("Assets/motion animation/Falling To Landing.fbx");
        var roll        = LoadClip("Assets/motion animation/Falling To Roll.fbx");
        var landToRun   = LoadClip("Assets/motion animation/Fall A Land To Run Forward.fbx");
        var slide       = LoadClip("Assets/motion animation/Running Slide.fbx");
        var jump        = LoadClip("Assets/motion animation/Running Jump.fbx");
        var doubleJump  = LoadClip("Assets/motion animation/Running Forward Flip.fbx");
        var wallLeft    = LoadClip("Assets/motion animation/Wall Run left.fbx");
        var wallRight   = LoadClip("Assets/motion animation/Wall Run right.fbx");
        var stunned     = LoadClip("Assets/motion animation/Stunned.fbx");
        var dying       = LoadClip("Assets/motion animation/Dying.fbx");
        var runToStop   = LoadClip("Assets/motion animation/Run To Stop.fbx");

        // ── States ──
        var sIdle       = MakeState(rootSM, "Idle",        idle,       new Vector2(200, 0));
        var sRun        = MakeState(rootSM, "Run",         run,        new Vector2(200, 100));
        var sSprint     = MakeState(rootSM, "Sprint",      sprint,     new Vector2(200, 200));
        var sJump       = MakeState(rootSM, "Jump",        jump,       new Vector2(200, -100));
        var sDoubleJump = MakeState(rootSM, "DoubleJump",  doubleJump, new Vector2(200, -200));
        var sFalling    = MakeState(rootSM, "Falling",     falling,    new Vector2(500, 0));
        var sLand       = MakeState(rootSM, "HardLanding", land,       new Vector2(500, -100));
        var sRoll       = MakeState(rootSM, "LandingRoll", roll,       new Vector2(500, -200));
        var sLandToRun  = MakeState(rootSM, "LandToRun",   landToRun,  new Vector2(500, -300));
        var sSlide      = MakeState(rootSM, "Slide",       slide,      new Vector2(-100, 100));
        var sWallLeft   = MakeState(rootSM, "WallRunLeft", wallLeft,   new Vector2(-100, 200));
        var sWallRight  = MakeState(rootSM, "WallRunRight",wallRight,  new Vector2(-100, 300));
        var sStunned    = MakeState(rootSM, "Stunned",     stunned,    new Vector2(500, 200));
        var sDying      = MakeState(rootSM, "Dying",       dying,      new Vector2(500, 300));
        var sRunToStop  = MakeState(rootSM, "RunToStop",   runToStop,  new Vector2(400, 50));

        rootSM.defaultState = sIdle;

        // ── Blend Locomotion ──
        // Idle → Run
        T(sIdle, sRun).AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");

        // Run → RunToStop
        T(sRun, sRunToStop).AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

        // RunToStop → Idle (Exit Time)
        MakeExitTransition(sRunToStop, sIdle, 0.9f, 0.15f);

        // RunToStop → Run (Cancel Stop)
        T(sRunToStop, sRun).AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");

        // Run ↔ Sprint
        T(sRun, sSprint).AddCondition(AnimatorConditionMode.Greater, 1.5f, "Speed");
        T(sSprint, sRun).AddCondition(AnimatorConditionMode.Less, 1.5f, "Speed");

        // ── Jump (dari Idle/Run/Sprint/RunToStop saja — bukan dari landing/dying) ──
        // Menggunakan transisi per-state, bukan AnyState, agar tidak konflik
        foreach (var groundState in new[] { sIdle, sRun, sSprint, sRunToStop })
        {
            var t = groundState.AddTransition(sJump);
            t.AddCondition(AnimatorConditionMode.If, 0, "Jump");
            t.hasExitTime = false;
            t.duration    = 0.05f;
        }

        // ── Double Jump (dari airborne state) ──
        foreach (var airState in new[] { sJump, sFalling })
        {
            var t = airState.AddTransition(sDoubleJump);
            t.AddCondition(AnimatorConditionMode.If, 0, "DoubleJump");
            t.hasExitTime = false;
            t.duration    = 0.1f;
        }

        // ── Jump → Falling ──
        var jToF = sJump.AddTransition(sFalling);
        jToF.AddCondition(AnimatorConditionMode.Less, 0f, "VerticalVelocity");
        jToF.AddCondition(AnimatorConditionMode.IfNot, 0, "IsGrounded");
        jToF.hasExitTime = false;
        jToF.duration    = 0.15f;

        // ── DoubleJump → Falling ──
        var djToF = sDoubleJump.AddTransition(sFalling);
        djToF.hasExitTime    = true;
        djToF.exitTime       = 0.8f;
        djToF.duration       = 0.15f;

        // ── Falling → Landing States ──
        SetTransition(sFalling, sRoll,      "Roll",        exitTime: false);
        SetTransition(sFalling, sLand,      "HardLanding", exitTime: false);
        SetTransition(sFalling, sLandToRun, "LandToRun",   exitTime: false);

        // Falling → Idle (normal landing, jika tidak ada trigger lain)
        var fToIdle = sFalling.AddTransition(sIdle);
        fToIdle.AddCondition(AnimatorConditionMode.If, 0, "IsGrounded");
        fToIdle.hasExitTime = false;
        fToIdle.duration    = 0.1f;

        // ── Landing Exits ──
        MakeExitTransition(sLand,      sIdle, 0.95f, 0.15f);
        MakeExitTransition(sRoll,      sRun,  0.9f,  0.1f);
        MakeExitTransition(sLandToRun, sRun,  0.85f, 0.1f);

        // ── Slide (dari Run/Sprint/RunToStop saja) ──
        foreach (var groundMoving in new[] { sRun, sSprint, sRunToStop })
        {
            var t = groundMoving.AddTransition(sSlide);
            t.AddCondition(AnimatorConditionMode.If, 0, "Slide");
            t.hasExitTime = false;
            t.duration    = 0.05f;
        }
        MakeExitTransition(sSlide, sRun, 0.9f, 0.1f);

        // ── Wall Run ──
        // Masuk dari state bergerak dan udara
        foreach (var s in new[] { sRun, sSprint, sJump, sFalling, sRunToStop })
        {
            var tL = s.AddTransition(sWallLeft);
            tL.AddCondition(AnimatorConditionMode.Equals, 1f, "WallRunSide");
            tL.hasExitTime = false;
            tL.duration    = 0.15f;

            var tR = s.AddTransition(sWallRight);
            tR.AddCondition(AnimatorConditionMode.Equals, 2f, "WallRunSide");
            tR.hasExitTime = false;
            tR.duration    = 0.15f;
        }

        // Wall Run keluar → Falling
        SetIntTransition(sWallLeft,  sFalling, "WallRunSide", 0);
        SetIntTransition(sWallRight, sFalling, "WallRunSide", 0);

        // Wall Run → Jump (Wall Jump)
        foreach (var wallState in new[] { sWallLeft, sWallRight })
        {
            var t = wallState.AddTransition(sJump);
            t.AddCondition(AnimatorConditionMode.If, 0, "Jump");
            t.hasExitTime = false;
            t.duration    = 0.1f;
        }

        // ── Stunned (dari semua kecuali Dying) ──
        // AnyState → Stunned aman karena Dying tidak punya exit transition
        var anyStunned = rootSM.AddAnyStateTransition(sStunned);
        anyStunned.AddCondition(AnimatorConditionMode.If, 0, "Stunned");
        anyStunned.hasExitTime = false;
        anyStunned.duration    = 0.1f;

        var stunnedExit = sStunned.AddTransition(sIdle);
        stunnedExit.AddCondition(AnimatorConditionMode.IfNot, 0, "Stunned");
        stunnedExit.hasExitTime = false;
        stunnedExit.duration    = 0.15f;

        // ── Dying: AnyState, tidak ada exit ──
        var anyDying = rootSM.AddAnyStateTransition(sDying);
        anyDying.AddCondition(AnimatorConditionMode.If, 0, "Die");
        anyDying.hasExitTime = false;
        anyDying.duration    = 0.05f;

        AssetDatabase.SaveAssets();
        Debug.Log($"[CreatePlayerAnimator] Berhasil dibuat di: {controllerPath}");
    }

    // ── Helpers ──
    private static void AddParam(AnimatorController ctrl, string name, AnimatorControllerParameterType type)
        => ctrl.AddParameter(name, type);

    private static AnimatorState MakeState(AnimatorStateMachine sm, string name, AnimationClip clip, Vector2 pos)
    {
        var state  = sm.AddState(name, new Vector3(pos.x, pos.y, 0));
        state.motion = clip;
        return state;
    }

    // Shorthand: buat transisi dan return condition builder
    private static AnimatorStateTransition T(AnimatorState from, AnimatorState to, bool hasExit = false, float duration = 0.1f)
    {
        var t = from.AddTransition(to);
        t.hasExitTime = hasExit;
        t.duration    = duration;
        return t;
    }

    private static void SetTransition(AnimatorState from, AnimatorState to, string trigger, bool exitTime = false)
    {
        var t = from.AddTransition(to);
        t.AddCondition(AnimatorConditionMode.If, 0, trigger);
        t.hasExitTime = exitTime;
        t.duration    = 0.05f;
    }

    private static void SetIntTransition(AnimatorState from, AnimatorState to, string param, int value)
    {
        var t = from.AddTransition(to);
        t.AddCondition(AnimatorConditionMode.Equals, value, param);
        t.hasExitTime = false;
        t.duration    = 0.15f;
    }

    private static void MakeExitTransition(AnimatorState from, AnimatorState to, float exitTime, float duration)
    {
        var t = from.AddTransition(to);
        t.hasExitTime = true;
        t.exitTime    = exitTime;
        t.duration    = duration;
    }

    private static AnimationClip LoadClip(string fbxPath)
    {
        var assets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
        foreach (var asset in assets)
        {
            if (asset is AnimationClip clip && !clip.name.Contains("__preview__"))
                return clip;
        }
        Debug.LogWarning($"[CreatePlayerAnimator] Clip tidak ditemukan di: {fbxPath}");
        return null;
    }
}