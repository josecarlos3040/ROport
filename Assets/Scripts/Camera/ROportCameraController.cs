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
    //[SerializeField] private float overviewPitch = 90f;
    [SerializeField] private float overviewDistance = 60f;

    [Header("Drawing Camera")]
    [SerializeField] private float drawingSize = 16f;

    // Inclinacao: 90 = totalmente de cima.
    [SerializeField, Range(0f, 90f)]
    private float drawingPitch = 65f;

    // Rotacao horizontal em volta da cidade.
    [SerializeField, Range(-180f, 180f)]
    private float drawingYaw = 30f;

    // Inclinacao lateral da imagem.
    [SerializeField, Range(-30f, 30f)]
    private float drawingRoll = 0f;

    [SerializeField] private float drawingDistance = 26f;

    [Header("Execution Camera")]
    [SerializeField] private float executionSize = 9f;

    // 0 = frente do personagem
    // 90 = lado direito
    // -90 = lado esquerdo
    // 180 = atras do personagem
    [SerializeField, Range(-180f, 180f)]
    private float executionOrbitAngle = 80f;

    [SerializeField, Min(1f)]
    private float executionDistance = 12f;

    [SerializeField]
    private float executionHeight = 7f;

    [SerializeField]
    private float executionLookHeight = 1f;

    [SerializeField]
    private float lookAhead = 1.5f;

    [Header("Player")]
    [SerializeField]
    private float playerPivotHeight = 1f;

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

        if (routeDrawer != null)
            routeDrawer.RouteChanged += OnRouteChanged;
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
        else if (routeDrawer != null &&
                 routeDrawer.Points.Count >= 2)
        {
            ChangeView(CameraView.RouteFocus);
        }
        else
        {
            ChangeView(CameraView.Overview);
        }

        UpdateCamera(true);
    }

    private void LateUpdate()
    {
        // Evita mover a camera durante o desenho.
        bool isDrawing =
            runManager != null &&
            runManager.Phase == RunPhase.Planning &&
            Mouse.current != null &&
            Mouse.current.leftButton.isPressed;

        if (isDrawing)
            return;

        UpdateCamera(false);
    }

    private void OnRouteChanged(int pointCount)
    {
        if (runManager == null ||
            runManager.Phase != RunPhase.Planning)
            return;

        if (pointCount >= 2)
            ChangeView(CameraView.RouteFocus);
        else
            ChangeView(CameraView.Overview);
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

        UpdateVisibility();
    }

    private void UpdateVisibility()
    {
        bool overview = currentView == CameraView.Overview;

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

        // Ignoramos a inclinacao vertical para
        // calcular a orbita da camera.
        forward.y = 0f;

        if (forward.sqrMagnitude < 0.001f)
            return Vector3.forward;

        return forward.normalized;
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
            // Visao geral de toda a cidade.
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
            // Visao inclinada para continuar desenhando.
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
            // CAMERA DINAMICA DURANTE A EXECUCAO.

            Vector3 forward = GetPlayerForward();

            // Direita do personagem no plano horizontal.
            Vector3 right = Vector3.Cross(
                Vector3.up, forward).normalized;

            float angle =
                executionOrbitAngle * Mathf.Deg2Rad;

            // Define de qual lado o personagem
            // sera observado.
            Vector3 orbitDirection =
                forward * Mathf.Cos(angle) +
                right * Mathf.Sin(angle);

            target = GetPlayerGroundPosition();

            // Olha um pouco para frente do skate.
            if (currentView == CameraView.Following)
                target += forward * lookAhead;

            target += Vector3.up * executionLookHeight;

            desiredPosition =
                target +
                orbitDirection * executionDistance +
                Vector3.up * executionHeight;

            // A camera sempre olha para o personagem.
            desiredRotation = Quaternion.LookRotation(
                target - desiredPosition,
                Vector3.up);

            desiredSize = executionSize;
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

        // Zoom suave.
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

        if (routeDrawer != null)
            routeDrawer.RouteChanged -= OnRouteChanged;

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