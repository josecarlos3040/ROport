using UnityEngine;

public class SpriteBillboard : MonoBehaviour
{
    [Header("Camera")]
    [SerializeField] private Camera targetCamera;

    [Header("Trick Animation")]
    [SerializeField] private Transform trickRoot;

    [SerializeField] private bool simulateTrickSpin = true;

    [SerializeField] private SpriteRenderer spriteRenderer;

    [SerializeField, Range(0.05f, 1f)]
    private float minimumWidth = 0.12f;

    private Vector3 originalScale;
    private float originalTrickYaw;
    private bool originalFlipX;

    private void Awake()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

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

        // Faz o sprite olhar horizontalmente para a camera,
        // mantendo a orientacao vertical no mundo.
        Vector3 facingDirection =
            -targetCamera.transform.forward;

        facingDirection.y = 0f;

        if (facingDirection.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(
                facingDirection.normalized,
                Vector3.up);
        }

        if (!simulateTrickSpin || trickRoot == null)
            return;

        // Converte o giro 3D do TrickController
        // em um efeito visual de giro 2D.
        float yaw = Mathf.DeltaAngle(
            originalTrickYaw,
            trickRoot.localEulerAngles.y
        );

        float widthFactor = Mathf.Cos(
            yaw * Mathf.Deg2Rad
        );

        // Simula o personagem virando de lado.
        float width = Mathf.Max(
            minimumWidth,
            Mathf.Abs(widthFactor)
        );

        transform.localScale = new Vector3(
            originalScale.x * width,
            originalScale.y,
            originalScale.z
        );

        // Espelha o desenho na segunda metade do giro.
        if (spriteRenderer != null)
        {
            spriteRenderer.flipX =
                originalFlipX ^ (widthFactor < 0f);
        }
    }

    private void OnDisable()
    {
        transform.localScale = originalScale;

        if (spriteRenderer != null)
            spriteRenderer.flipX = originalFlipX;
    }
}