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

    public event Action<CityProblem> PhotoRecorded;

    private void Awake()
    {
        problems = GetComponentsInChildren<CityProblem>(true);

        ValidateProblemIds();

        if (saveBetweenSessions)
            LoadProgress();

        UpdateAllProblems();
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

    private void ValidateProblemIds()
    {
        HashSet<string> ids = new HashSet<string>();

        foreach (CityProblem problem in problems)
        {
            if (problem == null)
                continue;

            if (string.IsNullOrWhiteSpace(problem.Id))
            {
                Debug.LogError(
                    $"[ROport] Problema sem ID: {problem.name}",
                    problem);
            }
            else if (!ids.Add(problem.Id))
            {
                Debug.LogError(
                    $"[ROport] ID duplicado: {problem.Id}",
                    problem);
            }
        }
    }

    public bool RegisterPhoto(CityProblem problem)
    {
        if (problem == null || runManager == null ||
            runManager.Phase != RunPhase.Executing)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(problem.Id) ||
            Array.IndexOf(problems, problem) < 0)
        {
            return false;
        }

        if (!problem.TryPhotograph())
            return false;

        pendingIds.Add(problem.Id);

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

        UpdateAllProblems();

        if (saveBetweenSessions)
            SaveProgress();

        Debug.Log("[ROport] Melhorias da cidade aplicadas.");
    }

    private void UpdateAllProblems()
    {
        if (problems == null)
            return;

        foreach (CityProblem problem in problems)
        {
            if (problem == null)
                continue;

            problem.SetResolved(resolvedIds.Contains(problem.Id));
        }
    }

    private void SaveProgress()
    {
        CitySaveData data = new CitySaveData();

        data.resolvedIds.AddRange(resolvedIds);

        PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(data));
        PlayerPrefs.Save();
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

            if (data?.resolvedIds == null)
                return;

            foreach (string id in data.resolvedIds)
            {
                if (!string.IsNullOrWhiteSpace(id))
                    resolvedIds.Add(id);
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                "[ROport] Falha ao carregar progresso: " +
                exception.Message);
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
            problems = GetComponentsInChildren<CityProblem>(true);

        UpdateAllProblems();

        Debug.Log("[ROport] Cidade resetada.");
    }
}