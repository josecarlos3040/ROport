
using UnityEngine;

public class PhonePlanningUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private ROportRunManager runManager;

    [SerializeField]
    private GameObject phoneRoot;

    [SerializeField]
    private Camera phoneMapCamera;

    private void OnEnable()
    {
        if (runManager != null)
        {
            runManager.PhaseChanged += OnPhaseChanged;
        }
    }

    private void Start()
    {
        if (runManager != null)
            OnPhaseChanged(runManager.Phase);
    }

    private void OnDisable()
    {
        if (runManager != null)
        {
            runManager.PhaseChanged -= OnPhaseChanged;
        }
    }

    private void OnPhaseChanged(RunPhase phase)
    {
        bool isPlanning =
            phase == RunPhase.Planning;

        // Mostra o celular somente no planejamento.
        if (phoneRoot != null)
            phoneRoot.SetActive(isPlanning);

        // Economiza renderizacao durante a execucao.
        if (phoneMapCamera != null)
            phoneMapCamera.enabled = isPlanning;
    }
}