using System;
using UnityEngine;

/// <summary>One lens channel. Shared materials stay intact when the signal changes.</summary>
public class TrafficLightController : MonoBehaviour
{
    [SerializeField] Renderer[] visualRenderers = Array.Empty<Renderer>();
    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    static readonly int LegacyColor = Shader.PropertyToID("_Color");
    static readonly int Emission = Shader.PropertyToID("_EmissionColor");
    struct Slot
    {
        public Renderer renderer;
        public int index;
        public Color colour, emission;
        public MaterialPropertyBlock block;
    }
    Slot[] slots;
    bool initialized;
    public bool IsLit { get; private set; }

    public void BindVisuals(Renderer[] renderers)
    {
        visualRenderers = renderers;
        slots = null;
        initialized = false;
    }

    public void ChangeRed() => SetLit(CompareTag("RedLight"));
    public void ChangeYellow() => SetLit(CompareTag("YellowLight"));
    public void ChangeGreen() => SetLit(CompareTag("GreenLight"));

    public void SetLit(bool lit)
    {
        if (initialized && IsLit == lit) return;
        if (slots == null) CacheSlots();
        IsLit = lit;
        initialized = true;
        foreach (var slot in slots)
        {
            if (slot.renderer == null) continue;
            slot.renderer.GetPropertyBlock(slot.block, slot.index);
            var colour = slot.colour * (lit ? 1f : 0.10f);
            colour.a = 1f;
            slot.block.SetColor(BaseColor, colour);
            slot.block.SetColor(LegacyColor, colour);
            slot.block.SetColor(Emission, lit ? slot.emission : Color.black);
            slot.block.SetFloat("_Paint", 0f); // safety colours remain distinct on the signal lenses
            slot.renderer.SetPropertyBlock(slot.block, slot.index);
        }
    }

    void CacheSlots()
    {
        var renderers = visualRenderers.Length > 0 ? visualRenderers : GetComponents<Renderer>();
        var list = new System.Collections.Generic.List<Slot>();
        foreach (var renderer in renderers)
        {
            if (renderer == null) continue;
            var materials = renderer.sharedMaterials;
            for (var i = 0; i < materials.Length; i++)
            {
                var material = materials[i];
                if (material == null) continue;
                list.Add(new Slot
                {
                    renderer = renderer, index = i, block = new MaterialPropertyBlock(),
                    colour = material.HasProperty(BaseColor) ? material.GetColor(BaseColor) : material.color,
                    emission = material.HasProperty(Emission) ? material.GetColor(Emission) : Color.black
                });
            }
        }
        slots = list.ToArray();
    }
}
