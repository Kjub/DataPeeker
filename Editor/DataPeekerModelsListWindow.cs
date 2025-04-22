using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Editor.Editor;
using UnityEditor;
using UnityEngine;
using Game.Model;
using Game.Presentation.Managers.Behaviours;

public class DataPeekerModelsListWindow : EditorWindow
{
    public static GameModelContext SharedContext;

    [MenuItem("Tools/Data Peeker")]
    public static void ShowWindow()
    {
        GetWindow<DataPeekerModelsListWindow>("Data Peeker");
    }

    private List<Type> modelTypes = new List<Type>();
    private List<Type> filteredModelTypes;
    private string searchTerm = "";
    private Vector2 scrollPosition;

    private void OnEnable()
    {
        FindManagerBehaviourBase();
        FindModelTypes();
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private void OnDisable()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
    }

    private void OnGUI()
    {
        GUILayout.BeginHorizontal(GUI.skin.FindStyle("Toolbar"));
        string newSearchTerm = GUILayout.TextField(searchTerm, GUI.skin.FindStyle("ToolbarSearchTextField"));
        if (GUILayout.Button("", GUI.skin.FindStyle("ToolbarSearchCancelButton")))
        {
            newSearchTerm = "";
            GUI.FocusControl(null);
        }

        GUILayout.EndHorizontal();
        GUILayout.Space(15f);

        if (newSearchTerm != searchTerm)
        {
            searchTerm = newSearchTerm;
            UpdateFilteredModelTypes();
        }

        if (filteredModelTypes.Count == 0)
        {
            EditorGUILayout.LabelField("No matching models found.");
            return;
        }

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        foreach (Type modelType in filteredModelTypes)
        {
            if (GUILayout.Button(modelType.Name))
            {
                DataPeekerModelWindow.ShowWindow(modelType);
            }
        }

        EditorGUILayout.EndScrollView();
    }

    private void FindManagerBehaviourBase()
    {
        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
        foreach (Assembly assembly in assemblies)
        {
            Type[] types = assembly.GetTypes();
            foreach (Type type in types)
            {
                if (typeof(ManagerBehaviourBase).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract)
                {
                    ManagerBehaviourBase manager = FindFirstObjectByType(type) as ManagerBehaviourBase;
                    if (manager != null)
                    {
                        SharedContext = manager.Context.CommonApplication.GetContext<GameModelContext>();
                        return;
                    }
                }
            }
        }
    }

    private void FindModelTypes()
    {
        modelTypes.Clear();
        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
        foreach (Assembly assembly in assemblies)
        {
            Type[] types = assembly.GetTypes();
            foreach (Type type in types)
            {
                if (typeof(IReadOnlyModel).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract)
                {
                    modelTypes.Add(type);
                }
            }
        }
        
        modelTypes.Sort((type1, type2) => string.Compare(type1.Name, type2.Name, StringComparison.Ordinal));
        
        if (Application.isPlaying == false)
        {
            modelTypes.Insert(0, typeof(DataPeekerTestModel));
        }

        UpdateFilteredModelTypes();
    }

    private void UpdateFilteredModelTypes()
    {
        if (string.IsNullOrEmpty(searchTerm))
        {
            filteredModelTypes = new List<Type>(modelTypes);
        }
        else
        {
            filteredModelTypes = modelTypes
                .Where(type => type.Name.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();
        }
    }

    private void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            FindManagerBehaviourBase();
        }
    }
}