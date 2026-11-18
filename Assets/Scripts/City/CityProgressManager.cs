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

    // Mantemos a chave anterior para preservar os saves.
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

        ValidateIds();

        if (saveBetweenSessions)
            LoadProgress();

        UpdateAllProblems();
    }

    private void OnEnable()
    {
        if (runManager == null)
            return;

        runManager.CheckpointReached += CommitPendingPhotos;
        runManager.StageFailed += DiscardPendingPhotos;
    }

    private void OnDisable()
    {
        if (runManager == null)
            return;

        runManager.CheckpointReached -= CommitPendingPhotos;
        runManager.StageFailed -= DiscardPendingPhotos;
    }

    private void ValidateIds()
    {
        HashSet<string> knownIds = new HashSet<string>();

        foreach (CityProblem problem in problems)
        {
            if (problem == null)
                continue;

            if (string.IsNullOrWhiteSpace(problem.Id))
            {
                Debug.LogError(
                    "[ROport] Problema sem ID: " + problem.name,
                    problem);
            }
            else if (!knownIds.Add(problem.Id))
            {
                Debug.LogError(
                    "[ROport] ID duplicado: " + problem.Id,
                    problem);
            }
        }
    }

    public bool RegisterPhoto(CityProblem problem)
    {
        if (problem == null ||
            runManager == null ||
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

        Debug.Log(
            "[ROport] Fotografia pendente: " +
            problem.DisplayName);

        return true;
    }

    private void CommitPendingPhotos()
    {
        if (pendingIds.Count == 0)
            return;

        foreach (string id in pendingIds)
            resolvedIds.Add(id);

        pendingIds.Clear();

        UpdateAllProblems();

        if (saveBetweenSessions)
            SaveProgress();

        Debug.Log(
            "[ROport] Checkpoint concluido! " +
            "Melhorias aplicadas.");
    }

    private void DiscardPendingPhotos()
    {
        if (pendingIds.Count == 0)
            return;

        pendingIds.Clear();

        // Retorna os problemas fotografados
        // nesta tentativa ao estado Active.
        // Os ja resolvidos continuam resolvidos.
        UpdateAllProblems();

        Debug.Log(
            "[ROport] Fotografias pendentes perdidas.");
    }

    private void UpdateAllProblems()
    {
        if (problems == null)
            return;

        foreach (CityProblem problem in problems)
        {
            if (problem == null)
                continue;

            problem.SetResolved(
                resolvedIds.Contains(problem.Id));
        }
    }

    private void SaveProgress()
    {
        CitySaveData data = new CitySaveData();

        data.resolvedIds.AddRange(resolvedIds);

        PlayerPrefs.SetString(
            SaveKey,
            JsonUtility.ToJson(data));

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

            if (data == null || data.resolvedIds == null)
                return;

            foreach (string id in data.resolvedIds)
            {
                if (!string.IsNullOrWhiteSpace(id))
                    resolvedIds.Add(id);
            }
        }
        catch (Exception exception)
        {
            Debug.LogError(
                "[ROport] Erro ao carregar cidade: " +
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
        {
            problems =
                GetComponentsInChildren<CityProblem>(true);
        }

        UpdateAllProblems();

        Debug.Log("[ROport] Cidade resetada.");
    }
}