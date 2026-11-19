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

    [Header("Animation Durations")]

    [SerializeField, Min(0.1f)]
    private float fallDuration = 1f;

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

    private bool deathActive;
    private bool trickActive;
    private RobotAnimation trickAnimation;

    private bool temporaryActive;
    private RobotAnimation temporaryAnimation;
    private float temporaryTimer;

    // A camera consulta essa propriedade.
    public bool IsFallAnimationActive =>
        deathActive ||
        (temporaryActive &&
         (temporaryAnimation == RobotAnimation.Impact ||
          temporaryAnimation == RobotAnimation.Fall));

    private void Awake()
    {
        if (routeFollower == null)
            routeFollower = GetComponent<RouteFollower>();

        if (spriteAnimator != null)
            spriteAnimator.applyRootMotion = false;
    }

    private void OnEnable()
    {
        if (cityProgressManager != null)
        {
            cityProgressManager.PhotoRecorded +=
                OnPhotoRecorded;
        }

        if (runManager != null)
        {
            runManager.PhaseChanged += OnPhaseChanged;
        }
    }

    private void OnDisable()
    {
        if (cityProgressManager != null)
        {
            cityProgressManager.PhotoRecorded -=
                OnPhotoRecorded;
        }

        if (runManager != null)
        {
            runManager.PhaseChanged -= OnPhaseChanged;
        }
    }

    private void Update()
    {
        if (spriteAnimator == null)
            return;

        // MORTE: nenhuma outra animacao pode
        // sobrescrever ROFall.
        if (deathActive)
        {
            PlayState(RobotAnimation.Fall);
            return;
        }

        if (runManager == null)
            return;

        // Fora da execucao, fica parado.
        if (runManager.Phase != RunPhase.Executing)
        {
            trickActive = false;
            temporaryActive = false;

            PlayState(RobotAnimation.Stay);
            return;
        }

        // Fotografias e impactos.
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

        // Salto ou grind.
        if (trickActive)
        {
            PlayState(trickAnimation);
            return;
        }

        // Movimento normal.
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
        if (deathActive)
            return;

        trickActive = true;

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

    public void PlayRampFall()
    {
        PlayTemporary(
            RobotAnimation.Fall,
            fallDuration);
    }

    // Chamado quando o personagem MORRE.
    public void PlayDeath()
    {
        if (deathActive)
            return;

        deathActive = true;
        trickActive = false;
        temporaryActive = false;
        temporaryTimer = 0f;

        currentStateHash = 0;

        PlayState(RobotAnimation.Fall);

        Debug.Log("[ROport] Animacao de morte iniciada.");
    }

    public void ResetDeath()
    {
        deathActive = false;
        trickActive = false;
        temporaryActive = false;
        temporaryTimer = 0f;

        currentStateHash = 0;
    }

    private void OnPhaseChanged(RunPhase phase)
    {
        if (phase == RunPhase.Planning)
            ResetDeath();
    }

    private void OnPhotoRecorded(CityProblem problem)
    {
        PlayPhoto();
    }

    private void PlayTemporary(
        RobotAnimation animation,
        float duration)
    {
        if (deathActive ||
            runManager == null ||
            runManager.Phase != RunPhase.Executing)
        {
            return;
        }

        temporaryAnimation = animation;
        temporaryTimer = duration;
        temporaryActive = true;

        // Permite reiniciar um impacto consecutivo.
        currentStateHash = 0;

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

        if (currentStateHash == hash)
            return;

        if (!spriteAnimator.HasState(0, hash))
        {
            if (missingStates.Add(hash))
            {
                Debug.LogWarning(
                    "[ROport] Estado nao encontrado: " +
                    animation,
                    spriteAnimator);
            }

            return;
        }

        spriteAnimator.Play(hash, 0, 0f);
        currentStateHash = hash;
    }

    // Teste manual pelo Inspector.
    [ContextMenu("ROport - Testar Animacao de Morte")]
    private void TestDeath()
    {
        if (Application.isPlaying)
            PlayDeath();
    }
}