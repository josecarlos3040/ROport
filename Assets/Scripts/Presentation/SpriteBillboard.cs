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

    private float extraRoll;

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

        UpdateBillboardRotation();
        UpdateTrickSpin();
    }

    // Mantem o sprite vertical e voltado para a camera.
    private void UpdateBillboardRotation()
    {
        Vector3 direction = -targetCamera.transform.forward;

        // Ignora a inclinacao vertical da camera.
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.0001f)
            return;

        Quaternion facingRotation = Quaternion.LookRotation(
            direction.normalized,
            Vector3.up);

        // A inclinacao adicional representa
        // tropeços e quedas durante manobras.
        Quaternion rollRotation = Quaternion.AngleAxis(
            extraRoll,
            Vector3.forward);

        transform.rotation = facingRotation * rollRotation;
    }

    // Simula o personagem girando de lado
    // durante uma manobra de 360 graus.
    private void UpdateTrickSpin()
    {
        if (!simulateTrickSpin || trickRoot == null)
        {
            RestoreSpinAppearance();
            return;
        }

        float yaw = Mathf.DeltaAngle(
            originalTrickYaw,
            trickRoot.localEulerAngles.y);

        float widthFactor = Mathf.Cos(
            yaw * Mathf.Deg2Rad);

        float width = Mathf.Max(
            minimumWidth,
            Mathf.Abs(widthFactor));

        transform.localScale = new Vector3(
            originalScale.x * width,
            originalScale.y,
            originalScale.z);

        if (spriteRenderer != null)
        {
            spriteRenderer.flipX =
                originalFlipX ^ (widthFactor < 0f);
        }
    }

    // Usado pelo TrickController
    // para inclinar o robô durante erros.
    public void SetExtraRoll(float angle)
    {
        extraRoll = angle;
    }

    private void RestoreSpinAppearance()
    {
        transform.localScale = originalScale;

        if (spriteRenderer != null)
            spriteRenderer.flipX = originalFlipX;
    }

    private void OnDisable()
    {
        extraRoll = 0f;
        RestoreSpinAppearance();
    }
}