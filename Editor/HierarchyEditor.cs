using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

public class HierarchyEditor : EditorWindow {
    
    public static HierarchyData data;
    
    public static void ShowWindow(HierarchyData newData) {
        data = newData;
        var window = GetWindow<HierarchyEditor>();
        window.titleContent = new GUIContent("Hierarchy Editor");
        window.Refresh();
    }

    public VisualElement content;
    public VisualElement ruleList;
    public int selectedRuleIndex = 0;
    
    
    public static Color defaultColor = new Color(0.803921569f, 0.803921569f, 0.803921569f);
    
    private void CreateGUI() {
        rootVisualElement.Clear();

        if (data == null) {
            data = AssetDatabase.LoadAssetAtPath<HierarchyData>("Assets/Editor Default Resources/HierarchyData.asset");
        }
        
        if (data == null) return;
        
        content = new VisualElement();
        var contentScroll = new ScrollView();
        contentScroll.Add(content);
        BuildEditor();
        
        ruleList = new VisualElement();
        var ruleScroll = new ScrollView();
        
        // Add rule controls
        var addRule = new VisualElement();
        addRule.RegisterCallback<MouseUpEvent>(evt => {
            ArrayUtility.Add(ref data.rules, new RuleData() {
                name = $"Rule {data.rules.Length}",
                
                iconColor = Color.white, 
                hasIcon = false,
                iconPath = "LucideIcons/bookmark.png"
            });
            selectedRuleIndex = data.rules.Length - 1;
            SaveChanges();
            BuildRules();
            BuildEditor();
        });
        addRule.style.width = 16;
        addRule.style.height = 16;
        addRule.style.backgroundImage = Background.FromTexture2D(EditorGUIUtility.Load("LucideIcons/book-plus.png") as Texture2D);
        addRule.style.unityBackgroundImageTintColor = defaultColor;
        
        var refreshButton = new VisualElement();
        refreshButton.RegisterCallback<MouseUpEvent>(evt => Refresh());
        refreshButton.style.width = 16;
        refreshButton.style.height = 16;
        refreshButton.style.backgroundImage = Background.FromTexture2D(EditorGUIUtility.Load("LucideIcons/refresh-ccw.png") as Texture2D);
        refreshButton.style.unityBackgroundImageTintColor = defaultColor;


        var ruleControls = new VisualElement { style = {
            paddingTop = 4,
            paddingLeft = 4,
            paddingRight = 4,
            paddingBottom = 4
        }};
        ruleControls.style.flexDirection = FlexDirection.Row;
        ruleControls.Add(new Label("Rules") {
            style = {
                flexGrow = 1,
            }
        });
        ruleControls.Add(addRule);
        ruleControls.Add(refreshButton);


        ruleScroll.Add(ruleControls);
        
        ruleScroll.Add(ruleList);
        BuildRules();
        
        var splitPane = new TwoPaneSplitView();
        splitPane.contentContainer.Add(ruleScroll);
        splitPane.contentContainer.Add(contentScroll);
        splitPane.fixedPaneInitialDimension = 200;
        
        rootVisualElement.Add(splitPane);
    }
    
    public void SaveChanges() {
        //EditorUtility.SetDirty(data);
        
        // Undoable change support
        EditorUtility.SetDirty(data);
        BetterHierarchy.ResetCache();
        BuildRules();
    }

    public void Refresh() {
        BuildRules();
        BuildEditor();
    }
    
    public void BuildEditor() {
        content.Clear();
        if (data == null) {
            return;
        }
        
        
        if (data.rules.Length == 0) {
            return;
        }
        if (selectedRuleIndex < 0) {
            selectedRuleIndex = 0;
        } else if (selectedRuleIndex >= data.rules.Length) {
            selectedRuleIndex = data.rules.Length - 1;
        }
        
        var selectedRule = data.rules[selectedRuleIndex];
        
        
        var ruleName = new TextField("Rule Name");
        ruleName.value = selectedRule.name;
        ruleName.RegisterValueChangedCallback(evt => {
            selectedRule.name = evt.newValue;
            SaveChanges();
        });
        content.Add(ruleName);
        
        var priority = new IntegerField("Priority");
        priority.value = selectedRule.priority;
        priority.RegisterValueChangedCallback(evt => {
            selectedRule.priority = evt.newValue;
            SaveChanges();
        });
        content.Add(priority);
        
        var lucideIconNames = EditorGUIUtility.Load("LucideIcons/index.txt") as TextAsset;
        if (lucideIconNames != null) {
            
            var hasIconToggle = new Toggle("Enable Icon");
            hasIconToggle.style.marginTop = 16;
            hasIconToggle.RegisterValueChangedCallback(evt => {
                selectedRule.hasIcon = evt.newValue;
                SaveChanges();
                BuildEditor();
            });
            hasIconToggle.value = selectedRule.hasIcon;
            content.Add(hasIconToggle);
            
            if (selectedRule.hasIcon) {
                var currentIcon = new Image();

                var hasIconValue = selectedRule.iconPath.Trim() != "";
                if (hasIconValue) {
                    try {
                        currentIcon.image = EditorGUIUtility.Load(selectedRule.iconPath) as Texture2D;
                    } catch (Exception e) {
                        Debug.LogWarning("Icon path is invalid");
                        selectedRule.iconPath = "";
                        hasIconValue = false;
                    }
                }
                
                currentIcon.style.width = 40;
                currentIcon.style.height = 40;
                currentIcon.tintColor = selectedRule.iconColor;
                currentIcon.style.alignSelf = Align.Center;
                content.Add(currentIcon);
                
                var textureField = new ObjectField("Icon");
                textureField.objectType = typeof(Texture2D);
                textureField.value = hasIconValue ? currentIcon.image : null;
                textureField.RegisterValueChangedCallback(evt => {
                    var path = AssetDatabase.GetAssetPath(evt.newValue);
                    if (!path.StartsWith("Assets/Editor Default Resources")) {
                        Debug.LogError("Icon must be in the Editor Default Resources folder");
                        return;
                    }
                    
                    selectedRule.iconPath = path.Replace("Assets/Editor Default Resources/", "");
                    currentIcon.image = evt.newValue as Texture2D;
                    SaveChanges();
                });
                content.Add(textureField);
                
                
                var iconColor = new ColorField("Icon Color");
                iconColor.value = selectedRule.iconColor;
                iconColor.RegisterValueChangedCallback(evt => {
                    selectedRule.iconColor = evt.newValue;
                    currentIcon.tintColor = evt.newValue;
                    SaveChanges();
                });
                content.Add(iconColor);
                
                content.Add(PaletteSelector(BetterHierarchy.DefaultPalette, color => {
                    selectedRule.iconColor = color;
                    iconColor.value = color;
                    currentIcon.tintColor = color;
                    SaveChanges();
                }));
                
                var names = lucideIconNames.text.Split(";").Select(x => $"LucideIcons/{x.Trim()}.png").ToArray();
                var iconList = new VisualElement();
                iconList.style.flexDirection = FlexDirection.Row;
                iconList.style.flexShrink = 0;
                iconList.style.flexWrap = Wrap.Wrap;
        
                for (int i = 0; i < names.Length; i++) {
                    var icon = EditorGUIUtility.Load(names[i]) as Texture2D;
                    var iconElement = new Image();
                    iconElement.image = icon;
                    iconElement.style.width = 20;
                    iconElement.style.height = 20;
            
                    var iconIndex = i;
                    iconElement.RegisterCallback<MouseUpEvent>(evt => {
                        selectedRule.iconPath = names[iconIndex];
                        currentIcon.image = icon;
                        SaveChanges();
                    });
            
                    iconList.Add(iconElement);
                }
                
                var iconListFoldout = new Foldout();
                iconListFoldout.text = "Icon List";
                iconListFoldout.Add(iconList);
                iconListFoldout.value = false;
                
                content.Add(iconListFoldout);
            }
        }

        
        
        // Highlight color section
        var hasHighlightToggle = new Toggle("Enable Highlight");
        hasHighlightToggle.RegisterValueChangedCallback(evt => {
            selectedRule.hasHighlight = evt.newValue;
            SaveChanges();
            BuildEditor();
        });
        hasHighlightToggle.value = selectedRule.hasHighlight;
        hasHighlightToggle.style.marginTop = 16;
        content.Add(hasHighlightToggle);
        
        if (selectedRule.hasHighlight) {
            var highlightColor = new ColorField("Highlight Color");
            highlightColor.value = selectedRule.highlightColor;
            highlightColor.RegisterValueChangedCallback(evt => {
                selectedRule.highlightColor = evt.newValue;
                SaveChanges();
            });
            content.Add(highlightColor);
            
            content.Add(PaletteSelector(BetterHierarchy.DefaultPalette, color => {
                selectedRule.highlightColor = color;
                highlightColor.value = color;
                SaveChanges();
            }, 0.15f));
        }
        
        // MonoScript section
        var monoScriptField = new ObjectField("Script");
        monoScriptField.objectType = typeof(MonoScript);
        monoScriptField.value = selectedRule.script;
        monoScriptField.RegisterValueChangedCallback(evt => {
            selectedRule.script = evt.newValue as MonoScript;
            SaveChanges();
        });
        monoScriptField.style.display = selectedRule.hasScript ? DisplayStyle.Flex : DisplayStyle.None;
        
        var hasScriptToggle = new Toggle("Enabled");
        hasScriptToggle.RegisterValueChangedCallback(evt => {
            selectedRule.hasScript = evt.newValue;
            monoScriptField.style.display = evt.newValue ? DisplayStyle.Flex : DisplayStyle.None;
            SaveChanges();
        });
        hasScriptToggle.value = selectedRule.hasScript;
        hasScriptToggle.style.marginTop = 16;
        var scriptTab = new Tab("Script", Background.FromTexture2D(EditorGUIUtility.Load("LucideIcons/file-code-2.png") as Texture2D));
        scriptTab.Add(hasScriptToggle);
        scriptTab.Add(monoScriptField);
        
        // Glob rule section
        var globRuleField = new TextField("Glob Rule");
        globRuleField.value = selectedRule.globRule;
        globRuleField.RegisterValueChangedCallback(evt => {
            selectedRule.globRule = evt.newValue;
            SaveChanges();
        });
        globRuleField.style.display = selectedRule.hasGlobRule ? DisplayStyle.Flex : DisplayStyle.None;
        var hasGlobRuleToggle = new Toggle("Enabled");
        hasGlobRuleToggle.RegisterValueChangedCallback(evt => {
            selectedRule.hasGlobRule = evt.newValue;
            globRuleField.style.display = evt.newValue ? DisplayStyle.Flex : DisplayStyle.None;
            SaveChanges();
        });
        hasGlobRuleToggle.value = selectedRule.hasGlobRule;
        hasGlobRuleToggle.style.marginTop = 16;
        var globTab = new Tab("Glob", Background.FromTexture2D(EditorGUIUtility.Load("LucideIcons/asterisk.png") as Texture2D));
        globTab.Add(hasGlobRuleToggle);
        globTab.Add(globRuleField);
        
        // Regex rule section
        var regexRuleField = new TextField("Regex Rule");
        regexRuleField.value = selectedRule.regexRule;
        regexRuleField.RegisterValueChangedCallback(evt => {
            selectedRule.regexRule = evt.newValue;
            SaveChanges();
        });
        regexRuleField.style.display = selectedRule.hasRegexRule ? DisplayStyle.Flex : DisplayStyle.None;
        
        var hasRegexRuleToggle = new Toggle("Enabled");
        hasRegexRuleToggle.RegisterValueChangedCallback(evt => {
            selectedRule.hasRegexRule = evt.newValue;
            regexRuleField.style.display = evt.newValue ? DisplayStyle.Flex : DisplayStyle.None;
            SaveChanges();
        });
        hasRegexRuleToggle.value = selectedRule.hasRegexRule;
        hasRegexRuleToggle.style.marginTop = 16;
        var regexTab = new Tab("Regex", Background.FromTexture2D(EditorGUIUtility.Load("LucideIcons/regex.png") as Texture2D));
        regexTab.Add(hasRegexRuleToggle);
        regexTab.Add(regexRuleField);


        var tabLabel = new Label("Rule Selectors");
        tabLabel.style.marginTop = 16;
        tabLabel.style.marginBottom = 8;
        var tabContainer = new TabView();
        tabContainer.style.alignSelf = Align.Stretch;
        tabContainer.Add(scriptTab);
        tabContainer.Add(globTab);
        tabContainer.Add(regexTab);
        content.Add(tabLabel);
        content.Add(tabContainer);
        
        // Delete button
        var deleteButton = new Button(() => {
            ArrayUtility.RemoveAt(ref data.rules, selectedRuleIndex);
            selectedRuleIndex = Mathf.Clamp(selectedRuleIndex, 0, data.rules.Length - 1);
            SaveChanges();
            BuildRules();
            BuildEditor();
        });
        deleteButton.text = "Delete Rule";
        deleteButton.style.marginTop = 32;
        
        content.Add(deleteButton);
    }

    private static VisualElement PaletteSelector(List<Color> palette, Action<Color> callback, float alpha = 1.0f) {
        var paletteElement = new VisualElement();
        paletteElement.style.flexDirection = FlexDirection.Row;
        paletteElement.style.flexWrap = Wrap.Wrap;
        paletteElement.style.flexShrink = 0;
        paletteElement.style.paddingTop = 4;
        paletteElement.style.paddingBottom = 4;
        paletteElement.style.paddingLeft = 4;
        paletteElement.style.paddingRight = 4;
        
        foreach (var color in palette) {
            var colorElement = new VisualElement();
            colorElement.style.flexGrow = 1;
            colorElement.style.height = 20;
            colorElement.style.backgroundColor = new Color(color.r, color.g, color.b, alpha);
            colorElement.RegisterCallback<MouseUpEvent>(evt => {
                callback(new Color(color.r, color.g, color.b, alpha));
            });
            paletteElement.Add(colorElement);
        }

        return paletteElement;
        
    }

    public void BuildRules() {
        ruleList.Clear();
        if (data == null) {
            return;
        }

        foreach (var ruleData in data.rules) {
            var element = new VisualElement();
            var label = new Label(ruleData.name ?? "Unnamed Rule");
            element.Add(label);
            
            element.RegisterCallback<MouseDownEvent>(evt => {
                selectedRuleIndex = Array.IndexOf(data.rules, ruleData);
                BuildEditor();
            });
            
            ruleList.Add(element);
        }
    }
}