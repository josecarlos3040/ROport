using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(150)]
public class RobotAnimationController : MonoBehaviour
{
    private enum RobotAnimation
    {
        Stay,
        Move,
        Jump,
        Slide,
        Photo,
        Impact,
        Fall
    }

    [Header("References")]
    [SerializeField] private Animator spriteAnimator;
    [SerializeField] private RouteFollower routeFollower;
    [SerializeField] private ROportRunManager runManager;
    [SerializeField] private CityProgressManager cityProgressManager;
    [SerializeField] private RobotAnimationController robotAnimation;

    [Header("One-Shot Durations")]
    [SerializeField, Min(0.1f)]
    private float photoDuration = 0.6f;

    [SerializeField, Min(0.1f)]
    private float impactDuration = 0.6f;

    private static readonly int StayHash =
        Animator.StringToHash("Base Layer.ROStay");

    private static readonly int MoveHash =
        Animator.StringToHash("Base Layer.ROMove");

    private static readonly int JumpHash =
        Animator.StringToHash("Base Layer.ROJump");

    private static readonly int SlideHash =
        Animator.StringToHash("Base Layer.ROSlide");

    private static readonly int PhotoHash =
        Animator.StringToHash("Base Layer.ROPhoto");

    private static readonly int ImpactHash =
        Animator.StringToHash("Base Layer.ROImpact");

    private static readonly int FallHash =
        Animator.StringToHash("Base Layer.ROFall");

    private readonly HashSet<int> missingStates =
        new HashSet<int>();

    private int currentStateHash;

    private bool trickActive;
    private RobotAnimation trickAnimation;

    private bool temporaryActive;
    private RobotAnimation temporaryAnimation;
    private float temporaryTimer;

    private void Awake()
    {
        if (routeFollower == null)
            routeFollower = GetComponent<RouteFollower>();

        if (spriteAnimator != null)
            spriteAnimator.applyRootMotion = false;

        if (robotAnimation == null)
            robotAnimation = GetComponent<RobotAnimationController>();
    }

    private void OnEnable()
    {
        if (cityProgressManager != null)
        {
            cityProgressManager.PhotoRecorded +=
                OnPhotoRecorded;
        }
    }

    private void OnDisable()
    {
        if (cityProgressManager != null)
        {
            cityProgressManager.PhotoRecorded -=
                OnPhotoRecorded;
        }
    }

    private void Update()
    {
        if (spriteAnimator == null || runManager == null)
            return;

        // Morte tem prioridade sobre qualquer animacao.
        if (runManager.Phase == RunPhase.Dead)
        {
            PlayState(RobotAnimation.Fall);
            return;
        }

        // Fora da execucao, o robo fica parado.
        if (runManager.Phase != RunPhase.Executing)
        {
            trickActive = false;
            temporaryActive = false;
            temporaryTimer = 0f;

            PlayState(RobotAnimation.Stay);
            return;
        }

        // Animacoes temporarias, como foto e impacto.
        if (temporaryActive)
        {
            temporaryTimer -= Time.deltaTime;

            if (temporaryTimer > 0f)
            {
                PlayState(temporaryAnimation);
                return;
            }

            temporaryActive = false;
        }

        // Manobra ativa enquanto percorre sua zona.
        if (trickActive)
        {
            PlayState(trickAnimation);
            return;
        }

        // Movimento automatico pela rota.
        bool moving =
            routeFollower != null &&
            routeFollower.IsFollowing &&
            !routeFollower.IsBlocked;

        PlayState(
            moving
                ? RobotAnimation.Move
                : RobotAnimation.Stay);
    }

    public void BeginTrick(TrickAnimation animation)
    {
        trickActive = true;

        // Grind usa slide.
        // Jump, Spin e JumpAndSpin usam Jump
        // ate termos animacoes especificas para giro.
        trickAnimation =
            animation == TrickAnimation.Grind
                ? RobotAnimation.Slide
                : RobotAnimation.Jump;

        if (!temporaryActive)
            PlayState(trickAnimation);
    }

    public void EndTrick()
    {
        trickActive = false;
    }

    public void PlayImpact()
    {
        PlayTemporary(
            RobotAnimation.Impact,
            impactDuration);
    }

    public void PlayPhoto()
    {
        PlayTemporary(
            RobotAnimation.Photo,
            photoDuration);
    }

    private void OnPhotoRecorded(CityProblem problem)
    {
        PlayPhoto();
    }

    private void PlayTemporary(
        RobotAnimation animation,
        float duration)
    {
        if (runManager == null ||
            runManager.Phase != RunPhase.Executing)
        {
            return;
        }

        temporaryAnimation = animation;
        temporaryTimer = Mathf.Max(0.1f, duration);
        temporaryActive = true;

        PlayState(animation);
    }

    private int GetStateHash(RobotAnimation animation)
    {
        switch (animation)
        {
            case RobotAnimation.Stay:
                return StayHash;

            case RobotAnimation.Move:
                return MoveHash;

            case RobotAnimation.Jump:
                return JumpHash;

            case RobotAnimation.Slide:
                return SlideHash;

            case RobotAnimation.Photo:
                return PhotoHash;

            case RobotAnimation.Impact:
                return ImpactHash;

            case RobotAnimation.Fall:
                return FallHash;

            default:
                return StayHash;
        }
    }

    private void PlayState(RobotAnimation animation)
    {
        if (spriteAnimator == null ||
            !spriteAnimator.isActiveAndEnabled ||
            spriteAnimator.runtimeAnimatorController == null)
        {
            currentStateHash = 0;
            return;
        }

        int hash = GetStateHash(animation);

        // Evita reiniciar o clip a cada frame.
        if (currentStateHash == hash)
            return;

        if (!spriteAnimator.HasState(0, hash))
        {
            if (missingStates.Add(hash))
            {
                Debug.LogWarning(
                    "[ROport Animation] Estado nao encontrado: " +
                    animation,
                    spriteAnimator);
            }

            return;
        }

        spriteAnimator.Play(hash, 0, 0f);
        currentStateHash = hash;
    }
}