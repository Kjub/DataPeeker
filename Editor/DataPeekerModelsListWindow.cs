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

    private List<Type> _modelTypes = new List<Type>();
    private List<Type> _filteredModelTypes;
    private string _searchTerm = "";
    private Vector2 _scrollPosition;
    private bool _focusSearchNextFrame = true;
    
    [RuntimeInitializeOnLoadMethod]
    private static void InitDataPeekerModelList()
    {
        SharedContext = null;
    }

    private void OnEnable()
    {
        FindManagerBehaviourBase();
        FindModelTypes();
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private void OnDestroy()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
    }

    private bool focusSearchNextFrame = true;

    private void OnGUI()
    {
        DrawSearchBar();

        if (focusSearchNextFrame)
        {
            EditorGUI.FocusTextInControl("SearchField");
            focusSearchNextFrame = false;
        }
    }

    private void DrawSearchBar()
    {
        GUILayout.BeginHorizontal(EditorStyles.toolbar);

        GUI.SetNextControlName("SearchField");
        string newSearchTerm = GUILayout.TextField(_searchTerm, GUI.skin.FindStyle("ToolbarSearchTextField"));

        if (!string.IsNullOrEmpty(newSearchTerm))
        {
            if (GUILayout.Button("", GUI.skin.FindStyle("ToolbarSearchCancelButton")))
            {
                newSearchTerm = "";
                GUI.FocusControl(null);
            }
        }

        GUILayout.EndHorizontal();
        GUILayout.Space(10f);

        if (newSearchTerm != _searchTerm)
        {
            _searchTerm = newSearchTerm;
            UpdateFilteredModelTypes();
        }

        if (_filteredModelTypes.Count == 0)
        {
            EditorGUILayout.LabelField("No matching models found.");
            return;
        }

        _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
        foreach (Type modelType in _filteredModelTypes)
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
                    ManagerBehaviourBase manager = FindAnyObjectByType(type) as ManagerBehaviourBase;
                    if (manager != null && manager.Context != null)
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
        _modelTypes.Clear();
        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
        foreach (Assembly assembly in assemblies)
        {
            Type[] types = assembly.GetTypes();
            foreach (Type type in types)
            {
                if (typeof(IReadOnlyModel).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract)
                {
                    _modelTypes.Add(type);
                }
            }
        }
        
        _modelTypes.Sort((type1, type2) => string.Compare(type1.Name, type2.Name, StringComparison.Ordinal));
        
        if (Application.isPlaying == false)
        {
            _modelTypes.Insert(0, typeof(DataPeekerTestModel));
        }

        UpdateFilteredModelTypes();
    }

    private void UpdateFilteredModelTypes()
    {
        if (string.IsNullOrEmpty(_searchTerm))
        {
            _filteredModelTypes = new List<Type>(_modelTypes);
        }
        else
        {
            _filteredModelTypes = _modelTypes
                .Where(type => type.Name.IndexOf(_searchTerm, StringComparison.OrdinalIgnoreCase) >= 0)
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