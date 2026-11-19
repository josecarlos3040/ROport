
using UnityEngine;

public class PhonePlanningUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ROportRunManager runManager;
    [SerializeField] private GameObject phoneRoot;
    [SerializeField] private Camera phoneMapCamera;

    private void OnEnable()
    {
        if (runManager != null)
            runManager.PhaseChanged += OnPhaseChanged;
    }

    private void Start()
    {
        if (runManager != null)
            OnPhaseChanged(runManager.Phase);
    }

    private void OnDisable()
    {
        if (runManager != null)
            runManager.PhaseChanged -= OnPhaseChanged;
    }

    private void OnPhaseChanged(RunPhase phase)
    {
        bool planning = phase == RunPhase.Planning;

        // Somente o painel do celular.
        if (phoneRoot != null)
            phoneRoot.SetActive(planning);

        // Camera que gera o minimapa do celular.
        if (phoneMapCamera != null)
            phoneMapCamera.enabled = planning;
    }
}