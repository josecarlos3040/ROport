using TMPro;
using UnityEngine;

public class CityCheckpointManager : MonoBehaviour
{
    [Header("Checkpoints")]
    [SerializeField] private Transform[] checkpoints = new Transform[5];

    [Header("Arrival")]
    [SerializeField, Min(0.1f)]
    private float arrivalRadius = 1.25f;

    [SerializeField, Min(0.1f)]
    private float verticalTolerance = 1.5f;

    [SerializeField, Min(0f)]
    private float playerPivotHeight = 1f;

    [Header("UI")]
    [SerializeField] private TMP_Text checkpointText;

    private int currentIndex;

    public int CurrentIndex => currentIndex;
    public int TotalCheckpoints =>
        checkpoints != null ? checkpoints.Length : 0;

    public bool IsComplete =>
        TotalCheckpoints > 0 &&
        currentIndex >= TotalCheckpoints;

    public Transform CurrentCheckpoint
    {
        get
        {
            if (checkpoints == null ||
                currentIndex >= checkpoints.Length)
                return null;

            return checkpoints[currentIndex];
        }
    }

    private void Awake()
    {
        if (TotalCheckpoints == 0)
        {
            Debug.LogError(
                "[ROport] Nenhum checkpoint configurado.", this);
        }

        RefreshPresentation();
    }

    // O checkpoint deve ficar na superficie da rua.
    public bool IsAtCurrentCheckpoint(Vector3 playerPosition)
    {
        Transform target = CurrentCheckpoint;

        if (target == null)
            return false;

        Vector3 feetPosition =
            playerPosition - Vector3.up * playerPivotHeight;

        Vector3 difference =
            feetPosition - target.position;

        float horizontalDistance = new Vector2(
            difference.x, difference.z).magnitude;

        return horizontalDistance <= arrivalRadius &&
               Mathf.Abs(difference.y) <= verticalTolerance;
    }

    // Verifica onde o jogador pretende terminar a rota.
    public bool RouteEndsAtCheckpoint(Vector3 routeEnd)
    {
        return IsAtCurrentCheckpoint(routeEnd);
    }

    public void AdvanceCheckpoint()
    {
        if (IsComplete || CurrentCheckpoint == null)
            return;

        currentIndex++;

        RefreshPresentation();
    }

    private void RefreshPresentation()
    {
        if (checkpoints != null)
        {
            for (int i = 0; i < checkpoints.Length; i++)
            {
                if (checkpoints[i] != null)
                {
                    checkpoints[i].gameObject.SetActive(
                        i == currentIndex);
                }
            }
        }

        if (checkpointText == null)
            return;

        checkpointText.text = IsComplete
            ? "TODOS OS DESTINOS CONCLUIDOS!"
            : $"DESTINO {currentIndex + 1}/{TotalCheckpoints}";
    }

    private void OnDrawGizmosSelected()
    {
        if (checkpoints == null)
            return;

        Gizmos.color = Color.yellow;

        foreach (Transform checkpoint in checkpoints)
        {
            if (checkpoint == null)
                continue;

            Gizmos.DrawWireSphere(
                checkpoint.position,
                arrivalRadius);
        }
    }
}