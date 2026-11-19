using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Camera))]
public class ROportCameraController : MonoBehaviour
{
    private enum CameraView
    {
        Overview,
        RouteFocus,
        Following,
        Finished
    }

    [Header("References")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private Transform player;
    [SerializeField] private Transform cityCenter;
    [SerializeField] private RouteDrawer routeDrawer;
    [SerializeField] private ROportRunManager runManager;

    [Header("Visibility")]
    [SerializeField] private LayerMask problemVisualLayers;
    [SerializeField] private GameObject playerSpriteVisual;
    [SerializeField] private GameObject mapMarker;

    [Header("Overview Camera")]
    [SerializeField] private float overviewSize = 28f;
    [SerializeField] private float overviewDistance = 60f;

    [Header("Drawing Camera")]
    [SerializeField] private float drawingSize = 16f;

    [SerializeField, Range(0f, 90f)]
    private float drawingPitch = 65f;

    [SerializeField, Range(-180f, 180f)]
    private float drawingYaw = 30f;

    [SerializeField, Range(-30f, 30f)]
    private float drawingRoll = 0f;

    [SerializeField] private float drawingDistance = 26f;

    [Header("Execution Camera - First Floor")]
    [SerializeField] private float executionSize = 9f;

    [SerializeField, Range(-180f, 180f)]
    private float executionOrbitAngle = 80f;

    [SerializeField, Min(1f)]
    private float executionDistance = 12f;

    [SerializeField] private float executionHeight = 7f;
    [SerializeField] private float executionLookHeight = 1f;
    [SerializeField] private float lookAhead = 1.5f;

    [Header("Second Floor Camera")]
    [SerializeField] private bool enableSecondFloorView = true;

    [Tooltip("Altura Y do chao para entrar no modo superior.")]
    [SerializeField] private float secondFloorEnterHeight = 8.5f;

    [Tooltip("Altura Y do chao para voltar ao modo inferior.")]
    [SerializeField] private float secondFloorExitHeight = 7f;

    [Tooltip("90 seria completamente de cima.")]
    [SerializeField, Range(45f, 89f)]
    private float upperFloorPitch = 82f;

    [SerializeField, Min(1f)]
    private float upperFloorHeight = 14f;

    [SerializeField, Min(1f)]
    private float upperFloorSize = 10f;

    [SerializeField, Min(0f)]
    private float floorTransitionDuration = 1f;

    [Header("Player")]
    [SerializeField] private float playerPivotHeight = 1f;

    [Header("Smoothing")]
    [SerializeField, Min(0.01f)]
    private float positionSmoothTime = 0.35f;

    [SerializeField, Min(0.01f)]
    private float zoomSmoothTime = 0.4f;

    [SerializeField, Min(0.1f)]
    private float rotationSpeed = 5f;

    private CameraView currentView = CameraView.Overview;

    private Vector3 positionVelocity;
    private float zoomVelocity;

    private int originalCullingMask;
    private bool originalSpriteActive;
    private bool originalMarkerActive;

    // Controla a transicao entre os andares.
    private bool isOnUpperFloor;
    private float upperFloorBlend;

    private void Awake()
    {
        if (mainCamera == null)
            mainCamera = GetComponent<Camera>();

        mainCamera.orthographic = true;

        originalCullingMask = mainCamera.cullingMask;

        if (playerSpriteVisual != null)
            originalSpriteActive = playerSpriteVisual.activeSelf;

        if (mapMarker != null)
            originalMarkerActive = mapMarker.activeSelf;
    }

    private void OnEnable()
    {
        if (runManager != null)
            runManager.PhaseChanged += OnPhaseChanged;
    }

    private void Start()
    {
        if (runManager == null || player == null)
        {
            Debug.LogWarning(
                "[ROport Camera] Referencias incompletas.",
                this);
        }

        if (runManager != null &&
            runManager.Phase == RunPhase.Executing)
        {
            ChangeView(CameraView.Following);
        }
        else if (runManager != null &&
                 runManager.Phase == RunPhase.Finished)
        {
            ChangeView(CameraView.Finished);
        }
        else
        {
            ChangeView(CameraView.Overview);
        }

        UpdateCamera(true);
    }

    private void LateUpdate()
    {
        // Preserva a camera durante o desenho.
        bool isDrawing =
            runManager != null &&
            runManager.Phase == RunPhase.Planning &&
            Mouse.current != null &&
            Mouse.current.leftButton.isPressed;

        if (isDrawing)
            return;

        UpdateCamera(false);
    }

    private void OnPhaseChanged(RunPhase phase)
    {
        switch (phase)
        {
            case RunPhase.Planning:
                ChangeView(CameraView.Overview);
                break;

            case RunPhase.Executing:
                ChangeView(CameraView.Following);
                break;

            case RunPhase.Finished:
                ChangeView(CameraView.Finished);
                break;
        }
    }

    private void ChangeView(CameraView newView)
    {
        currentView = newView;

        positionVelocity = Vector3.zero;
        zoomVelocity = 0f;

        if (newView == CameraView.Overview ||
            newView == CameraView.RouteFocus)
        {
            isOnUpperFloor = false;
            upperFloorBlend = 0f;
        }

        UpdateVisibility();
    }

    private void UpdateVisibility()
    {
        bool overview =
            currentView == CameraView.Overview;

        bool executing =
            currentView == CameraView.Following ||
            currentView == CameraView.Finished;

        if (mainCamera != null)
        {
            mainCamera.cullingMask = executing
                ? originalCullingMask
                : originalCullingMask &
                  ~problemVisualLayers.value;
        }

        if (playerSpriteVisual != null)
            playerSpriteVisual.SetActive(!overview);

        if (mapMarker != null)
            mapMarker.SetActive(overview);
    }

    private Vector3 GetPlayerGroundPosition()
    {
        if (player == null)
            return Vector3.zero;

        return player.position -
            Vector3.up * playerPivotHeight;
    }

    private Vector3 GetPlayerForward()
    {
        if (player == null)
            return Vector3.forward;

        Vector3 forward = player.forward;
        forward.y = 0f;

        if (forward.sqrMagnitude < 0.001f)
            return Vector3.forward;

        return forward.normalized;
    }

    // NOVO: verifica em qual andar o jogador esta.
    private void UpdateFloorState(bool instant)
    {
        if (!enableSecondFloorView || player == null)
        {
            isOnUpperFloor = false;
            upperFloorBlend = 0f;
            return;
        }

        float groundHeight = GetPlayerGroundPosition().y;

        // Usa dois limites diferentes para impedir
        // que a camera fique alternando sem parar.
        if (isOnUpperFloor)
        {
            if (groundHeight <= secondFloorExitHeight)
                isOnUpperFloor = false;
        }
        else
        {
            if (groundHeight >= secondFloorEnterHeight)
                isOnUpperFloor = true;
        }

        float targetBlend = isOnUpperFloor ? 1f : 0f;

        if (instant || floorTransitionDuration <= 0f)
        {
            upperFloorBlend = targetBlend;
            return;
        }

        upperFloorBlend = Mathf.MoveTowards(
            upperFloorBlend,
            targetBlend,
            Time.deltaTime / floorTransitionDuration);
    }

    private void UpdateCamera(bool instant)
    {
        if (mainCamera == null)
            return;

        Vector3 target;
        Vector3 desiredPosition;
        Quaternion desiredRotation;
        float desiredSize;

        if (currentView == CameraView.Overview)
        {
            // Visao geral da cidade.
            target = cityCenter != null
                ? cityCenter.position
                : Vector3.zero;

            desiredRotation = Quaternion.Euler(
                drawingPitch,
                drawingYaw,
                drawingRoll);

            desiredPosition = target -
                desiredRotation * Vector3.forward *
                overviewDistance;

            desiredSize = overviewSize;
        }
        else if (currentView == CameraView.RouteFocus)
        {
            target = GetPlayerGroundPosition();

            desiredRotation = Quaternion.Euler(
                drawingPitch, 0f, 0f);

            desiredPosition = target -
                desiredRotation * Vector3.forward *
                drawingDistance;

            desiredSize = drawingSize;
        }
        else
        {
            // Camera de acompanhamento.
            // Agora considera primeiro e segundo andar.

            UpdateFloorState(instant);

            Vector3 forward = GetPlayerForward();

            Vector3 right = Vector3.Cross(
                Vector3.up, forward).normalized;

            float angle =
                executionOrbitAngle * Mathf.Deg2Rad;

            Vector3 orbitDirection =
                forward * Mathf.Cos(angle) +
                right * Mathf.Sin(angle);

            target = GetPlayerGroundPosition();

            if (currentView == CameraView.Following)
                target += forward * lookAhead;

            target += Vector3.up * executionLookHeight;

            // Altura da camera interpolada.
            float cameraHeight = Mathf.Lerp(
                executionHeight,
                upperFloorHeight,
                upperFloorBlend);

            // Para a camera superior, calculamos
            // a distancia horizontal pelo angulo.
            float upperPitchRadians =
                upperFloorPitch * Mathf.Deg2Rad;

            float upperHorizontalDistance =
                upperFloorHeight /
                Mathf.Tan(upperPitchRadians);

            float horizontalDistance = Mathf.Lerp(
                executionDistance,
                upperHorizontalDistance,
                upperFloorBlend);

            desiredPosition =
                target +
                orbitDirection * horizontalDistance +
                Vector3.up * cameraHeight;

            desiredRotation = Quaternion.LookRotation(
                target - desiredPosition,
                Vector3.up);

            desiredSize = Mathf.Lerp(
                executionSize,
                upperFloorSize,
                upperFloorBlend);
        }

        if (instant)
        {
            transform.SetPositionAndRotation(
                desiredPosition,
                desiredRotation);

            mainCamera.orthographicSize = desiredSize;
            return;
        }

        // Transicao suave de posicao.
        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref positionVelocity,
            positionSmoothTime);

        // Transicao suave de rotacao.
        float rotationFactor =
            1f - Mathf.Exp(
                -rotationSpeed * Time.deltaTime);

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            desiredRotation,
            rotationFactor);

        // Transicao suave de zoom.
        mainCamera.orthographicSize =
            Mathf.SmoothDamp(
                mainCamera.orthographicSize,
                desiredSize,
                ref zoomVelocity,
                zoomSmoothTime);
    }

    private void OnDisable()
    {
        if (runManager != null)
            runManager.PhaseChanged -= OnPhaseChanged;

        if (mainCamera != null)
            mainCamera.cullingMask = originalCullingMask;

        if (playerSpriteVisual != null)
            playerSpriteVisual.SetActive(
                originalSpriteActive);

        if (mapMarker != null)
            mapMarker.SetActive(
                originalMarkerActive);
    }
}