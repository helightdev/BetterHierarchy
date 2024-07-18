using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Unity.EditorCoroutines.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using ColorUtility = UnityEngine.ColorUtility;
using Scene = UnityEngine.SceneManagement.Scene;

public static class BetterHierarchy {
    
    public const string PackageName = "dev.helight.hierarchy";
    
    public static Dictionary<int, ObjectAnalyzerResult> HierarchyCache = new();
    public static List<Color> DefaultPalette = new();
    public static HierarchyData Configuration;
    public static ITreeColorSampler TreeColorSampler;

    [InitializeOnLoadMethod]
    public static void Bootstrap() {
        DefaultPalette = new List<Color> {
            FromHex("#ACACAC"), FromHex("#ef4444"), FromHex("#f97316"), FromHex("#eab308"),
            FromHex("#22c55e"), FromHex("#14b8a6"), FromHex("#3b82f6"), FromHex("#8b5cf6"),
            FromHex("#ec4899")
        };
        
        if (!AssetDatabase.AssetPathExists("Assets/Editor Default Resources")) {
            Debug.Log("Installing better hierarchy package");
            
            AssetDatabase.CreateFolder("Assets", "Editor Default Resources");
            AssetDatabase.MoveAsset($"Packages/{PackageName}/LucideIcons", "Assets/Editor Default Resources/LucideIcons");
            
            var data = ScriptableObject.CreateInstance<HierarchyData>();
            AssetDatabase.CreateAsset(data, "Assets/Editor Default Resources/HierarchyData.asset");
            
            Debug.Log("Better hierarchy package installed!");
            
        }
        
        EditorApplication.hierarchyChanged += ResetCache;
        EditorApplication.hierarchyWindowItemOnGUI += DrawItem;
        
        EditorCoroutineUtility.StartCoroutineOwnerless(TickCoroutine());

        Configuration = AssetDatabase.LoadAssetAtPath<HierarchyData>(
            "Assets/Editor Default Resources/HierarchyData.asset");
        if (Configuration == null) {
            Debug.LogWarning("Failed to load configuration");
        } else {
            TreeColorSampler = Configuration.GetTreeColorSampler();
        }
    }

    private static int smallestX = int.MaxValue;
    private static DateTime lastTick = DateTime.Now;
    private static Color HeaderShadow = new(0.16f, 0.16f, 0.16f);

    private const int ItemHeight = 16;
    private const float LeftExpand = 15;
    private const float RightExpand = 16;
    private const float RightSoftExpand = 11;
    private const int LineWidth = 14;
    private const int LineOffset = 9;


    private static void DrawItem(int instanceID, Rect selectionRect) {
        try {
            if (Configuration == null) return;

            smallestX = Mathf.Min(smallestX, (int)selectionRect.x);

            if (!HierarchyCache.TryGetValue(instanceID, out var result)) {
                result = Analyze(instanceID);
                HierarchyCache[instanceID] = result;
            }

            if (result == null) {
                return;
            }

            result.gcMark = true;
            var rect = new Rect(selectionRect) { x = smallestX, width = selectionRect.width + selectionRect.x - smallestX };

            var iconStartPosition = new Rect(rect) {
                x = selectionRect.x + selectionRect.width - (15 * result.icons.Count) + RightSoftExpand, width = 15, height = 15
            };


            //var t = "\u2501";
            if (result.parent != -2) {
                // Move to the start
                rect.x -= LeftExpand;
                rect.width += LeftExpand;

                // Expand to the right and draw highlights
                rect.width += RightExpand;
                if (result.hasHighlight) {
                    EditorGUI.DrawRect(rect, result.highlightColor);
                }
                rect.width -= RightExpand;

                foreach (var icon in result.icons) {
                    var previousColor = GUI.color;
                    GUI.color = icon.Item2;
                    GUI.DrawTexture(iconStartPosition, icon.Item1);
                    GUI.color = previousColor;

                    GUI.Label(iconStartPosition, "",
                        new GUIStyle {
                            normal = new GUIStyleState { textColor = Color.white, }, fontSize = ItemHeight,
                        });

                    iconStartPosition.x += 15;
                }

                if (result.name.StartsWith("#")) {
                    var headerRect = new Rect(selectionRect) { y = selectionRect.y, width = selectionRect.width + RightSoftExpand, height = ItemHeight };
                    
                    EditorGUI.DrawRect(headerRect, HeaderShadow);
                    var innerRect = new Rect(headerRect) { x = headerRect.x + 1, y = headerRect.y + 1, width = headerRect.width - 2, height = headerRect.height - 2 };
                    EditorGUI.DrawRect(innerRect, Configuration.headerBackground);
                    
                    var name = result.name[1..].Trim().ToUpper();
                    GUI.Label(headerRect, name, new GUIStyle {
                        normal = new GUIStyleState { textColor = Configuration.headerForeground, }, fontSize = (int)(ItemHeight * 0.8),
                        alignment = TextAnchor.MiddleCenter,
                    });
                }
                
                try {
                    DrawCombinedLine(selectionRect, result);
                } catch (System.Exception _) {
                    // ignored
                }
            }
        } catch (System.Exception e) {
            Debug.LogError(e);
        }
    }

    public static void DrawCombinedLine(Rect selection, ObjectAnalyzerResult result) {
        var firstX = selection.x - (result.depth + 1) * LineWidth - LineOffset;
        var texRect = new Rect(firstX, selection.y, result.treeTexture.width, result.treeTexture.height);
        GUI.DrawTexture(texRect, result.treeTexture);
    }

    public static Texture2D DrawCombinedLineIntoTexture(List<Color> segments, bool terminal, bool isHeader) {
        var width = segments.Count * LineWidth;
        var height = ItemHeight;
        var texture = new Texture2D(width, height);

        // Fill transparent
        for (var x = 0; x < width; x++) {
            for (var y = 0; y < height; y++) {
                texture.SetPixel(x, y, Color.clear);
            }
        }

        const float thickness = 2f;
        for (var i = 0; i < segments.Count; i++) {
            var color = segments[i];
            var child = new Rect(0, 0, width, height) { x = i * LineWidth, width = thickness, height = ItemHeight };
            DrawTextureRect(child, texture, color, width, height);

            if (i == segments.Count - 1 && !isHeader) {
                // Draw horizontal center line
                child.y += ItemHeight / 2f - 1;
                child.height = thickness;
                child.width = terminal ? 18 : 10;
                DrawTextureRect(child, texture, color, width, height);
            }
        }

        texture.Apply();
        return texture;
    }

    private static void DrawTextureRect(Rect rect, Texture2D texture, Color color, int w, int h) {
        for (var x = Mathf.FloorToInt(rect.x); x < rect.x + rect.width; x++) {
            for (var y = Mathf.FloorToInt(rect.y); y < rect.y + rect.height; y++) {
                if (x >= w || y >= h) continue;
                texture.SetPixel(x, y, color);
            }
        }
    }


    private static IEnumerator TickCoroutine() {
        float cacheReloadInterval = 1f;

        while (true) {
            var now = DateTime.Now;
            var diff = now - lastTick;
            if (Mathf.Abs((float)diff.TotalSeconds) > cacheReloadInterval) {
                lastTick = now;
            } else {
                yield return 1f;
                continue;
            }

            if (Configuration == null) {
                try {
                    Configuration =
                        AssetDatabase.LoadAssetAtPath<HierarchyData>(
                            "Assets/Editor Default Resources/HierarchyData.asset");
                    Debug.Log("Loaded configuration");
                } catch (System.Exception _) {
                    // ignored
                }
            } else {
                cacheReloadInterval = Configuration.cacheReloadInterval;

                if (Configuration.invalidateCacheOnReload) {
                    HierarchyCache.Clear();
                } else {
                    var toRemove = new List<int>();
                    foreach (var (id, entry) in HierarchyCache) {
                        if (entry == null) {
                            toRemove.Add(id);
                            continue;
                        }

                        if (!entry.gcMark) {
                            toRemove.Add(id);
                        } else {
                            entry.gcMark = false;
                        }
                    }

                    toRemove.ForEach(id => {
                        HierarchyCache.Remove(id);
                    });
                }
            }
        }
    }

    public static void ResetCache() {
        smallestX = int.MaxValue;
        HierarchyCache.Clear();
        TreeColorSampler = Configuration.GetTreeColorSampler();
        Debug.Log("Cache cleared");
    }

    public static Scene[] GetOpenScenes() {
        var scenes = new Scene[SceneManager.sceneCount];
        for (var i = 0; i < SceneManager.sceneCount; i++) {
            scenes[i] = SceneManager.GetSceneAt(i);
        }

        return scenes;
    }

    public static ObjectAnalyzerResult GetOrAnalyze(int instanceId) {
        if (!HierarchyCache.TryGetValue(instanceId, out var result)) {
            result = Analyze(instanceId);
            HierarchyCache[instanceId] = result;
        }

        return result;
    }

    public static ObjectAnalyzerResult Analyze(int instanceId) {
        try {
            var obj = EditorUtility.InstanceIDToObject(instanceId);
            if (!obj) {
                var scenes = GetOpenScenes();
                foreach (var scene in scenes) {
                    if (scene.handle == instanceId) {
                        return null;
                    }
                }

                return null;
            }

            var result = new ObjectAnalyzerResult { parent = -1 };

            if (obj is GameObject go) {
                result.name = go.name;
                result.parent = go.transform.parent?.gameObject.GetInstanceID() ?? -1;
                result.hasParent = go.transform.parent != null;
                result.path = GetPath(result);
                result.depth = result.path.Count - 1;
                result.hasChildren = go.transform.childCount > 0;

                if (!result.hasParent) {
                    var scene = go.scene;
                    var index = 0;
                    foreach (var sceneRoot in scene.GetRootGameObjects()) {
                        if (sceneRoot.GetInstanceID() == go.GetInstanceID()) {
                            result.sceneRootIndex = index;
                            break;
                        }

                        index++;
                    }

                    result.sceneRootIndex = index;
                } else {
                    result.sceneRootIndex = result.path[0].sceneRootIndex;
                }

                var matchingRules = new List<RuleData>();
                foreach (var rule in Configuration.rules) {
                    if (rule.hasGlobRule && obj.name.Glob(rule.globRule)) {
                        matchingRules.Add(rule);
                    }

                    if (rule.hasRegexRule && new Regex(rule.regexRule).IsMatch(obj.name)) {
                        matchingRules.Add(rule);
                    }

                    if (rule.hasScript && rule.script != null) {
                        var script = rule.script.GetClass();
                        if (go.TryGetComponent(script, out var _)) {
                            matchingRules.Add(rule);
                        }
                    }
                }

                // Sort in descending order
                matchingRules.Sort((a, b) => b.priority.CompareTo(a.priority));

                // Take the first 4 icons
                result.icons = new List<(Texture2D, Color)>();
                foreach (var rule in matchingRules) {
                    if (rule.hasIcon && result.icons.Count < 4) {
                        var icon = EditorGUIUtility.IconContent(rule.iconPath);
                        var tex = icon.image as Texture2D;
                        result.icons.Add((tex, rule.iconColor));
                    }
                }

                // Apply the highest priority highlight color
                foreach (var rule in matchingRules) {
                    if (rule.hasHighlight) {
                        result.hasHighlight = true;
                        result.highlightColor = rule.highlightColor;
                        break;
                    }
                }

                result.treeTexture =
                    DrawCombinedLineIntoTexture(TreeColorSampler.GetLineColors(result), !result.hasChildren, result.name.StartsWith("#"));
            }


            return result;
        } catch (System.Exception e) {
            Debug.LogError(e);
            return null;
        }
    }


    private static List<ObjectAnalyzerResult> GetPath(ObjectAnalyzerResult self) {
        if (!self.hasParent) return new List<ObjectAnalyzerResult> { self };

        var parent = GetOrAnalyze(self.parent);
        if (parent != null) return new List<ObjectAnalyzerResult>(parent.path) { self };
        return new List<ObjectAnalyzerResult> { self };
    }


    public static Color FromHex(string hex) {
        return ColorUtility.TryParseHtmlString(hex, out var color) ? color : Color.white;
    }

    public static bool Glob(this string str, string pattern) {
        return new Regex(
            "^" + Regex.Escape(pattern).Replace(@"\*", ".*").Replace(@"\?", ".") + "$",
            RegexOptions.IgnoreCase | RegexOptions.Singleline
        ).IsMatch(str);
    }
}

public class ObjectAnalyzerResult {
    public string name;
    public int depth;
    public bool hasChildren;

    public bool hasParent;
    public int parent;

    public int sceneRootIndex;
    public List<ObjectAnalyzerResult> path;

    public Texture2D treeTexture;

    public bool hasHighlight;
    public Color highlightColor;
    public List<(Texture2D, Color)> icons;

    public bool gcMark = false;
}

public interface ITreeColorSampler {
    public List<Color> GetLineColors(ObjectAnalyzerResult result);
}

public class PaletteTreeColorSampler : ITreeColorSampler {
    public List<Color> palette;
    public bool alternate;
    public bool useBaseColor;

    public List<Color> GetLineColors(ObjectAnalyzerResult result) {
        var p = palette?.ToList();

        if (p == null || p.Count == 0) {
            return new List<Color>();
        }
        
        if (useBaseColor) {
            p.RemoveAt(0);
        }


        if (result.sceneRootIndex % 2 == 0 && alternate) {
            p.Reverse();
        }

        var colors = new List<Color>();

        var start = 0;
        for (var i = 0; i < result.path.Count - (useBaseColor ? 1 : 0); i++) {
            var sample = p[(start + i) % p.Count];
            colors.Add(sample);
        }

        if (useBaseColor) colors.Insert(0, palette[0]);

        return colors;
    }
}

public class MonotoneTreeColorSampler : ITreeColorSampler {
    public Color color;

    public List<Color> GetLineColors(ObjectAnalyzerResult result) {
        return result.path.Select(_ => color).ToList();
    }
}