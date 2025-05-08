using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AtClient.Helpers.Functionality.Extensions;
using Editor.Editor;
using Game.Model;
using Game.Model.Job;
using Game.Model.ValueObjects;
using Game.Presentation;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using Material = UnityEngine.Material;

public class DataPeekerModelWindow : EditorWindow
{
    private static Dictionary<Type, DataPeekerModelWindow> openWindows = new Dictionary<Type, DataPeekerModelWindow>();
    private static Dictionary<Type, (FieldInfo[], PropertyInfo[])> typeCache = new Dictionary<Type, (FieldInfo[], PropertyInfo[])>();

    private Type _modelType;
    private object _modelInstance;
    private bool _isDefaultInstance;
    private Vector2 _scrollPosition;
    private HashSet<object> _visitedObjects = new HashSet<object>();

    private Color _color1 = new Color(0.9f, 0.9f, 0.9f);
    private Color _color2 = new Color(0.2f, 0.2f, 0.2f);
    private Color _backingFieldColor = new Color(1.0f, 0.65f, 0.0f);
    private bool _useColor1 = true;

    private HashSet<Type> _simpleTypes = new HashSet<Type> { typeof(DateTime), typeof(decimal), typeof(TimeSpan), typeof(ItemCollectionVO), typeof(Material), typeof(IPrice) };
    private HashSet<Type> _ignoredTypes = new HashSet<Type> { typeof(GameModelContext), typeof(PresentationContext) };

    private string _searchTerm = "";
    private string _previousSearchTerm = "";
    private bool _isSearchActive = false;

    private DataPeekerModelItem _root;
    private List<DataPeekerModelItem> _endElements;

    private Texture2D lineTexture;

    private void OnGUI()
    {
        EditorGUI.BeginChangeCheck();
        DrawTopOptions();
        if (EditorGUI.EndChangeCheck())
        {
            _previousSearchTerm = _searchTerm;
            UpdateSearchMatches(_searchTerm);
        }

        _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
        DisplayModelHierarchy(_root); // Start from the root node
        EditorGUILayout.EndScrollView();
    }

    private void DrawTopOptions()
    {
        _searchTerm = EditorGUILayout.TextField("Search", _previousSearchTerm);
    }

    private void UpdateSearchMatches(string searchTerm)
    {
        ResetSearchMatches(); // First reset all search matches to false
        if (string.IsNullOrEmpty(searchTerm))
        {
            // If no search term is provided, set everything to match
            _isSearchActive = false;
            SetSearchMatch(_root, true);
        }
        else
        {
            _isSearchActive = true;
            string[] queries = searchTerm.Split(new string[] { "||" }, StringSplitOptions.RemoveEmptyEntries);
            // Perform the search from every leaf upward
            foreach (DataPeekerModelItem leaf in _endElements)
            {
                CheckAndMarkUpwards(leaf, queries);
            }
        }
    }

    private void ResetSearchMatches()
    {
        SetSearchMatch(_root, false); // Reset match and expand states starting from the root
    }

    private bool CheckAndMarkUpwards(DataPeekerModelItem item, string[] queries)
    {
        if (item == null) return false;

        // Check current item for a match
        bool matches = queries.Any(query =>
            item.Name.IndexOf(query.Trim(), StringComparison.OrdinalIgnoreCase) >= 0 ||
            (item.Value != null && item.Value.ToString().IndexOf(query.Trim(), StringComparison.OrdinalIgnoreCase) >= 0));

        if (matches)
        {
            SetSearchMatch(item, true);

            DataPeekerModelItem current = item;
            while (current.Parent != null)
            {
                current.IsExpandedBySearch = true; // Expand all parents
                current.MatchesSearch = true; // Mark all parents as matching
                current = current.Parent;
            }
        }
        else
        {
            if (item.Parent != null)
            {
                matches = CheckAndMarkUpwards(item.Parent, queries);
            }
        }

        return matches;
    }

    private void SetSearchMatch(DataPeekerModelItem item, bool match)
    {
        item.MatchesSearch = match;
        item.IsExpandedBySearch = match; // Expand if it matches

        foreach (DataPeekerModelItem child in item.Children)
        {
            SetSearchMatch(child, match); // Recursively set all children to match
        }
    }

    public static void ShowWindow(Type modelType)
    {
        if (openWindows.TryGetValue(modelType, out DataPeekerModelWindow existingWindow))
        {
            existingWindow.Focus();
        }
        else
        {
            DataPeekerModelWindow peekerModelWindow = CreateInstance<DataPeekerModelWindow>();
            peekerModelWindow._modelType = modelType;
            peekerModelWindow.InitializeModelInstance();
            peekerModelWindow.titleContent = new GUIContent(modelType.Name);

            if (openWindows.Count > 0)
            {
                Rect existingWindowPosition = openWindows.Values.First().position;
                peekerModelWindow.position = new Rect(existingWindowPosition.x + 30, existingWindowPosition.y + 30, existingWindowPosition.width, existingWindowPosition.height);
            }

            peekerModelWindow.Show();
            openWindows[modelType] = peekerModelWindow;
        }
    }

    private void InitializeModelInstance()
    {
        _root = new DataPeekerModelItem(); // Create the root node
        _endElements = new List<DataPeekerModelItem>();
        
        if (Application.isPlaying == false)
        {
            _modelInstance = Activator.CreateInstance(_modelType);
            _isDefaultInstance = true;
        }
        else
        {
            if (DataPeekerModelsListWindow.SharedContext != null)
            {
                MethodInfo getMethod = DataPeekerModelsListWindow.SharedContext.GetType().GetMethod("Get").MakeGenericMethod(_modelType);
                _modelInstance = getMethod.Invoke(DataPeekerModelsListWindow.SharedContext, null);
                _isDefaultInstance = _modelInstance == null;
            }
            else
            {
                _modelInstance = Activator.CreateInstance(_modelType);
                _isDefaultInstance = true;
            }
        }
        
        _root.Children.AddRange(BuildModelHierarchy(_modelInstance, _modelType, 0, _root));
        CollectEndElements(_root);
    }

    private void CollectEndElements(DataPeekerModelItem item)
    {
        if (item.Children.Count == 0)
        {
            _endElements.Add(item); // This is a leaf node
        }
        else
        {
            foreach (DataPeekerModelItem child in item.Children)
            {
                CollectEndElements(child);
            }
        }
    }


    private List<DataPeekerModelItem> BuildModelHierarchy(object obj, Type type, int indentLevel, DataPeekerModelItem parent = null)
    {
        List<DataPeekerModelItem> modelItems = new List<DataPeekerModelItem>();
        
        if (obj == null || _visitedObjects.Contains(obj))
            return modelItems;
        
        _visitedObjects.Add(obj);

        (FieldInfo[] fields, PropertyInfo[] properties) = GetTypeProperties(type);
        var backingFields = new Dictionary<string, FieldInfo>();

        // Collect all relevant backing fields in a dictionary
        foreach (FieldInfo field in fields)
        {
            if (field.Name.Contains("k__BackingField"))
            {
                backingFields[field.Name] = field;
            }
        }

        // Process properties
        foreach (PropertyInfo property in properties)
        {
            if (!property.CanRead || _ignoredTypes.Contains(property.PropertyType) || property.GetIndexParameters().Length > 0)
            {
                continue;
            }

            string backingFieldName = $"<{property.Name}>k__BackingField";
            bool isBackingField = backingFields.ContainsKey(backingFieldName);
            
            if (!property.CanWrite && isBackingField)
                continue;

            PropertyInfo capturedProperty = property;
            object propertyValue = null;
            try
            {
                propertyValue = capturedProperty.GetValue(obj);
            }
            catch(Exception ex)
            {
                propertyValue = null;
            }
            DataPeekerModelItem dataPeekerModelItem = new DataPeekerModelItem(capturedProperty.Name, propertyValue, capturedProperty.PropertyType, indentLevel, parent, isBackingField: isBackingField);
            dataPeekerModelItem.SetBinding(() => propertyValue, capturedProperty.CanWrite == true ? newValue => capturedProperty.SetValue(obj, newValue) : null);
            ProcessValue(propertyValue, capturedProperty.PropertyType, indentLevel, dataPeekerModelItem);

            modelItems.Add(dataPeekerModelItem);
        }

        // Process fields
        foreach (FieldInfo field in fields)
        {
            if (backingFields.ContainsKey(field.Name) || _ignoredTypes.Contains(field.FieldType))
                continue;

            FieldInfo capturedField = field;
            object fieldValue = null;
            try
            {
                fieldValue = capturedField.GetValue(obj);
            }
            catch(Exception ex)
            {
                fieldValue = null;
            }
            
            DataPeekerModelItem dataPeekerModelItem = new DataPeekerModelItem(capturedField.Name, fieldValue, capturedField.FieldType, indentLevel, parent);
            dataPeekerModelItem.SetBinding(() => fieldValue, newValue => capturedField.SetValue(obj, newValue));
            ProcessValue(fieldValue, capturedField.FieldType, indentLevel, dataPeekerModelItem);

            modelItems.Add(dataPeekerModelItem);
        }

        _visitedObjects.Remove(obj);
        return modelItems;
    }

    private void ProcessValue(object value, Type type, int indentLevel, DataPeekerModelItem parent)
    {
        if (typeof(IList).IsAssignableFrom(type))
        {
            IList list = value as IList;
            if (list != null)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    // Create a new DataPeekerModelItem for each element in the list
                    DataPeekerModelItem item = new DataPeekerModelItem($"Item {i}", list[i], list[i]?.GetType() ?? type, indentLevel + 1, parent);
                    // Recursively build the hierarchy for each element, using the current item as parent
                    item.Children.AddRange(BuildModelHierarchy(list[i], list[i]?.GetType() ?? type, indentLevel + 2, item));
                    parent.Children.Add(item);
                }
            }
        }
        else if (typeof(IDictionary).IsAssignableFrom(type))
        {
            IDictionary dictionary = value as IDictionary;
            if (dictionary != null)
            {
                foreach (DictionaryEntry entry in dictionary)
                {
                    // Create a new DataPeekerModelItem for each entry in the dictionary
                    DataPeekerModelItem item = new DataPeekerModelItem($"{entry.Key}", entry.Value, entry.Value?.GetType() ?? type, indentLevel + 1, parent);
                    // Recursively build the hierarchy for the value of each dictionary entry, using the current item as parent
                    item.Children.AddRange(BuildModelHierarchy(entry.Value, entry.Value?.GetType() ?? type, indentLevel + 2, item));
                    parent.Children.Add(item);
                }
            }
        }
        else if (!IsSystemType(type))
        {
            // If the type is not a system type, directly build the hierarchy for this object
            parent.Children.AddRange(BuildModelHierarchy(value, type, indentLevel + 1, parent));
        }
    }

    private bool IsSystemType(Type type)
    {
        return type.Namespace != null && (type.Namespace.StartsWith("System") || type.Namespace.StartsWith("Microsoft"));
    }

    private void DisplayModelItem(DataPeekerModelItem item)
    {
        EditorGUI.indentLevel = item.IndentLevel;
        float labelHeight = EditorGUIUtility.singleLineHeight;

        // Calculate available space for the label
        float labelWidth = 300;
        EditorGUIUtility.labelWidth = labelWidth;

        Rect rect = EditorGUILayout.BeginVertical("box");
        GUILayout.Space(1f);

        // Add some content to the vertical layout before calling GetLastRect
        if (item.IsBackingField == true)
        {
            CreateBackingFieldLabel(item);
        }

        Color originalColor = GUI.backgroundColor;
        GUI.backgroundColor = _useColor1 ? _color1 : _color2;
        _useColor1 = !_useColor1;
        EditorGUILayout.BeginVertical();
        GUI.backgroundColor = originalColor;

        if (item.Value == null || _simpleTypes.Contains(item.Type))
        {
            string displayValue = item.Value == null ? "null" : item.Value.ToString();
            EditorGUILayout.LabelField(CreateIndentedLabel(item.IndentLevel, item.Name), displayValue, GUILayout.Height(labelHeight));
            DrawHierarchyLines(item.IndentLevel, rect);
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndVertical();
            return;
        }

        DrawItemField(item, labelHeight);
        DrawHierarchyLines(item.IndentLevel, rect);

        EditorGUILayout.EndVertical();
        EditorGUILayout.EndVertical();
    }

    private void DrawItemField(DataPeekerModelItem item, float labelHeight)
    {
        // Handle different types with specific UI controls
        GUILayoutOption[] guiLayoutOption =  {GUILayout.Height(labelHeight), GUILayout.MinWidth(150)};

        string indentedLabel = CreateIndentedLabel(item.IndentLevel, item.Name);
        if (item.Type == typeof(int) || item.Type == typeof(int?))
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(indentedLabel, GUILayout.Height(labelHeight));
            item.SetBoundValue(EditorGUILayout.IntField((int)item.Value, guiLayoutOption));
            EditorGUILayout.EndHorizontal();
        }
        else if (item.Type == typeof(float) || item.Type == typeof(float?))
        {
            item.SetBoundValue(EditorGUILayout.FloatField(indentedLabel, (float)item.Value, guiLayoutOption));
        }
        else if (item.Type == typeof(string) || item.Type == typeof(string))
        {
            item.SetBoundValue(EditorGUILayout.TextField(indentedLabel, (string)item.Value, guiLayoutOption));
        }
        else if (item.Type == typeof(bool) || item.Type == typeof(bool?))
        {
            item.SetBoundValue(EditorGUILayout.Toggle(indentedLabel, (bool)item.Value, guiLayoutOption));
        }
        else if (item.Type == typeof(Vector2) || item.Type == typeof(Vector2?))
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(indentedLabel, GUILayout.Height(labelHeight));
            item.SetBoundValue(EditorGUILayout.Vector2Field(GUIContent.none, (Vector2)item.Value, guiLayoutOption));
            EditorGUILayout.EndHorizontal();
        }
        else if (item.Type == typeof(Vector3) || item.Type == typeof(Vector3?))
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(indentedLabel, GUILayout.Height(labelHeight));
            item.SetBoundValue(EditorGUILayout.Vector3Field(GUIContent.none,(Vector3)item.Value, guiLayoutOption));
            EditorGUILayout.EndHorizontal();
        }
        else if (item.Type == typeof(Vector4) || item.Type == typeof(Vector4?))
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(indentedLabel, GUILayout.Height(labelHeight));
            item.SetBoundValue(EditorGUILayout.Vector4Field(GUIContent.none,(Vector4)item.Value, guiLayoutOption));
            EditorGUILayout.EndHorizontal();
        }
        else if (typeof(IList).IsAssignableFrom(item.Type))
        {
            DisplayListField(item);
        }
        else if (typeof(IDictionary).IsAssignableFrom(item.Type))
        {
            DisplayDictionaryField(item);
        }
        else if (item.Type.IsClass || (item.Type.IsValueType && !item.Type.IsPrimitive && !item.Type.IsEnum))
        {
            if (IsItemExpanded(item, $"{item.Name} ({item.Type.Name}) | {item.IsExpanded} | {item.IsExpandedBySearch}"))
            {
                DisplayModelHierarchy(item);
            }
        }
        else
        {
            GUILayout.Label(indentedLabel + ": " + item.Value, guiLayoutOption);
            GUILayout.Space(labelHeight);
        }
        

    }

    private void CreateBackingFieldLabel(DataPeekerModelItem item)
    {
        string itemName = item.Name[..1].ToLower() + item.Name[1..];

        Color defaultColor = GUI.color;
        GUI.color = _backingFieldColor;
        EditorGUILayout.LabelField(CreateIndentedLabel(item.IndentLevel, $"_{itemName}"), GUILayout.Height(EditorGUIUtility.singleLineHeight));
        GUI.color = defaultColor;
    }

    private void DisplayModelHierarchy(DataPeekerModelItem root)
    {
        if (root == null) return;

        foreach (DataPeekerModelItem item in root.Children)
        {
            if (_isSearchActive == false || (item.MatchesSearch == true && _isSearchActive == true))
            {
                DisplayModelItem(item);
            }
        }
    }

    private void CacheTypeProperties(Type type)
    {
        BindingFlags bindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        FieldInfo[] fields = type.GetFields(bindingFlags);
        PropertyInfo[] properties = type.GetProperties(bindingFlags);
        typeCache[type] = (fields, properties);
    }

    private (FieldInfo[], PropertyInfo[]) GetTypeProperties(Type type)
    {
        if (!typeCache.ContainsKey(type))
        {
            CacheTypeProperties(type);
        }

        return typeCache[type];
    }

    private void OnEnable()
    {
        EditorApplication.update += OnEditorUpdate;
        if (lineTexture == null)
        {
            InitializeLineTexture();
        }
    }

    private void OnEditorUpdate()
    {
        if (_isDefaultInstance && DataPeekerModelsListWindow.SharedContext != null)
        {
            MethodInfo getMethod = DataPeekerModelsListWindow.SharedContext.GetType().GetMethod("Get").MakeGenericMethod(_modelType);
            object contextModelInstance = getMethod.Invoke(DataPeekerModelsListWindow.SharedContext, null);

            if (contextModelInstance != null)
            {
                _modelInstance = contextModelInstance;
                BuildModelHierarchy(_modelInstance, _modelType, 0);
                _isDefaultInstance = false;
            }
        }

        Repaint();
    }

    private void DisplayListField(DataPeekerModelItem item)
    {
        object itemValue = item.Value;
        IList list = itemValue as IList;
        if (list == null)
            return;
        
        if (IsItemExpanded(item, $"{item.Name} (List) [{list.Count}]") == false)
        {
            return;
        }
        
        foreach (DataPeekerModelItem child in item.Children)
        {
            if (child.MatchesSearch == true)
            {
                DisplayModelItem(child);
            }
        }
    }

    private void DisplayDictionaryField(DataPeekerModelItem item)
    {
        object itemValue = item.Value;
        IDictionary dictionary = itemValue as IDictionary;
        if (dictionary == null)
            return;

        if (IsItemExpanded(item, $"{item.Name} (Dictionary) [{dictionary.Count}]") == true)
        {
            foreach (DataPeekerModelItem child in item.Children)
            {
                DisplayModelItem(child);
            }
        }
    }
    
    private bool IsItemExpanded(DataPeekerModelItem item, string label)
    {
        if (item == null)
            return false;
        
        if (_isSearchActive == true)
        {
            item.IsExpandedBySearch = EditorGUILayout.Foldout(item.IsExpandedBySearch, CreateIndentedLabel(item.IndentLevel, label), true);
            return item.IsExpandedBySearch;
        }
        
        item.IsExpanded = EditorGUILayout.Foldout(item.IsExpanded, CreateIndentedLabel(item.IndentLevel, label), true);
        return item.IsExpanded;
    }

    private void InitializeLineTexture()
    {
        lineTexture = new Texture2D(1, 1);
        lineTexture.SetPixel(0, 0, Color.gray);
        lineTexture.Apply();
    }

    private void OnDisable()
    {
        EditorApplication.update -= OnEditorUpdate;
        if (lineTexture != null)
        {
            DestroyImmediate(lineTexture);
            lineTexture = null;
        }
    }

    private void OnDestroy()
    {
        openWindows?.Remove(_modelType);
        if (lineTexture != null)
        {
            DestroyImmediate(lineTexture);
            lineTexture = null;
        }
    }

    private string CreateIndentedLabel(int indentLevel, string label)
    {
        return new string(' ', indentLevel * 2) + label;
    }

    private void DrawHierarchyLines(int depth, Rect rect)
    {
        if (lineTexture == null) InitializeLineTexture();

        for (int i = 0; i < depth; i++)
        {
            float indent = 20 * i + 10; // Adjust based on your indentation logic
            GUI.DrawTexture(new Rect(indent, rect.yMin, 2, rect.height), lineTexture);
            if (i == depth - 1)
            {
                // Draw horizontal line to the text
                GUI.DrawTexture(new Rect(indent, rect.yMin + rect.height / 2, 10, 2), lineTexture);
            }
        }
    }
}