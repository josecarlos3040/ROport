using UnityEngine;

public class SpriteBillboard : MonoBehaviour
{
    [Header("Camera")]
    [SerializeField] private Camera targetCamera;

    [Header("Trick Animation")]
    [SerializeField] private Transform trickRoot;

    [SerializeField] private bool simulateTrickSpin = true;

    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("Movement Flip")]
    [SerializeField] private RouteFollower routeFollower;
    [SerializeField] private ROportRunManager runManager;
    [SerializeField] private bool flipWithMovement = true;
    [Tooltip("Ative se o desenho original olha para a direita da tela.")]
    [SerializeField] private bool spriteFacesRight = true;
    [SerializeField, Range(0.01f, 0.8f)]
    private float horizontalDirectionThreshold = 0.15f;

    [SerializeField, Range(0.05f, 1f)]
    private float minimumWidth = 0.12f;

    private Vector3 originalScale;
    private float originalTrickYaw;
    private bool originalFlipX;
    private bool movementFlip;

    private float extraRoll = 0f;

    // Permite que o TrickController incline o sprite.
    public void SetExtraRoll(float angle)
    {
        extraRoll = angle;
    }

    private void Awake()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        if (routeFollower == null)
            routeFollower = GetComponentInParent<RouteFollower>();

        // RunManager e opcional. Se quiser, atribua no Inspector.

        originalScale = transform.localScale;

        if (trickRoot != null)
            originalTrickYaw = trickRoot.localEulerAngles.y;

        if (spriteRenderer != null)
            originalFlipX = spriteRenderer.flipX;
    }

    private void LateUpdate()
    {
        if (targetCamera == null)
            return;

        // Mantem o Sprite como billboard, sempre olhando para a camera.
        Vector3 facingDirection = -targetCamera.transform.forward;
        facingDirection.y = 0f;

        if (facingDirection.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(
                facingDirection.normalized, Vector3.up);
        }

        // Inclina o sprite durante grind/queda, mesmo sem simular spin.
        transform.rotation *= Quaternion.AngleAxis(
            extraRoll, Vector3.forward);

        UpdateMovementFlip();

        // Preserva o efeito de giro ja usado pelas manobras.
        float widthFactor = 1f;
        if (simulateTrickSpin && trickRoot != null)
        {
            float yaw = Mathf.DeltaAngle(
                originalTrickYaw, trickRoot.localEulerAngles.y);
            widthFactor = Mathf.Cos(yaw * Mathf.Deg2Rad);
        }

        float width = Mathf.Max(
            minimumWidth, Mathf.Abs(widthFactor));

        transform.localScale = new Vector3(
            originalScale.x * width,
            originalScale.y,
            originalScale.z);

        // O espelhamento da movimentacao e do spin sao combinados,
        // em vez de um sobrescrever o outro a cada frame.
        if (spriteRenderer != null)
        {
            bool spinFlip =
                simulateTrickSpin && trickRoot != null &&
                widthFactor < 0f;

            spriteRenderer.flipX =
                originalFlipX ^ movementFlip ^ spinFlip;
        }
    }

    private void UpdateMovementFlip()
    {
        if (!flipWithMovement || routeFollower == null ||
            !routeFollower.IsFollowing)
            return;

        if (runManager != null &&
            runManager.Phase != RunPhase.Executing)
            return;

        Vector3 direction = routeFollower.CurrentMoveDirection;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
            return;

        // Direita na tela, relativa a posicao atual da camera.
        Vector3 cameraRight = targetCamera.transform.right;
        cameraRight.y = 0f;
        if (cameraRight.sqrMagnitude < 0.0001f)
            return;

        float horizontal = Vector3.Dot(
            direction.normalized, cameraRight.normalized);

        // Perto de movimentos para frente/tras mantem o ultimo flip.
        if (Mathf.Abs(horizontal) < horizontalDirectionThreshold)
            return;

        bool movingLeftOnScreen = horizontal < 0f;
        movementFlip = spriteFacesRight
            ? movingLeftOnScreen
            : !movingLeftOnScreen;
    }

    private void OnDisable()
    {
        transform.localScale = originalScale;

        if (spriteRenderer != null)
            spriteRenderer.flipX = originalFlipX;
    }
}