using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Camera))]
public class ROportCameraController : MonoBehaviour
{
    private enum CameraView
    {
        Planning,
        Following,
        Finished
    }

    [Header("References")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private Transform player;
    [SerializeField] private ROportRunManager runManager;
    [SerializeField] private RouteFollower routeFollower;
    [SerializeField] private RobotAnimationController robotAnimation;

    [Header("Visibility")]
    [SerializeField] private LayerMask problemVisualLayers;
    [SerializeField] private GameObject playerSpriteVisual;
    [SerializeField] private GameObject mapMarker;

    [Header("Execution Camera - First Floor")]
    [SerializeField, Min(0.1f)]
    private float executionSize = 9f;

    [SerializeField, Range(-180f, 180f)]
    private float executionOrbitAngle = 80f;

    [SerializeField, Min(1f)]
    private float executionDistance = 12f;

    [SerializeField] private float executionHeight = 7f;
    [SerializeField] private float executionLookHeight = 1f;
    [SerializeField] private float lookAhead = 1.5f;

    [Header("Second Floor Camera")]
    [SerializeField] private bool enableSecondFloorView = true;

    [SerializeField]
    private float secondFloorEnterHeight = 8.5f;

    [SerializeField]
    private float secondFloorExitHeight = 7f;

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

    [Header("Phone Planning Camera")]
    [SerializeField]
    private Vector3 phonePlanningRotation =
        new Vector3(12f, 90f, 0f);

    [SerializeField, Min(1f)]
    private float phonePlanningDistance = 6f;

    [SerializeField]
    private float phonePlanningHeight = 1f;

    [SerializeField]
    private float phonePlanningLookSideBias = 3f;

    [SerializeField, Min(0.1f)]
    private float phonePlanningSize = 4.2f;

    [SerializeField]
    private float phonePlanningLookHeight = 1f;

    [Header("Smoothing")]
    [SerializeField, Min(0.01f)]
    private float positionSmoothTime = 0.35f;

    [SerializeField, Min(0.01f)]
    private float zoomSmoothTime = 0.4f;

    [SerializeField, Min(0.1f)]
    private float rotationSpeed = 5f;

    private CameraView currentView = CameraView.Planning;

    private Vector3 positionVelocity;
    private float zoomVelocity;

    private int originalCullingMask;
    private bool originalSpriteActive;
    private bool originalMarkerActive;

    // Ultima direcao real do movimento em execucao.
    private Vector3 lastValidForward = Vector3.forward;

    // Posicao anterior usada para calcular movimento.
    private Vector3 lastExecutionPlayerPosition;
    private bool hasExecutionPlayerPosition;

    // Segundo andar.
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

        if (player == null)
            return;

        if (routeFollower == null)
        {
            routeFollower =
                player.GetComponent<RouteFollower>();
        }

        if (robotAnimation == null)
        {
            robotAnimation =
                player.GetComponent<RobotAnimationController>();
        }

        Vector3 initialForward = player.forward;
        initialForward.y = 0f;

        if (initialForward.sqrMagnitude > 0.0001f)
        {
            lastValidForward = initialForward.normalized;
        }

        lastExecutionPlayerPosition = player.position;
        hasExecutionPlayerPosition = true;
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

            return;
        }

        OnPhaseChanged(runManager.Phase);
        UpdateCamera(true);
    }

    private void LateUpdate()
    {
        if (mainCamera == null || player == null)
            return;

        // Nao mexe na camera enquanto desenha.
        bool isDrawing =
            currentView == CameraView.Planning &&
            Mouse.current != null &&
            Mouse.current.leftButton.isPressed;

        if (isDrawing)
            return;

        // Congela a camera durante a queda.
        if (robotAnimation != null &&
            robotAnimation.IsFallAnimationActive)
        {
            positionVelocity = Vector3.zero;
            zoomVelocity = 0f;

            // Ignora deslocamentos provocados pela queda.
            if (currentView == CameraView.Following)
            {
                lastExecutionPlayerPosition = player.position;
                hasExecutionPlayerPosition = true;
            }

            return;
        }

        UpdateCamera(false);
    }

    private void OnPhaseChanged(RunPhase phase)
    {
        switch (phase)
        {
            case RunPhase.Planning:
                ChangeView(CameraView.Planning);
                break;

            case RunPhase.Executing:
                ChangeView(CameraView.Following);
                break;

            case RunPhase.Finished:
                ChangeView(CameraView.Finished);
                break;
        }
    }

    private void ChangeView(CameraView view)
    {
        CameraView previousView = currentView;

        currentView = view;

        positionVelocity = Vector3.zero;
        zoomVelocity = 0f;

        UpdateVisibility();

        // Voltando do planejamento para a execucao.
        if (view == CameraView.Following &&
            previousView != CameraView.Following &&
            player != null)
        {
            // Ignora movimentos realizados durante
            // o planejamento.
            lastExecutionPlayerPosition = player.position;
            hasExecutionPlayerPosition = true;

            // Retorna diretamente para a camera
            // de execucao usando a ultima direcao
            // real do jogador.
            UpdateCamera(true);
        }
    }

    private void UpdateVisibility()
    {
        bool planning =
            currentView == CameraView.Planning;

        if (mainCamera != null)
        {
            mainCamera.cullingMask = planning
                ? originalCullingMask &
                  ~problemVisualLayers.value
                : originalCullingMask;
        }

        if (playerSpriteVisual != null)
            playerSpriteVisual.SetActive(true);

        if (mapMarker != null)
            mapMarker.SetActive(planning);
    }

    private Vector3 GetPlayerGroundPosition()
    {
        return player.position -
            Vector3.up * playerPivotHeight;
    }

    // ==================================================
    // DIRECAO REAL DA EXECUCAO
    // ==================================================

    private Vector3 GetPlayerForward()
    {
        if (player == null)
            return lastValidForward;

        // Durante planejamento ou finalizacao,
        // nao alteramos a direcao de execucao.
        if (currentView != CameraView.Following)
            return lastValidForward;

        // Inicializa a posicao anterior.
        if (!hasExecutionPlayerPosition)
        {
            lastExecutionPlayerPosition = player.position;
            hasExecutionPlayerPosition = true;

            return lastValidForward;
        }

        // Calcula quanto o player realmente andou.
        Vector3 displacement =
            player.position - lastExecutionPlayerPosition;

        displacement.y = 0f;

        lastExecutionPlayerPosition = player.position;

        // So atualiza se estiver realmente andando.
        if (routeFollower != null &&
            routeFollower.IsFollowing &&
            !routeFollower.IsBlocked &&
            displacement.sqrMagnitude > 0.00001f)
        {
            lastValidForward = displacement.normalized;
        }

        return lastValidForward;
    }

    // ==================================================
    // CAMERA DE PLANEJAMENTO
    // ==================================================

    private void GetPhonePlanningPose(
        out Vector3 desiredPosition,
        out Quaternion desiredRotation)
    {
        // Rotacao manual pelo Inspector.
        desiredRotation =
            Quaternion.Euler(phonePlanningRotation);

        Vector3 pivot =
            GetPlayerGroundPosition() +
            Vector3.up * phonePlanningLookHeight;

        Vector3 cameraForward =
            desiredRotation * Vector3.forward;

        Vector3 cameraRight =
            desiredRotation * Vector3.right;

        Vector3 cameraUp =
            desiredRotation * Vector3.up;

        desiredPosition =
            pivot
            - cameraForward * phonePlanningDistance
            + cameraRight * phonePlanningLookSideBias
            + cameraUp * phonePlanningHeight;
    }

    // ==================================================
    // CONTROLE DO SEGUNDO ANDAR
    // ==================================================

    private void UpdateFloorState(bool instant)
    {
        if (!enableSecondFloorView || player == null)
        {
            isOnUpperFloor = false;
            upperFloorBlend = 0f;
            return;
        }

        float groundHeight =
            GetPlayerGroundPosition().y;

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

        float targetBlend =
            isOnUpperFloor ? 1f : 0f;

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

    // ==================================================
    // CAMERA DE EXECUCAO
    // ==================================================

    private void GetExecutionPose(
        bool instant,
        out Vector3 desiredPosition,
        out Quaternion desiredRotation,
        out float desiredSize)
    {
        UpdateFloorState(instant);

        // Usa a direcao REAL do movimento.
        // Nao usa a rotacao da camera de planejamento.
        Vector3 forward = GetPlayerForward();

        forward.y = 0f;

        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.forward;

        forward.Normalize();

        Vector3 right =
            Vector3.Cross(
                Vector3.up,
                forward).normalized;

        float angle =
            executionOrbitAngle * Mathf.Deg2Rad;

        // A camera gira quando o robo muda
        // a direcao do movimento.
        Vector3 orbitDirection =
            forward * Mathf.Cos(angle) +
            right * Mathf.Sin(angle);

        orbitDirection.Normalize();

        Vector3 target = GetPlayerGroundPosition();

        if (currentView == CameraView.Following)
            target += forward * lookAhead;

        target += Vector3.up * executionLookHeight;

        float cameraHeight = Mathf.Lerp(
            executionHeight,
            upperFloorHeight,
            upperFloorBlend);

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

    // ==================================================
    // MOVIMENTACAO DA CAMERA
    // ==================================================

    private void UpdateCamera(bool instant)
    {
        if (mainCamera == null || player == null)
            return;

        Vector3 desiredPosition;
        Quaternion desiredRotation;
        float desiredSize;

        if (currentView == CameraView.Planning)
        {
            GetPhonePlanningPose(
                out desiredPosition,
                out desiredRotation);

            desiredSize = phonePlanningSize;
        }
        else
        {
            GetExecutionPose(
                instant,
                out desiredPosition,
                out desiredRotation,
                out desiredSize);
        }

        Transform cameraTransform =
            mainCamera.transform;

        // Troca instantanea quando necessario.
        if (instant)
        {
            cameraTransform.SetPositionAndRotation(
                desiredPosition,
                desiredRotation);

            mainCamera.orthographicSize = desiredSize;

            positionVelocity = Vector3.zero;
            zoomVelocity = 0f;

            return;
        }

        // Suaviza o acompanhamento do player.
        cameraTransform.position = Vector3.SmoothDamp(
            cameraTransform.position,
            desiredPosition,
            ref positionVelocity,
            positionSmoothTime);

        // Suaviza o giro durante as curvas.
        float rotationFactor =
            1f - Mathf.Exp(
                -rotationSpeed * Time.deltaTime);

        cameraTransform.rotation = Quaternion.Slerp(
            cameraTransform.rotation,
            desiredRotation,
            rotationFactor);

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
        {
            playerSpriteVisual.SetActive(
                originalSpriteActive);
        }

        if (mapMarker != null)
        {
            mapMarker.SetActive(
                originalMarkerActive);
        }
    }
}