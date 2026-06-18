using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.IO;

// ============================================================
//  CREATE RUNNER ANIMATOR CONTROLLER
//  Menu: Tools > Create Runner Animator Controller
//
//  Membuat Animator Controller untuk endless runner menggunakan
//  animasi dari folder "Assets/motion animation/".
//
//  State Machine:
//  ┌────────────┐
//  │  FastRun   │ ← Default (karakter selalu lari)
//  └─────┬──────┘
//     ┌──┴───┐ ┌──────┐ ┌──────┐
//     │Sprint│ │ Jump │ │Slide │
//     └──────┘ └──┬───┘ └──────┘
//                 │
//           ┌─────▼─────┐
//           │  Falling   │
//           └─────┬──────┘
//                 │
//           ┌─────▼──────┐
//           │  Landing   │
//           └────────────┘
//
//  + AnyState → Stunned (bool)
//  + AnyState → Dying (trigger)
// ============================================================
public static class CreateRunnerAnimator
{
    private const string AnimFolder     = "Assets/motion animation/";
    private const string OutputFolder   = "Assets/Animations";
    private const string ControllerName = "RunnerAnimatorController.controller";

    [MenuItem("Tools/Create Runner Animator Controller")]
    public static void CreateController()
    {
        string controllerPath = OutputFolder + "/" + ControllerName;

        if (!Directory.Exists(OutputFolder))
            Directory.CreateDirectory(OutputFolder);

        var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        if (controller == null)
        {
            Debug.LogError("[CreateRunnerAnimator] Gagal membuat controller di: " + controllerPath);
            return;
        }

        // ════════════════════════════════════════════
        //  PARAMETERS
        // ════════════════════════════════════════════
        AddParam(controller, "Speed",            AnimatorControllerParameterType.Float);
        AddParam(controller, "IsGrounded",       AnimatorControllerParameterType.Bool);
        AddParam(controller, "VerticalVelocity", AnimatorControllerParameterType.Float);
        AddParam(controller, "Jump",             AnimatorControllerParameterType.Trigger);
        AddParam(controller, "Slide",            AnimatorControllerParameterType.Trigger);
        AddParam(controller, "Stunned",          AnimatorControllerParameterType.Bool);
        AddParam(controller, "Die",              AnimatorControllerParameterType.Trigger);

        var rootSM = controller.layers[0].stateMachine;

        // ════════════════════════════════════════════
        //  LOAD ANIMATION CLIPS
        // ════════════════════════════════════════════
        var clipFastRun  = LoadClip(AnimFolder + "Fast Run.fbx");
        var clipSprint   = LoadClip(AnimFolder + "Sprint.fbx");
        var clipJump     = LoadClip(AnimFolder + "Running Jump.fbx");
        var clipSlide    = LoadClip(AnimFolder + "Running Slide.fbx");
        var clipFalling  = LoadClip(AnimFolder + "Falling Idle.fbx");
        var clipLanding  = LoadClip(AnimFolder + "Falling To Landing.fbx");
        var clipStunned  = LoadClip(AnimFolder + "Stunned.fbx");
        var clipDying    = LoadClip(AnimFolder + "Dying.fbx");

        // ════════════════════════════════════════════
        //  CREATE STATES
        // ════════════════════════════════════════════
        var sRun      = MakeState(rootSM, "Run",      clipFastRun,  new Vector2(0, 0));
        var sSprint   = MakeState(rootSM, "Sprint",   clipSprint,   new Vector2(0, 120));
        var sJump     = MakeState(rootSM, "Jump",     clipJump,     new Vector2(300, -120));
        var sSlide    = MakeState(rootSM, "Slide",    clipSlide,    new Vector2(-300, 0));
        var sFalling  = MakeState(rootSM, "Falling",  clipFalling,  new Vector2(300, 0));
        var sLanding  = MakeState(rootSM, "Landing",  clipLanding,  new Vector2(300, 120));
        var sStunned  = MakeState(rootSM, "Stunned",  clipStunned,  new Vector2(-300, 200));
        var sDying    = MakeState(rootSM, "Dying",    clipDying,    new Vector2(-300, 320));

        // Default state = Run (karakter selalu lari)
        rootSM.defaultState = sRun;

        // ════════════════════════════════════════════
        //  LOCOMOTION: Run ↔ Sprint (speed boost)
        // ════════════════════════════════════════════
        var runToSprint = T(sRun, sSprint);
        runToSprint.AddCondition(AnimatorConditionMode.Greater, 1.5f, "Speed");

        var sprintToRun = T(sSprint, sRun);
        sprintToRun.AddCondition(AnimatorConditionMode.Less, 1.5f, "Speed");

        // ════════════════════════════════════════════
        //  JUMP (dari Run dan Sprint)
        // ════════════════════════════════════════════
        foreach (var groundState in new[] { sRun, sSprint })
        {
            var t = groundState.AddTransition(sJump);
            t.AddCondition(AnimatorConditionMode.If, 0, "Jump");
            t.hasExitTime = false;
            t.duration    = 0.05f;
        }

        // Jump → Falling (saat mulai turun)
        var jumpToFall = sJump.AddTransition(sFalling);
        jumpToFall.AddCondition(AnimatorConditionMode.Less, 0f, "VerticalVelocity");
        jumpToFall.AddCondition(AnimatorConditionMode.IfNot, 0, "IsGrounded");
        jumpToFall.hasExitTime = false;
        jumpToFall.duration    = 0.15f;

        // ════════════════════════════════════════════
        //  FALLING → LANDING → RUN
        // ════════════════════════════════════════════
        // Falling → Landing (saat menyentuh tanah)
        var fallToLand = sFalling.AddTransition(sLanding);
        fallToLand.AddCondition(AnimatorConditionMode.If, 0, "IsGrounded");
        fallToLand.hasExitTime = false;
        fallToLand.duration    = 0.05f;

        // Landing → Run (setelah animasi landing selesai)
        var landToRun = sLanding.AddTransition(sRun);
        landToRun.hasExitTime = true;
        landToRun.exitTime    = 0.85f;
        landToRun.duration    = 0.15f;

        // Fallback: Falling langsung ke Run jika sudah grounded + bergerak
        var fallToRun = sFalling.AddTransition(sRun);
        fallToRun.AddCondition(AnimatorConditionMode.If, 0, "IsGrounded");
        fallToRun.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
        fallToRun.hasExitTime = false;
        fallToRun.duration    = 0.1f;

        // ════════════════════════════════════════════
        //  SLIDE (dari Run dan Sprint)
        // ════════════════════════════════════════════
        foreach (var groundMoving in new[] { sRun, sSprint })
        {
            var t = groundMoving.AddTransition(sSlide);
            t.AddCondition(AnimatorConditionMode.If, 0, "Slide");
            t.hasExitTime = false;
            t.duration    = 0.05f;
        }

        // Slide → Run (setelah animasi slide selesai)
        var slideToRun = sSlide.AddTransition(sRun);
        slideToRun.hasExitTime = true;
        slideToRun.exitTime    = 0.9f;
        slideToRun.duration    = 0.1f;

        // ════════════════════════════════════════════
        //  STUNNED (AnyState → Stunned → Run)
        // ════════════════════════════════════════════
        var anyToStunned = rootSM.AddAnyStateTransition(sStunned);
        anyToStunned.AddCondition(AnimatorConditionMode.If, 0, "Stunned");
        anyToStunned.hasExitTime = false;
        anyToStunned.duration    = 0.1f;

        var stunnedToRun = sStunned.AddTransition(sRun);
        stunnedToRun.AddCondition(AnimatorConditionMode.IfNot, 0, "Stunned");
        stunnedToRun.hasExitTime = false;
        stunnedToRun.duration    = 0.15f;

        // ════════════════════════════════════════════
        //  DYING (AnyState → Dying, terminal)
        // ════════════════════════════════════════════
        var anyToDying = rootSM.AddAnyStateTransition(sDying);
        anyToDying.AddCondition(AnimatorConditionMode.If, 0, "Die");
        anyToDying.hasExitTime = false;
        anyToDying.duration    = 0.05f;

        // ════════════════════════════════════════════
        //  SAVE
        // ════════════════════════════════════════════
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[CreateRunnerAnimator] ✅ Berhasil dibuat di: {controllerPath}");
        Selection.activeObject = controller;
    }

    // ════════════════════════════════════════════
    //  HELPER METHODS
    // ════════════════════════════════════════════

    private static void AddParam(AnimatorController ctrl, string name, AnimatorControllerParameterType type)
        => ctrl.AddParameter(name, type);

    private static AnimatorState MakeState(AnimatorStateMachine sm, string name, AnimationClip clip, Vector2 pos)
    {
        var state  = sm.AddState(name, new Vector3(pos.x, pos.y, 0));
        state.motion = clip;
        return state;
    }

    private static AnimatorStateTransition T(AnimatorState from, AnimatorState to, bool hasExit = false, float duration = 0.1f)
    {
        var t = from.AddTransition(to);
        t.hasExitTime = hasExit;
        t.duration    = duration;
        return t;
    }

    private static AnimationClip LoadClip(string fbxPath)
    {
        var assets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
        if (assets == null || assets.Length == 0)
        {
            Debug.LogWarning($"[CreateRunnerAnimator] ⚠ Tidak ada asset di: {fbxPath}");
            return null;
        }

        foreach (var asset in assets)
        {
            if (asset is AnimationClip clip && !clip.name.Contains("__preview__"))
                return clip;
        }

        Debug.LogWarning($"[CreateRunnerAnimator] ⚠ AnimationClip tidak ditemukan di: {fbxPath}");
        return null;
    }
}
