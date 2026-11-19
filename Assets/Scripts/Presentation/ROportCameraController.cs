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
    [SerializeField] private Transform cityCenter;
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

    [SerializeField, Min(1f)]
    private float executionDistance = 12f;

    [SerializeField]
    private float executionHeight = 7f;

    [SerializeField]
    private float executionLookHeight = 1f;

    [SerializeField]
    private float lookAhead = 1.5f;

    [Header("First Floor - City Center")]
    [SerializeField]
    private bool faceCityCenterOnFirstFloor = true;

    [SerializeField, Min(0.1f)]
    private float cityDirectionRotationSpeed = 5f;

    [SerializeField, Min(0f)]
    private float centerDirectionDeadZone = 2f;

    [Header("Execution Camera - Second Floor")]
    [SerializeField, Range(-180f, 180f)]
    private float executionOrbitAngle = 80f;

    [SerializeField]
    private bool enableSecondFloorView = true;

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
    [SerializeField]
    private float playerPivotHeight = 1f;

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

    private Vector3 lastValidForward = Vector3.forward;

    private Vector3 lastExecutionPlayerPosition;
    private bool hasExecutionPlayerPosition;

    private Vector3 currentCityDirection = Vector3.forward;
    private bool hasCityDirection;

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
            lastValidForward = initialForward.normalized;

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

        if (faceCityCenterOnFirstFloor &&
            cityCenter == null)
        {
            Debug.LogWarning(
                "[ROport Camera] CityCenter nao atribuido. " +
                "Usando direcao do movimento.",
                this);
        }

        OnPhaseChanged(runManager.Phase);
        UpdateCamera(true);
    }

    private void LateUpdate()
    {
        if (mainCamera == null || player == null)
            return;

        bool isDrawing =
            currentView == CameraView.Planning &&
            Mouse.current != null &&
            Mouse.current.leftButton.isPressed;

        if (isDrawing)
            return;

        // Evita que a queda mova a camera.
        if (robotAnimation != null &&
            robotAnimation.IsFallAnimationActive)
        {
            positionVelocity = Vector3.zero;
            zoomVelocity = 0f;

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

        if (view == CameraView.Following &&
            previousView != CameraView.Following &&
            player != null)
        {
            // Ignora deslocamentos durante planejamento.
            lastExecutionPlayerPosition = player.position;
            hasExecutionPlayerPosition = true;

            // Volta diretamente ao enquadramento
            // de execucao, sem girar pela cidade.
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

    // Direcao real do movimento.
    // So muda durante execucao.
    private Vector3 GetPlayerForward()
    {
        if (player == null)
            return lastValidForward;

        if (currentView != CameraView.Following)
            return lastValidForward;

        if (!hasExecutionPlayerPosition)
        {
            lastExecutionPlayerPosition = player.position;
            hasExecutionPlayerPosition = true;
            return lastValidForward;
        }

        Vector3 displacement =
            player.position - lastExecutionPlayerPosition;

        displacement.y = 0f;

        lastExecutionPlayerPosition = player.position;

        if (routeFollower != null &&
            routeFollower.IsFollowing &&
            !routeFollower.IsBlocked &&
            displacement.sqrMagnitude > 0.00001f)
        {
            lastValidForward = displacement.normalized;
        }

        return lastValidForward;
    }

    // Direcao horizontal do Player para CityCenter.
    // Usada somente no primeiro andar.
    private Vector3 GetCityDirection(bool instant)
    {
        if (!faceCityCenterOnFirstFloor ||
            cityCenter == null ||
            player == null)
        {
            return lastValidForward;
        }

        Vector3 direction =
            cityCenter.position - player.position;

        direction.y = 0f;

        // Evita uma virada brusca quando o
        // robo passa muito perto do centro.
        if (direction.sqrMagnitude <
            centerDirectionDeadZone *
            centerDirectionDeadZone)
        {
            return hasCityDirection
                ? currentCityDirection
                : lastValidForward;
        }

        direction.Normalize();

        if (!hasCityDirection || instant)
        {
            currentCityDirection = direction;
            hasCityDirection = true;
        }
        else if (currentView == CameraView.Following)
        {
            currentCityDirection =
                Vector3.RotateTowards(
                    currentCityDirection,
                    direction,
                    cityDirectionRotationSpeed *
                    Mathf.Deg2Rad *
                    Time.deltaTime,
                    0f).normalized;
        }

        return currentCityDirection;
    }

    // Camera do planejamento: independente
    // da direcao do movimento e do CityCenter.
    private void GetPhonePlanningPose(
        out Vector3 desiredPosition,
        out Quaternion desiredRotation)
    {
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
        else if (groundHeight >= secondFloorEnterHeight)
        {
            isOnUpperFloor = true;
        }

        float targetBlend =
            isOnUpperFloor ? 1f : 0f;

        if (instant || floorTransitionDuration <= 0f)
        {
            upperFloorBlend = targetBlend;
        }
        else
        {
            upperFloorBlend = Mathf.MoveTowards(
                upperFloorBlend,
                targetBlend,
                Time.deltaTime /
                floorTransitionDuration);
        }
    }

    private void GetExecutionPose(
        bool instant,
        out Vector3 desiredPosition,
        out Quaternion desiredRotation,
        out float desiredSize)
    {
        UpdateFloorState(instant);

        Vector3 moveForward = GetPlayerForward();
        moveForward.y = 0f;

        if (moveForward.sqrMagnitude < 0.0001f)
            moveForward = Vector3.forward;

        moveForward.Normalize();

        Vector3 target = GetPlayerGroundPosition();
        target += Vector3.up * executionLookHeight;

        // PRIMEIRO ANDAR
        // Camera sempre do lado de fora,
        // olhando na direcao do centro da cidade.
        Vector3 cityForward =
            GetCityDirection(instant);

        Vector3 firstFloorTarget =
            target + cityForward * lookAhead;

        Vector3 firstFloorPosition =
            target
            - cityForward * executionDistance
            + Vector3.up * executionHeight;

        // SEGUNDO ANDAR
        // Camera mais top-down e dinamica.
        Vector3 right =
            Vector3.Cross(
                Vector3.up,
                moveForward).normalized;

        float angle =
            executionOrbitAngle * Mathf.Deg2Rad;

        Vector3 upperOrbitDirection =
            moveForward * Mathf.Cos(angle) +
            right * Mathf.Sin(angle);

        upperOrbitDirection.Normalize();

        float upperPitchRadians =
            upperFloorPitch * Mathf.Deg2Rad;

        float upperHorizontalDistance =
            upperFloorHeight /
            Mathf.Tan(upperPitchRadians);

        Vector3 upperTarget =
            target + moveForward * lookAhead;

        Vector3 upperPosition =
            upperTarget
            + upperOrbitDirection *
              upperHorizontalDistance
            + Vector3.up * upperFloorHeight;

        // Transicao suave entre as duas cameras.
        desiredPosition = Vector3.Lerp(
            firstFloorPosition,
            upperPosition,
            upperFloorBlend);

        Vector3 lookTarget = Vector3.Lerp(
            firstFloorTarget,
            upperTarget,
            upperFloorBlend);

        desiredRotation = Quaternion.LookRotation(
            lookTarget - desiredPosition,
            Vector3.up);

        desiredSize = Mathf.Lerp(
            executionSize,
            upperFloorSize,
            upperFloorBlend);
    }

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

        cameraTransform.position = Vector3.SmoothDamp(
            cameraTransform.position,
            desiredPosition,
            ref positionVelocity,
            positionSmoothTime);

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