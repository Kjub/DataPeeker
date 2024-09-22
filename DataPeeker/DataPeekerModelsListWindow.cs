using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
        if (modelTypes.Count == 0)
        {
            EditorGUILayout.LabelField("No models found implementing IReadOnlyModel.");
            return;
        }

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        foreach (Type modelType in modelTypes)
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
                    ManagerBehaviourBase manager = FindObjectOfType(type) as ManagerBehaviourBase;
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

        modelTypes.Sort((type1, type2) => type1.Name.CompareTo(type2.Name));
    }

    private void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            FindManagerBehaviourBase();
        }
    }
}