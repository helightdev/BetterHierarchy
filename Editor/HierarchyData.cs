using System.Linq;
using UnityEditor;
using UnityEngine;

[CreateAssetMenu(fileName = "HierarchyData", menuName = "Hierarchy", order = 0)]
public class HierarchyData : ScriptableObject {
    
    [Header("Cache Settings")]
    public float cacheReloadInterval = 5f;
    public bool invalidateCacheOnReload = false;

    [Header("Colors")]
    public Color headerBackground = new Color(0.33f, 0.33f, 0.33f);
    public Color headerForeground = Color.white;
    public Color[] palette = BetterHierarchy.DefaultPalette.ToArray();
    
    [Header("Palette Settings")]
    public bool firstIsBase = true;
    public bool treeColoring = true;
    public bool alternateColoring = false;
    
    public RuleData[] rules;
    
    
    public ITreeColorSampler GetTreeColorSampler() {
        if (treeColoring) {
            var sampler = new PaletteTreeColorSampler();
            sampler.palette = palette.ToList();
            sampler.alternate = alternateColoring;
            sampler.useBaseColor = firstIsBase;
            return sampler;
        } else {
            return new MonotoneTreeColorSampler {
                color = firstIsBase ? palette[0] : Color.white
            };
        }
    }
}