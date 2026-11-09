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

    [Header("QTE")]
    [SerializeField, Min(0.5f)] private float interactionsRadius = 5f;

    public string Id => problemId;
    public string DisplayName => display;

    public CityProblemState State { get; private set; } = CityProblemState.Active;

    public bool CanPhotograph => isActiveAndEnabled && State == CityProblemState.Active;

    private void Awake()
    {
        UpdateVisuals();
    }
    
    public bool IsPlayerNear(Vector3 playerPosition)
    {
        Vector3 difference = playerPosition - transform.position;
        difference.y = 0f;

        return difference.sqrMagnitude <= interactionsRadius * interactionsRadius;
    }

    public bool TryPhotograph()
    {
        if(!CanPhotograph)
            return false;

        State = CityProblemState.Photographed;

        Debug.Log("[ROport] Fotografado: " + DisplayName);

        return true;
    }

    public void SetResolved(bool resolved)
    {
        State = resolved
            ? CityProblemState.Resolved
            : CityProblemState.Active;
        UpdateVisuals();
    }

    private void UpdateVisuals()
    {
        bool resolved = State == CityProblemState.Resolved;
        if (problemVisual != null)
            problemVisual.SetActive(!resolved);

        if(resolvedVisual != null)
            resolvedVisual.SetActive(resolved);
    }
}
