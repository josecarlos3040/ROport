using System.Collections.Generic;
using UnityEngine;

public class DitherOccluder : MonoBehaviour
{
    private const string VisibilityProperty = "_DitherVisibility";

    private static readonly int VisibilityId =
        Shader.PropertyToID(VisibilityProperty);

    private struct MaterialSlot
    {
        public Renderer Renderer;
        public int MaterialIndex;

        public MaterialSlot(Renderer renderer, int index)
        {
            Renderer = renderer;
            MaterialIndex = index;
        }
    }

    private readonly List<MaterialSlot> materialSlots =
        new List<MaterialSlot>();

    private MaterialPropertyBlock propertyBlock;

    private float currentVisibility = 1f;
    private float targetVisibility = 1f;

    private void Awake()
    {
        InitializeRenderers();
    }

    private void InitializeRenderers()
    {
        propertyBlock = new MaterialPropertyBlock();
        materialSlots.Clear();

        Renderer[] renderers =
            GetComponentsInChildren<Renderer>(true);

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null ||
                renderer is SpriteRenderer ||
                renderer is ParticleSystemRenderer)
            {
                continue;
            }

            Material[] materials = renderer.sharedMaterials;

            for (int i = 0; i < materials.Length; i++)
            {
                Material material = materials[i];

                if (material == null ||
                    !material.HasProperty(VisibilityId))
                {
                    continue;
                }

                materialSlots.Add(new MaterialSlot(renderer, i));
            }
        }

        if (materialSlots.Count == 0)
        {
            Debug.LogWarning(
                "[ROport Dither] Nenhum material compativel em: " +
                name + ". Confira o Shader Graph.",
                this);
        }
    }

    public void SetOccluded(
        bool occluded,
        float minimumVisibility)
    {
        targetVisibility = occluded
            ? Mathf.Clamp01(minimumVisibility)
            : 1f;
    }

    public void UpdateFade(float speed, float deltaTime)
    {
        if (!isActiveAndEnabled)
            return;

        if (Mathf.Abs(
            currentVisibility - targetVisibility) < 0.001f)
        {
            return;
        }

        currentVisibility = Mathf.MoveTowards(
            currentVisibility,
            targetVisibility,
            Mathf.Max(0.01f, speed) * deltaTime);

        ApplyVisibility();
    }

    public void RestoreImmediate()
    {
        targetVisibility = 1f;
        currentVisibility = 1f;

        ApplyVisibility();
    }

    private void ApplyVisibility()
    {
        if (propertyBlock == null)
            return;

        foreach (MaterialSlot slot in materialSlots)
        {
            Renderer renderer = slot.Renderer;

            if (renderer == null)
                continue;

            // Preserva outras propriedades que
            // eventualmente utilizam PropertyBlock.
            propertyBlock.Clear();

            renderer.GetPropertyBlock(
                propertyBlock,
                slot.MaterialIndex);

            propertyBlock.SetFloat(
                VisibilityId,
                currentVisibility);

            renderer.SetPropertyBlock(
                propertyBlock,
                slot.MaterialIndex);
        }
    }

    private void OnDisable()
    {
        RestoreImmediate();
    }
}