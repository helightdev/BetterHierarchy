using System;
using UnityEditor;
using UnityEngine;

[Serializable]
public class RuleData {
    public string name;
    public int priority;
        
    public bool hasIcon;
    public string iconPath;
    public Color iconColor;

    public bool hasHighlight;
    public Color highlightColor;
        
    public bool hasScript;
    public MonoScript script;
    
    public bool hasGlobRule;
    public string globRule;
    
    public bool hasRegexRule;
    public string regexRule;
}