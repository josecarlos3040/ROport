using System;
using System.Collections.Generic;
using UnityEngine;

public class CityProgressManager : MonoBehaviour
{
    [Serializable]
    private class CitySaveData
    {
        public List<string> resolvedIds = new List<string>();
    }

    private const string SaveKey = "ROport_City_QTE_v1";

    [Header("References")]
    [SerializeField] private ROportRunManager runManager;

    [Header("Save")]
    [SerializeField] private bool saveBetweenSessions = true;

    private CityProblem[] problems;

    private readonly HashSet<string> resolvedIds =
        new HashSet<string>();

    private readonly HashSet<string> pendingIds =
        new HashSet<string>();

    public IReadOnlyList<CityProblem> Problems => problems;

    // Observer Pattern: outros sistemas podem escutar as fotos.
    public event Action<CityProblem> PhotoRecorded;

    private void Awake()
    {
        problems = GetComponentsInChildren<CityProblem>(true);

        if (saveBetweenSessions)
            LoadProgress();

        HashSet<string> knownIds = new HashSet<string>();

        foreach (CityProblem problem in problems)
        {
            if (string.IsNullOrWhiteSpace(problem.Id))
            {
                Debug.LogError(
                    "[ROport] Problema sem ID: " + problem.name);
                continue;
            }

            if (!knownIds.Add(problem.Id))
            {
                Debug.LogError(
                    "[ROport] ID duplicado: " + problem.Id);
            }

            problem.SetResolved(
                resolvedIds.Contains(problem.Id));
        }
    }

    private void OnEnable()
    {
        if (runManager != null)
            runManager.PhaseChanged += OnPhaseChanged;
    }

    private void OnDisable()
    {
        if (runManager != null)
            runManager.PhaseChanged -= OnPhaseChanged;
    }

    public bool RegisterPhoto(CityProblem problem)
    {
        if (problem == null || runManager == null)
            return false;

        if (runManager.Phase != RunPhase.Executing)
            return false;

        if (Array.IndexOf(problems, problem) < 0)
            return false;

        if (string.IsNullOrWhiteSpace(problem.Id))
            return false;

        if (!problem.TryPhotograph())
            return false;

        pendingIds.Add(problem.Id);

        // Notifica outros sistemas sobre a foto.
        PhotoRecorded?.Invoke(problem);

        return true;
    }

    private void OnPhaseChanged(RunPhase phase)
    {
        if (phase == RunPhase.Planning)
            ApplyPendingImprovements();
    }

    private void ApplyPendingImprovements()
    {
        if (pendingIds.Count == 0)
            return;

        foreach (string id in pendingIds)
            resolvedIds.Add(id);

        pendingIds.Clear();

        foreach (CityProblem problem in problems)
        {
            problem.SetResolved(
                resolvedIds.Contains(problem.Id));
        }

        if (saveBetweenSessions)
            SaveProgress();

        Debug.Log("[ROport] Cidade atualizada!");
    }

    private void SaveProgress()
    {
        CitySaveData data = new CitySaveData();

        data.resolvedIds.AddRange(resolvedIds);

        PlayerPrefs.SetString(
            SaveKey,
            JsonUtility.ToJson(data));

        PlayerPrefs.Save();

        Debug.Log("[ROport] Progresso salvo!");
    }

    private void LoadProgress()
    {
        string json = PlayerPrefs.GetString(SaveKey, "");

        if (string.IsNullOrEmpty(json))
            return;

        try
        {
            CitySaveData data =
                JsonUtility.FromJson<CitySaveData>(json);

            if (data == null || data.resolvedIds == null)
                return;

            foreach (string id in data.resolvedIds)
            {
                resolvedIds.Add(id);
            }

            Debug.Log("[ROport] Progresso carregado!");
        }
        catch (Exception e)
        {
            Debug.LogError(
                "[ROport] Falha ao carregar progresso: " +
                e.Message);
        }
    }

    [ContextMenu("ROport - Resetar cidade salva")]
    public void ResetCity()
    {
        resolvedIds.Clear();
        pendingIds.Clear();

        PlayerPrefs.DeleteKey(SaveKey);
        PlayerPrefs.Save();

        if (problems == null)
        {
            problems =
                GetComponentsInChildren<CityProblem>(true);
        }

        foreach (CityProblem problem in problems)
        {
            problem.SetResolved(false);
        }

        Debug.Log("[ROport] Cidade resetada!");
    }
}