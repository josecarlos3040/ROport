using UnityEngine;

public enum CityProblemState
{
    Active,
    Photographed,
    Resolved
}

public class CityProblem : MonoBehaviour
{
    [Header("Problem Info")]
    [SerializeField] private string problemId = "buraco_01";
    [SerializeField] private string display = "Buraco na rua";

    [Header("Visuals")]
    [SerializeField] private GameObject problemVisual;
    [SerializeField] private GameObject resolvedVisual;

    [Header("Route Blocking")]
    [SerializeField] private bool blocksRouteWhenActive = false;
    [SerializeField] private GameObject routeBlocker;

    [Header("Interaction Area")]
    [SerializeField, Min(0.5f)]
    private float interactionsRadius = 5f;

    [SerializeField, Min(0.1f)]
    private float verticalTolerance = 1.25f;

    [SerializeField, Min(0f)]
    private float playerPivotHeight = 1f;

    public string Id => problemId;
    public string DisplayName => display;
    public float InteractionRadius => interactionsRadius;

    public CityProblemState State { get; private set; }
        = CityProblemState.Active;

    public bool CanPhotograph =>
        isActiveAndEnabled &&
        State == CityProblemState.Active;

    private void Awake()
    {
        UpdateVisuals();
    }

    public bool IsPlayerNear(Vector3 playerPosition)
    {
        Vector3 playerFeet =
            playerPosition - Vector3.up * playerPivotHeight;

        Vector3 difference =
            playerFeet - transform.position;

        if (Mathf.Abs(difference.y) > verticalTolerance)
            return false;

        float horizontalDistanceSquared =
            difference.x * difference.x +
            difference.z * difference.z;

        return horizontalDistanceSquared <=
               interactionsRadius * interactionsRadius;
    }

    public bool TryPhotograph()
    {
        if (!CanPhotograph)
            return false;

        State = CityProblemState.Photographed;

        // A foto nao resolve o problema imediatamente.
        // O bloqueio continua ativo ate a proxima run.
        UpdateVisuals();

        Debug.Log("[ROport] Fotografado: " + DisplayName);

        return true;
    }

    public void SetResolved(bool resolved)
    {
        State = resolved
            ? CityProblemState.Resolved
            : CityProblemState.Active;

        UpdateVisuals();

        Debug.Log(
            "[ROport] " + DisplayName +
            (resolved ? " resolvido!" : " ativo."));
    }

    private void UpdateVisuals()
    {
        bool resolved = State == CityProblemState.Resolved;

        // Mostra o problema enquanto nao foi resolvido.
        if (problemVisual != null)
            problemVisual.SetActive(!resolved);

        // Mostra a versao consertada.
        if (resolvedVisual != null)
            resolvedVisual.SetActive(resolved);

        // NOVO:
        // Bloqueia a passagem enquanto
        // o problema nao estiver resolvido.
        if (routeBlocker != null)
        {
            bool shouldBlock =
                blocksRouteWhenActive && !resolved;

            routeBlocker.SetActive(shouldBlock);
        }
    }
}