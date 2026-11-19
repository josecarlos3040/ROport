using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class RouteDrawer : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera topDownCamera;
    [SerializeField] private Transform player;
    [SerializeField] private LineRenderer routeLine;
    [SerializeField] private RoadNetwork roadNetwork;
    [SerializeField] private PhoneMapInput phoneMapInput;
    [SerializeField] private ROportRunManager runManager;

    [Header("Layers")]
    [SerializeField] private LayerMask roadMask;

    [Header("Drawing")]
    [SerializeField, Min(0.1f)]
    private float startRadius = 1.75f;

    [SerializeField, Min(0.1f)]
    private float startingNodeRadius = 1.5f;

    [SerializeField, Min(0.1f)]
    private float snapRadius = 3.4f;

    [SerializeField, Min(2)]
    private int maxNodes = 300;

    [SerializeField]
    private float lineHeight = 0.2f;

    [Header("Phone Map Drawing")]
    [SerializeField]
    private bool easyStartOnPhone = true;

    [Header("Player")]
    [SerializeField, Min(0f)]
    private float playerPivotHeight = 1f;

    [Header("Checkpoints - In Order")]
    [SerializeField]
    private Transform[] checkpoints;

    [SerializeField, Min(0)]
    private int initialCheckpointIndex = 0;

    [Header("Checkpoint Auto Complete")]
    [SerializeField, Min(0.1f)]
    private float checkpointSnapRadius = 10f;

    [SerializeField, Min(0.1f)]
    private float checkpointNodeSearchRadius = 5f;

    [SerializeField, Min(1)]
    private int maxAutoCompleteNodes = 8;

    [SerializeField, Min(0f)]
    private float maxFinalConnection = 3f;

    [SerializeField, Min(0f)]
    private float checkpointVerticalTolerance = 3f;

    private readonly List<Vector3> points =
        new List<Vector3>();

    private readonly List<int> routeNodes =
        new List<int>();

    private readonly HashSet<int> visitedNodes =
        new HashSet<int>();

    private bool canEdit;
    private bool isDrawing;
    private bool completedToCheckpoint;

    private int currentCheckpointIndex;

    private string lastFeedback;

    public IReadOnlyList<Vector3> Points => points;

    public bool HasRoute => routeNodes.Count >= 2;

    public bool IsConnectedToCheckpoint =>
        completedToCheckpoint;

    public int CurrentCheckpointIndex =>
        currentCheckpointIndex;

    public event Action<int> RouteChanged;
    public event Action<string> Feedback;

    private void Awake()
    {
        if (topDownCamera == null)
            topDownCamera = Camera.main;

        currentCheckpointIndex =
            Mathf.Max(0, initialCheckpointIndex);

        if (routeLine != null)
        {
            routeLine.useWorldSpace = true;
            routeLine.positionCount = 0;
        }
    }

    private void OnEnable()
    {
        if (runManager != null)
        {
            runManager.CheckpointReached +=
                OnCheckpointReached;
        }
    }

    private void OnDisable()
    {
        if (runManager != null)
        {
            runManager.CheckpointReached -=
                OnCheckpointReached;
        }
    }

    private void Update()
    {
        Mouse mouse = Mouse.current;

        if (!canEdit ||
            mouse == null ||
            player == null ||
            roadNetwork == null ||
            (phoneMapInput == null &&
             topDownCamera == null))
        {
            return;
        }

        bool pointerOnMap =
            phoneMapInput == null ||
            phoneMapInput.IsPointerOverMap();

        // Limpa a rota com botao direito.
        if (mouse.rightButton.wasPressedThisFrame &&
            pointerOnMap)
        {
            ClearRoute();
            return;
        }

        // Inicia o desenho.
        if (mouse.leftButton.wasPressedThisFrame)
        {
            bool canStart;

            if (phoneMapInput != null)
            {
                canStart = pointerOnMap;
            }
            else
            {
                canStart =
                    EventSystem.current == null ||
                    !EventSystem.current
                        .IsPointerOverGameObject();
            }

            if (canStart)
                BeginDrawing();
        }

        if (isDrawing &&
            mouse.leftButton.isPressed &&
            pointerOnMap)
        {
            ContinueDrawing();
        }

        if (mouse.leftButton.wasReleasedThisFrame)
            isDrawing = false;
    }

    public void SetEditable(bool editable)
    {
        canEdit = editable;

        if (!editable)
            isDrawing = false;
    }

    // =================================================
    // CHECKPOINTS
    // =================================================

    private void OnCheckpointReached()
    {
        currentCheckpointIndex++;

        completedToCheckpoint = false;

        Debug.Log(
            "[ROport Route] Checkpoint concluido. " +
            "Proximo indice: " +
            currentCheckpointIndex);
    }

    // Permite sincronizar com um gerenciador
    // externo de checkpoints, se necessario.
    public void SetCurrentCheckpointIndex(int index)
    {
        currentCheckpointIndex =
            Mathf.Max(0, index);

        completedToCheckpoint = false;
    }

    // Usar apenas ao iniciar uma nova partida.
    public void ResetCheckpointProgress()
    {
        currentCheckpointIndex =
            Mathf.Max(0, initialCheckpointIndex);

        ClearRoute();
    }

    private Transform GetCurrentCheckpoint()
    {
        if (checkpoints == null ||
            currentCheckpointIndex < 0 ||
            currentCheckpointIndex >=
                checkpoints.Length)
        {
            return null;
        }

        return checkpoints[currentCheckpointIndex];
    }

    // Chamado pelo RunManager ao apertar Espaco.
    // Retorna true apenas se a rota puder
    // ser ligada ao checkpoint atual.
    public bool TryCompleteToCurrentCheckpoint()
    {
        if (!canEdit || roadNetwork == null)
            return false;

        if (completedToCheckpoint)
            return HasRoute;

        Transform checkpoint = GetCurrentCheckpoint();

        if (checkpoint == null)
        {
            SendFeedback(
                "Checkpoint atual nao configurado.");
            return false;
        }

        if (!HasRoute)
        {
            SendFeedback(
                "Desenhe uma rota antes de executar.");
            return false;
        }

        int lastId =
            routeNodes[routeNodes.Count - 1];

        Vector3 lastSurface =
            roadNetwork.GetWorldPosition(lastId);

        Vector3 checkpointPosition =
            checkpoint.position;

        // Distancia horizontal entre a ponta
        // do desenho e o checkpoint.
        Vector2 horizontalDifference = new Vector2(
            lastSurface.x - checkpointPosition.x,
            lastSurface.z - checkpointPosition.z);

        float horizontalDistance =
            horizontalDifference.magnitude;

        float verticalDistance =
            Mathf.Abs(
                lastSurface.y -
                checkpointPosition.y);

        if (horizontalDistance >
            checkpointSnapRadius)
        {
            SendFeedback(
                "Aproxime o final da rota " +
                "do checkpoint.");
            return false;
        }

        if (verticalDistance >
            checkpointVerticalTolerance)
        {
            SendFeedback(
                "O checkpoint esta em outro andar.");
            return false;
        }

        // Localiza o no da rua mais proximo
        // do checkpoint.
        if (!roadNetwork.TryFindNearestNode(
            checkpointPosition,
            checkpointNodeSearchRadius,
            out int destinationNode))
        {
            SendFeedback(
                "Nao encontrei rua perto " +
                "do checkpoint.");
            return false;
        }

        Vector3 destinationSurface =
            roadNetwork.GetWorldPosition(
                destinationNode);

        if (Mathf.Abs(
            destinationSurface.y -
            checkpointPosition.y) >
            checkpointVerticalTolerance)
        {
            SendFeedback(
                "A rua do checkpoint esta " +
                "em outro nivel.");
            return false;
        }

        // Evita uma ultima linha muito longa
        // cortando a calcada ou um predio.
        Vector2 finalDifference = new Vector2(
            destinationSurface.x -
                checkpointPosition.x,
            destinationSurface.z -
                checkpointPosition.z);

        if (finalDifference.magnitude >
            maxFinalConnection)
        {
            SendFeedback(
                "Checkpoint longe da rua. " +
                "Ajuste Max Final Connection.");
            return false;
        }

        // Busca uma conexao usando os vizinhos
        // reais do RoadNetwork.
        if (!TryBuildAutoPath(
            lastId,
            destinationNode,
            out List<int> extraNodes))
        {
            SendFeedback(
                "Nao foi possivel conectar " +
                "sem repetir ou cortar ruas.");
            return false;
        }

        if (routeNodes.Count + extraNodes.Count >
            maxNodes)
        {
            SendFeedback(
                "Rota excedeu o limite de pontos.");
            return false;
        }

        // A partir daqui a conexao foi validada.
        // Agora podemos modificar a rota.
        foreach (int nodeId in extraNodes)
        {
            routeNodes.Add(nodeId);
            visitedNodes.Add(nodeId);

            Vector3 surface =
                roadNetwork.GetWorldPosition(nodeId);

            points.Add(
                surface +
                Vector3.up * playerPivotHeight);
        }

        // Pequena ligacao final ate a posicao
        // horizontal do checkpoint.
        // A altura continua sendo a da rua.
        Vector3 finalPoint = new Vector3(
            checkpointPosition.x,
            destinationSurface.y + playerPivotHeight,
            checkpointPosition.z);

        Vector3 lastPoint =
            points[points.Count - 1];

        if (Vector3.Distance(
            lastPoint, finalPoint) > 0.05f)
        {
            points.Add(finalPoint);
        }

        completedToCheckpoint = true;
        isDrawing = false;

        RefreshLine();
        RouteChanged?.Invoke(routeNodes.Count);

        Debug.Log(
            "[ROport Route] Rota conectada ao " +
            checkpoint.name +
            " | Nos adicionados: " +
            extraNodes.Count);

        return true;
    }

    // Encontra um caminho curto no grafo,
    // sem passar por nos ja utilizados.
    private bool TryBuildAutoPath(
        int startId,
        int destinationId,
        out List<int> result)
    {
        result = new List<int>();

        if (startId == destinationId)
            return true;

        if (visitedNodes.Contains(destinationId))
            return false;

        Queue<int> queue = new Queue<int>();

        Dictionary<int, int> parents =
            new Dictionary<int, int>();

        Dictionary<int, int> depths =
            new Dictionary<int, int>();

        queue.Enqueue(startId);

        parents[startId] = -1;
        depths[startId] = 0;

        bool found = false;

        while (queue.Count > 0)
        {
            int current = queue.Dequeue();

            if (current == destinationId)
            {
                found = true;
                break;
            }

            int depth = depths[current];

            if (depth >= maxAutoCompleteNodes)
                continue;

            foreach (int neighbor in
                     roadNetwork.GetNeighbors(current))
            {
                // Nao visita o mesmo no duas vezes.
                if (parents.ContainsKey(neighbor))
                    continue;

                // Nao volta por uma rua ja desenhada.
                if (visitedNodes.Contains(neighbor))
                    continue;

                parents[neighbor] = current;
                depths[neighbor] = depth + 1;

                queue.Enqueue(neighbor);
            }
        }

        if (!found)
            return false;

        int node = destinationId;

        while (node != startId)
        {
            result.Add(node);
            node = parents[node];
        }

        result.Reverse();

        return result.Count <= maxAutoCompleteNodes;
    }

    // =================================================
    // RESET
    // =================================================

    public void ClearRoute()
    {
        isDrawing = false;
        completedToCheckpoint = false;
        lastFeedback = null;

        points.Clear();
        routeNodes.Clear();
        visitedNodes.Clear();

        if (player == null || roadNetwork == null)
        {
            RefreshLine();
            RouteChanged?.Invoke(0);
            return;
        }

        Vector3 playerFeet =
            player.position -
            Vector3.up * playerPivotHeight;

        if (!roadNetwork.TryFindNearestNode(
            playerFeet,
            startingNodeRadius,
            out int startId))
        {
            Debug.LogError(
                "[ROport] Player longe do grafo. " +
                "Confira Starting Node Radius.",
                this);

            RefreshLine();
            RouteChanged?.Invoke(0);
            return;
        }

        routeNodes.Add(startId);
        visitedNodes.Add(startId);

        // Preserva a posicao real do Player.
        points.Add(player.position);

        Vector3 snappedPosition =
            roadNetwork.GetWorldPosition(startId) +
            Vector3.up * playerPivotHeight;

        if (Vector3.Distance(
            player.position, snappedPosition) > 0.05f)
        {
            points.Add(snappedPosition);
        }

        RefreshLine();
        RouteChanged?.Invoke(routeNodes.Count);
    }

    // =================================================
    // DRAWING
    // =================================================

    private void BeginDrawing()
    {
        if (completedToCheckpoint)
            return;

        if (routeNodes.Count == 0)
        {
            Debug.LogWarning(
                "[ROport Phone] Nenhum no inicial.");

            return;
        }

        if (!TryGetRoadPoint(
            out Vector3 mousePoint))
        {
            Debug.LogWarning(
                "[ROport Phone] Clique sem rua. " +
                "Verifique PhoneMapCamera e Road Mask.");

            return;
        }

        Vector3 lastSurface =
            roadNetwork.GetWorldPosition(
                routeNodes[routeNodes.Count - 1]);

        bool usingPhone =
            phoneMapInput != null &&
            easyStartOnPhone;

        if (!usingPhone)
        {
            if (Vector3.Distance(
                lastSurface, mousePoint) >
                startRadius)
            {
                return;
            }
        }

        isDrawing = true;

        Debug.Log(
            "[ROport Phone] Desenho iniciado!");
    }

    private void ContinueDrawing()
    {
        if (completedToCheckpoint ||
            routeNodes.Count == 0 ||
            !TryGetRoadPoint(
                out Vector3 mousePoint))
        {
            return;
        }

        if (!roadNetwork.TryFindNearestNode(
            mousePoint,
            snapRadius,
            out _))
        {
            return;
        }

        ExtendToward(mousePoint);
    }

    private bool TryGetRoadPoint(
        out Vector3 point)
    {
        point = Vector3.zero;

        Ray ray;

        if (phoneMapInput != null)
        {
            if (!phoneMapInput.TryGetMapRay(
                out ray))
            {
                return false;
            }
        }
        else
        {
            if (Mouse.current == null ||
                topDownCamera == null)
            {
                return false;
            }

            Vector2 mousePosition =
                Mouse.current.position.ReadValue();

            ray = topDownCamera.ScreenPointToRay(
                new Vector3(
                    mousePosition.x,
                    mousePosition.y,
                    0f));
        }

        const float maxDistance = 2000f;

        if (Physics.Raycast(
            ray,
            out RaycastHit hit,
            maxDistance,
            roadMask.value,
            QueryTriggerInteraction.Ignore))
        {
            point = hit.point;
            return true;
        }

        if (Mouse.current != null &&
            Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (Physics.Raycast(
                ray,
                out RaycastHit otherHit,
                maxDistance,
                ~0,
                QueryTriggerInteraction.Ignore))
            {
                Debug.LogWarning(
                    "[ROport Phone] Encontrou: " +
                    otherHit.collider.name +
                    " | Layer: " +
                    LayerMask.LayerToName(
                        otherHit.collider.gameObject.layer));
            }
            else
            {
                Debug.LogWarning(
                    "[ROport Phone] Nenhum Collider " +
                    "encontrado.");
            }
        }

        return false;
    }

    private void ExtendToward(
        Vector3 target)
    {
        bool changed = false;
        bool encounteredVisitedNode = false;

        for (int step = 0; step < maxNodes; step++)
        {
            int lastIndex = routeNodes.Count - 1;
            int lastId = routeNodes[lastIndex];

            int previousId = lastIndex > 0
                ? routeNodes[lastIndex - 1]
                : -1;

            Vector3 currentPosition =
                roadNetwork.GetWorldPosition(lastId);

            float bestDistance =
                (target - currentPosition)
                .sqrMagnitude - 0.001f;

            int bestNeighbor = -1;

            if (previousId >= 0)
            {
                Vector3 previousPosition =
                    roadNetwork.GetWorldPosition(
                        previousId);

                float previousDistance =
                    (target - previousPosition)
                    .sqrMagnitude;

                if (previousDistance < bestDistance)
                {
                    bestNeighbor = previousId;
                    bestDistance = previousDistance;
                }
            }

            foreach (int neighbor in
                     roadNetwork.GetNeighbors(lastId))
            {
                Vector3 neighborPosition =
                    roadNetwork.GetWorldPosition(
                        neighbor);

                float distance =
                    (target - neighborPosition)
                    .sqrMagnitude;

                if (distance >= bestDistance)
                    continue;

                if (neighbor == previousId)
                {
                    bestNeighbor = neighbor;
                    bestDistance = distance;
                    continue;
                }

                if (visitedNodes.Contains(neighbor))
                {
                    encounteredVisitedNode = true;
                    continue;
                }

                if (routeNodes.Count >= maxNodes)
                    continue;

                bestNeighbor = neighbor;
                bestDistance = distance;
            }

            if (bestNeighbor < 0)
                break;

            if (bestNeighbor == previousId)
            {
                int removed = routeNodes[lastIndex];

                routeNodes.RemoveAt(lastIndex);
                visitedNodes.Remove(removed);

                points.RemoveAt(points.Count - 1);
            }
            else
            {
                routeNodes.Add(bestNeighbor);
                visitedNodes.Add(bestNeighbor);

                points.Add(
                    roadNetwork.GetWorldPosition(
                        bestNeighbor) +
                    Vector3.up * playerPivotHeight);
            }

            changed = true;
        }

        if (changed)
        {
            lastFeedback = null;

            RefreshLine();
            RouteChanged?.Invoke(routeNodes.Count);
        }
        else if (encounteredVisitedNode)
        {
            SendFeedback(
                "Trecho ja utilizado. Volte para apagar.");
        }
    }

    // =================================================
    // UI AND LINE RENDERER
    // =================================================

    private void SendFeedback(
        string message)
    {
        if (lastFeedback == message)
            return;

        lastFeedback = message;
        Feedback?.Invoke(message);

        Debug.Log(
            "[ROport Route] " + message);
    }

    private void RefreshLine()
    {
        if (routeLine == null)
            return;

        routeLine.positionCount = points.Count;

        for (int i = 0; i < points.Count; i++)
        {
            Vector3 position =
                points[i] -
                Vector3.up * playerPivotHeight +
                Vector3.up * lineHeight;

            routeLine.SetPosition(i, position);
        }
    }
}