using System;
using TMPro;
using UnityEngine;

public class TrickScoreManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ROportRunManager runManager;
    [SerializeField] private CityProgressManager cityProgressManager;

    [Header("UI")]
    [SerializeField] private TMP_Text scoreText;

    [Header("Photo Points")]
    [SerializeField, Min(0)]
    private int photoPoints = 50;

    public int CurrentScore { get; private set; }

    // Observer Pattern
    public event Action<int> ScoreChanged;

    private void Awake()
    {
        UpdateUI();
    }

    private void OnEnable()
    {
        if (runManager != null)
            runManager.PhaseChanged += OnPhaseChanged;

        if (cityProgressManager != null)
            cityProgressManager.PhotoRecorded += OnPhotoRecorded;
    }

    private void OnDisable()
    {
        if (runManager != null)
            runManager.PhaseChanged -= OnPhaseChanged;

        if (cityProgressManager != null)
            cityProgressManager.PhotoRecorded -= OnPhotoRecorded;
    }

    private void OnPhotoRecorded(CityProblem problem)
    {
        AddPoints(photoPoints);

        Debug.Log(
            "[ROport] Foto de " + problem.DisplayName +
            ": +" + photoPoints + " pontos");
    }

    public bool RegisterTrick(int points)
    {
        if (runManager == null ||
            runManager.Phase != RunPhase.Executing ||
            points <= 0)
        {
            return false;
        }

        AddPoints(points);

        Debug.Log(
            "[ROport] Manobra realizada: +" +
            points + " pontos");

        return true;
    }

    private void AddPoints(int amount)
    {
        CurrentScore += amount;

        UpdateUI();

        ScoreChanged?.Invoke(CurrentScore);
    }

    private void OnPhaseChanged(RunPhase phase)
    {
        if (phase == RunPhase.Planning)
        {
            CurrentScore = 0;

            UpdateUI();

            ScoreChanged?.Invoke(CurrentScore);
        }
    }

    private void UpdateUI()
    {
        if (scoreText != null)
            scoreText.text = "PONTOS: " + CurrentScore;
    }
}