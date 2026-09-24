#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace EcosDeLaCripta.EditorTools
{
    /// <summary>
    /// Construye Animator Controllers reutilizando las animaciones que ya trae el pack Kenney
    /// (idle/walk/disparo/ataque/muerte) para que el jugador y los enemigos dejen de verse
    /// como maniquíes estáticos.
    /// </summary>
    public static class KenneyAnimatorSetup
    {
        private const string ControllersFolder = "Assets/Art/Kenney/MiniDungeon/Animators";

        public static AnimatorController BuildPlayerController()
        {
            return BuildController(
                "Assets/Art/Kenney/MiniDungeon/Models/character-human.fbx",
                $"{ControllersFolder}/PlayerAnimator.controller",
                attackClipName: "holding-right-shoot");
        }

        public static AnimatorController BuildEnemyController()
        {
            return BuildController(
                "Assets/Art/Kenney/MiniDungeon/Models/character-orc.fbx",
                $"{ControllersFolder}/EnemyAnimator.controller",
                attackClipName: "attack-melee-right");
        }

        private static AnimatorController BuildController(string fbxPath, string controllerPath, string attackClipName)
        {
            EnsureFolder();

            AnimationClip idle = LoadClip(fbxPath, "idle");
            AnimationClip walk = LoadClip(fbxPath, "walk");
            AnimationClip attack = LoadClip(fbxPath, attackClipName);
            AnimationClip die = LoadClip(fbxPath, "die");

            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath) != null)
            {
                AssetDatabase.DeleteAsset(controllerPath);
            }

            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            AnimatorStateMachine root = controller.layers[0].stateMachine;

            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Die", AnimatorControllerParameterType.Trigger);

            AnimatorState idleState = root.AddState("Idle");
            idleState.motion = idle;
            root.defaultState = idleState;

            AnimatorState walkState = root.AddState("Walk");
            walkState.motion = walk;
            walkState.speed = 1.3f;

            AnimatorStateTransition toWalk = idleState.AddTransition(walkState);
            toWalk.hasExitTime = false;
            toWalk.duration = 0.12f;
            toWalk.AddCondition(AnimatorConditionMode.Greater, 0.15f, "Speed");

            AnimatorStateTransition toIdle = walkState.AddTransition(idleState);
            toIdle.hasExitTime = false;
            toIdle.duration = 0.12f;
            toIdle.AddCondition(AnimatorConditionMode.Less, 0.15f, "Speed");

            if (attack != null)
            {
                AnimatorState attackState = root.AddState("Attack");
                attackState.motion = attack;

                AnimatorStateTransition anyToAttack = root.AddAnyStateTransition(attackState);
                anyToAttack.hasExitTime = false;
                anyToAttack.duration = 0.05f;
                anyToAttack.canTransitionToSelf = false;
                anyToAttack.AddCondition(AnimatorConditionMode.If, 0, "Attack");

                AnimatorStateTransition attackToIdle = attackState.AddTransition(idleState);
                attackToIdle.hasExitTime = true;
                attackToIdle.exitTime = 0.85f;
                attackToIdle.duration = 0.1f;
            }

            if (die != null)
            {
                AnimatorState dieState = root.AddState("Die");
                dieState.motion = die;

                AnimatorStateTransition anyToDie = root.AddAnyStateTransition(dieState);
                anyToDie.hasExitTime = false;
                anyToDie.duration = 0.05f;
                anyToDie.canTransitionToSelf = false;
                anyToDie.AddCondition(AnimatorConditionMode.If, 0, "Die");
            }

            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static AnimationClip LoadClip(string fbxPath, string clipName)
        {
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
            {
                if (asset is AnimationClip clip && clip.name == clipName) return clip;
            }
            Debug.LogWarning($"[KenneyAnimatorSetup] No se encontró el clip '{clipName}' en {fbxPath}");
            return null;
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Art/Kenney/MiniDungeon/Animators"))
            {
                AssetDatabase.CreateFolder("Assets/Art/Kenney/MiniDungeon", "Animators");
            }
        }
    }
}
#endif
