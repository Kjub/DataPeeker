using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Game.Model;
using Game.Model.ValueObjects;
using Game.Presentation;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Kjub.DataPeeker.Editor
{
    public class DataPeekerModelWindow : EditorWindow
    {
        private const int InitialTraversalDepth = 10;
        private const float HierarchyIndentWidth = 20f;
        private const float HierarchyLineOffset = 10f;
        private const float FoldoutOffsetFromLine = 12f;
        private const float FoldoutTriangleSize = 16f;
        private const float ConnectorEndPadding = 2f;

        private static Dictionary<Type, DataPeekerModelWindow> openWindows = new Dictionary<Type, DataPeekerModelWindow>();
        private static Dictionary<Type, (FieldInfo[], PropertyInfo[])> typeCache = new Dictionary<Type, (FieldInfo[], PropertyInfo[])>();
        private static readonly IEqualityComparer<object> ReferenceComparer = new ReferenceEqualityComparer();

        // Survives domain reloads; everything else is rebuilt from it in OnEnable
        [SerializeField] private string _modelTypeName;

        private Type _modelType;
        private object _modelInstance;
        private bool _isDefaultInstance;
        private Vector2 _scrollPosition;

        private static readonly Color[] HierarchyLineColors =
        {
            new Color32(230, 57, 70, 255),
            new Color32(42, 157, 143, 255),
            new Color32(69, 123, 157, 255),
            new Color32(244, 162, 97, 255),
            new Color32(131, 56, 236, 255),
            new Color32(46, 196, 182, 255),
            new Color32(255, 0, 110, 255),
            new Color32(58, 134, 255, 255),
            new Color32(251, 133, 0, 255),
            new Color32(0, 150, 199, 255),
            new Color32(139, 195, 74, 255),
            new Color32(255, 183, 3, 255),
            new Color32(156, 39, 176, 255),
            new Color32(76, 175, 80, 255),
            new Color32(233, 30, 99, 255),
            new Color32(0, 188, 212, 255),
            new Color32(180, 130, 90, 255),
            new Color32(205, 220, 57, 255),
            new Color32(99, 102, 241, 255),
            new Color32(255, 112, 67, 255)
        };

        private Color _backingFieldColor = new Color(1.0f, 0.65f, 0.0f);
        private int _visibleRowIndex;

        private HashSet<Type> _simpleTypes = new HashSet<Type>
        {
            typeof(DateTime),
            typeof(decimal),
            typeof(TimeSpan),
            typeof(int2),
            typeof(int3),
            typeof(int4),
            typeof(Transform),
            typeof(GameObject),
        };

        // NEW: treat all types under these namespaces as "simple"
        // Example: "Unity.Mathematics" covers int2/int3/etc (and any other structs in that namespace)
        private HashSet<string> _simpleNamespaces = new HashSet<string>
        {
            "Unity.Mathematics",
            "UnityEngine"
        };

        private HashSet<Type> _ignoredTypes = new HashSet<Type> { typeof(GameModelContext), typeof(PresentationContext) };

        private string _searchTerm = "";
        private bool _isSearchActive = false;
        private DataPeekerModelItem _selectedItem;

        private DataPeekerModelItem _root;

        private GUIStyle _rowStyle;
        private bool _treeHasKeyboardFocus;
        private bool _scrollSelectedItemIntoView;
        private bool _didFindSelectedItemRect;
        private Rect _selectedItemRect;
        private Rect _scrollViewRect;
        private bool _didFocusSearch = false;

        [RuntimeInitializeOnLoadMethod]
        private static void InitDataPeeker()
        {
            // Don't recreate the dictionary: open windows re-register in OnEnable before this
            // runs, and with domain reload disabled the existing entries are still valid.
            List<Type> deadEntries = openWindows
                .Where(pair => pair.Value == null)
                .Select(pair => pair.Key)
                .ToList();

            foreach (Type deadEntry in deadEntries)
            {
                openWindows.Remove(deadEntry);
            }
        }

        private void OnGUI()
        {
            HandleKeyboardSelection();

            EditorGUI.BeginChangeCheck();
            DrawTopOptions();
            if (EditorGUI.EndChangeCheck())
            {
                UpdateSearchMatches(_searchTerm);
            }

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            _visibleRowIndex = 0;
            _didFindSelectedItemRect = false;
            DisplayModelHierarchy(_root); // Start from the root node
            EditorGUILayout.EndScrollView();

            if (Event.current.type == EventType.Repaint)
            {
                Rect currentScrollViewRect = GUILayoutUtility.GetLastRect();
                if (currentScrollViewRect.height > 0f)
                {
                    _scrollViewRect = currentScrollViewRect;
                }
            }

            ScrollSelectedItemIntoViewIfNeeded();
        }

        private void DrawTopOptions()
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
            GUILayout.Space(5f);

            if (!_didFocusSearch)
            {
                EditorGUI.FocusTextInControl("SearchField");
                _didFocusSearch = true;
            }

            if (GUI.GetNameOfFocusedControl() == "SearchField")
            {
                _treeHasKeyboardFocus = false;
            }

            if (newSearchTerm != _searchTerm)
            {
                _searchTerm = newSearchTerm;
                UpdateSearchMatches(_searchTerm);
            }
        }

        private void UpdateSearchMatches(string searchTerm)
        {
            if (_root == null)
            {
                return;
            }

            ResetSearchMatches(); // First reset all search matches to false
            if (string.IsNullOrEmpty(searchTerm))
            {
                _isSearchActive = false;
                SetSearchMatch(_root, true);

                if (_selectedItem != null)
                {
                    ExpandToItem(_selectedItem);
                }
            }
            else
            {
                _isSearchActive = true;
                string[] queries = searchTerm.Split(new string[] { "||" }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(query => query.Trim())
                    .Where(query => string.IsNullOrEmpty(query) == false)
                    .ToArray();

                UpdateSearchMatchesRecursive(_root, queries);
            }
        }

        private void ExpandToItem(DataPeekerModelItem item)
        {
            while (item != null)
            {
                item.IsExpanded = true;
                item.IsExpandedBySearch = true;
                item = item.Parent;
            }
        }

        private void ResetSearchMatches()
        {
            SetSearchMatch(_root, false); // Reset match and expand states starting from the root
        }

        private bool UpdateSearchMatchesRecursive(DataPeekerModelItem item, string[] queries)
        {
            if (item == null)
            {
                return false;
            }

            object currentValue = item.GetBoundValue();
            bool matchesSelf = queries.Any(query =>
                item.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                (currentValue != null && currentValue.ToString().IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0));

            bool matchesChild = false;
            foreach (DataPeekerModelItem child in item.Children)
            {
                if (UpdateSearchMatchesRecursive(child, queries))
                {
                    matchesChild = true;
                }
            }

            item.MatchesSearch = matchesSelf || matchesChild;
            item.IsExpandedBySearch = matchesChild;
            return item.MatchesSearch;
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
                peekerModelWindow._modelTypeName = modelType.AssemblyQualifiedName;
                peekerModelWindow.InitializeModelInstance();
                peekerModelWindow.titleContent = new GUIContent(modelType.Name);

                if (openWindows.Count > 0)
                {
                    Rect existingWindowPosition = openWindows.Values.First().position;
                    peekerModelWindow.position = new Rect(existingWindowPosition.x + 30, existingWindowPosition.y + 30,
                        existingWindowPosition.width, existingWindowPosition.height);
                }

                peekerModelWindow.Show();
                openWindows[modelType] = peekerModelWindow;
            }
        }

        private void InitializeModelInstance()
        {
            if (Application.isPlaying == false)
            {
                _modelInstance = Activator.CreateInstance(_modelType);
                _isDefaultInstance = true;
            }
            else
            {
                _modelInstance = TryGetContextModelInstance();
                _isDefaultInstance = _modelInstance == null;

                if (_isDefaultInstance)
                {
                    _modelInstance = Activator.CreateInstance(_modelType);
                }
            }

            RebuildTree();
        }

        private void RebuildTree()
        {
            _selectedItem = null;
            _root = new DataPeekerModelItem();

            if (_modelInstance != null)
            {
                _root.Children.AddRange(BuildModelHierarchy(_modelInstance, _modelType, 0, _root, InitialTraversalDepth,
                    CreateAncestorReferenceSet(null, null)));
            }

            UpdateSearchMatches(_searchTerm);
        }

        private object TryGetContextModelInstance()
        {
            if (DataPeekerModelsListWindow.SharedContext == null || _modelType == null)
            {
                return null;
            }

            MethodInfo getMethod = DataPeekerModelsListWindow.SharedContext.GetType().GetMethod("Get");
            if (getMethod == null)
            {
                return null;
            }

            try
            {
                return getMethod.MakeGenericMethod(_modelType).Invoke(DataPeekerModelsListWindow.SharedContext, null);
            }
            catch (Exception ex)
            {
                Debug.Log($"Error retrieving {_modelType.Name} from shared context: {ex.Message}");
                return null;
            }
        }

        private List<DataPeekerModelItem> BuildModelHierarchy(object obj, Type type, int indentLevel, DataPeekerModelItem parent,
            int remainingDepth, HashSet<object> ancestorReferences)
        {
            List<DataPeekerModelItem> modelItems = new List<DataPeekerModelItem>();

            if (obj == null)
                return modelItems;

            Type resolvedType = obj.GetType();
            if (IsRecursiveReference(obj, resolvedType, ancestorReferences))
            {
                modelItems.Add(CreateRecursiveReferenceItem(indentLevel, parent, resolvedType));
                return modelItems;
            }

            HashSet<object> currentAncestors = CreateAncestorReferenceSet(ancestorReferences, obj);

            (FieldInfo[] fields, PropertyInfo[] properties) = GetTypeProperties(resolvedType);
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
                try
                {
                    if (property.CanRead == false || _ignoredTypes.Contains(property.PropertyType) || property.GetIndexParameters().Length > 0)
                    {
                        continue;
                    }

                    string backingFieldName = $"<{property.Name}>k__BackingField";
                    bool isBackingField = backingFields.ContainsKey(backingFieldName);

                    if (!property.CanWrite && isBackingField)
                        continue;

                    PropertyInfo capturedProperty = property;
                    Func<object> getter = CreateSafePropertyGetter(obj, capturedProperty);
                    object propertyValue = getter();

                    DataPeekerModelItem dataPeekerModelItem = new DataPeekerModelItem(
                        capturedProperty.Name,
                        propertyValue,
                        capturedProperty.PropertyType,
                        indentLevel,
                        parent,
                        isBackingField: isBackingField);

                    dataPeekerModelItem.SetBinding(
                        getter,
                        capturedProperty.CanWrite == true ? newValue => capturedProperty.SetValue(obj, newValue) : null);

                    ConfigureChildLoading(dataPeekerModelItem, remainingDepth - 1, currentAncestors);

                    modelItems.Add(dataPeekerModelItem);
                }
                catch (Exception ex)
                {
                    DataPeekerModelItem dataPeekerModelItem = new DataPeekerModelItem(property.Name, "Cannot be accessed",
                        typeof(string), indentLevel, parent, isBackingField: false);

                    modelItems.Add(dataPeekerModelItem);
                    Debug.Log($"Error processing property {property.Name} on {obj.GetType().Name}: {ex.Message}");
                }
            }

            // Process fields
            foreach (FieldInfo field in fields)
            {
                try
                {
                    if (backingFields.ContainsKey(field.Name) || _ignoredTypes.Contains(field.FieldType))
                        continue;

                    FieldInfo capturedField = field;
                    Func<object> getter = CreateSafeFieldGetter(obj, capturedField);
                    object fieldValue = getter();

                    DataPeekerModelItem dataPeekerModelItem = new DataPeekerModelItem(
                        capturedField.Name,
                        fieldValue,
                        capturedField.FieldType,
                        indentLevel,
                        parent);

                    dataPeekerModelItem.SetBinding(getter, newValue => capturedField.SetValue(obj, newValue));
                    ConfigureChildLoading(dataPeekerModelItem, remainingDepth - 1, currentAncestors);

                    modelItems.Add(dataPeekerModelItem);
                }
                catch (Exception ex)
                {
                    DataPeekerModelItem dataPeekerModelItem = new DataPeekerModelItem(field.Name, "Cannot be accessed",
                        typeof(string), indentLevel, parent, isBackingField: false);

                    modelItems.Add(dataPeekerModelItem);
                    Debug.Log($"Error processing field {field.Name} on {obj.GetType().Name}: {ex.Message}");
                }
            }

            return modelItems;
        }

        private void ConfigureChildLoading(DataPeekerModelItem item, int remainingDepth, HashSet<object> ancestorReferences)
        {
            if (CanHaveChildren(item.Type) == false)
            {
                return;
            }

            int childDepth = Math.Max(remainingDepth, 0);
            item.SetChildrenBuilder(() =>
            {
                object currentValue = item.GetBoundValue();
                Type currentType = currentValue?.GetType() ?? item.Type;
                return BuildChildItems(currentValue, currentType, item.IndentLevel, item, childDepth, ancestorReferences);
            }, loadImmediately: remainingDepth > 0);
        }

        private List<DataPeekerModelItem> BuildChildItems(object value, Type type, int indentLevel, DataPeekerModelItem parent,
            int remainingDepth, HashSet<object> ancestorReferences)
        {
            List<DataPeekerModelItem> childItems = new List<DataPeekerModelItem>();
            if (value == null)
            {
                return childItems;
            }

            Type resolvedType = value.GetType();
            if (IsRecursiveReference(value, resolvedType, ancestorReferences))
            {
                childItems.Add(CreateRecursiveReferenceItem(indentLevel + 1, parent, resolvedType));
                return childItems;
            }

            if (typeof(IList).IsAssignableFrom(resolvedType))
            {
                return BuildListItems((IList)value, indentLevel, parent, remainingDepth, CreateAncestorReferenceSet(ancestorReferences, value));
            }

            if (typeof(IDictionary).IsAssignableFrom(resolvedType))
            {
                return BuildDictionaryItems((IDictionary)value, indentLevel, parent, remainingDepth,
                    CreateAncestorReferenceSet(ancestorReferences, value));
            }

            if (!IsSystemType(resolvedType) && !IsSimpleTypeOrNamespace(resolvedType))
            {
                return BuildModelHierarchy(value, resolvedType, indentLevel + 1, parent, remainingDepth, ancestorReferences);
            }

            return childItems;
        }

        private List<DataPeekerModelItem> BuildListItems(IList list, int indentLevel, DataPeekerModelItem parent, int remainingDepth,
            HashSet<object> ancestorReferences)
        {
            List<DataPeekerModelItem> items = new List<DataPeekerModelItem>();

            for (int i = 0; i < list.Count; i++)
            {
                int capturedIndex = i;
                object elementValue = list[capturedIndex];
                Type elementType = elementValue?.GetType() ?? GetCollectionElementType(parent.Type);

                DataPeekerModelItem item = new DataPeekerModelItem($"Item {capturedIndex}", elementValue, elementType, indentLevel + 1, parent);
                item.SetBinding(
                    () => capturedIndex < list.Count ? list[capturedIndex] : null,
                    newValue =>
                    {
                        if (capturedIndex < list.Count && list.IsReadOnly == false && list.IsFixedSize == false)
                        {
                            list[capturedIndex] = newValue;
                        }
                    });

                ConfigureChildLoading(item, remainingDepth - 1, ancestorReferences);
                items.Add(item);
            }

            return items;
        }

        private List<DataPeekerModelItem> BuildDictionaryItems(IDictionary dictionary, int indentLevel, DataPeekerModelItem parent,
            int remainingDepth, HashSet<object> ancestorReferences)
        {
            List<DataPeekerModelItem> items = new List<DataPeekerModelItem>();

            foreach (DictionaryEntry entry in dictionary)
            {
                object key = entry.Key;
                object value = entry.Value;
                Type valueType = value?.GetType() ?? GetDictionaryValueType(parent.Type);

                DataPeekerModelItem item = new DataPeekerModelItem($"{key}", value, valueType, indentLevel + 1, parent);
                item.SetBinding(
                    () => dictionary.Contains(key) ? dictionary[key] : null,
                    newValue =>
                    {
                        if (dictionary.IsReadOnly == false && dictionary.IsFixedSize == false && dictionary.Contains(key))
                        {
                            dictionary[key] = newValue;
                        }
                    });

                ConfigureChildLoading(item, remainingDepth - 1, ancestorReferences);
                items.Add(item);
            }

            return items;
        }

        private Func<object> CreateSafePropertyGetter(object target, PropertyInfo property)
        {
            bool didLogError = false;

            return () =>
            {
                try
                {
                    return property.GetValue(target);
                }
                catch (Exception ex)
                {
                    if (didLogError == false)
                    {
                        Debug.Log($"Error accessing property {property.Name} on {target.GetType().Name}: {ex.Message}");
                        didLogError = true;
                    }

                    return null;
                }
            };
        }

        private Func<object> CreateSafeFieldGetter(object target, FieldInfo field)
        {
            bool didLogError = false;

            return () =>
            {
                try
                {
                    return field.GetValue(target);
                }
                catch (Exception ex)
                {
                    if (didLogError == false)
                    {
                        Debug.Log($"Error accessing field {field.Name} on {target.GetType().Name}: {ex.Message}");
                        didLogError = true;
                    }

                    return null;
                }
            };
        }

        private HashSet<object> CreateAncestorReferenceSet(HashSet<object> source, object currentValue)
        {
            HashSet<object> references = source != null
                ? new HashSet<object>(source, ReferenceComparer)
                : new HashSet<object>(ReferenceComparer);

            if (ShouldTrackReference(currentValue))
            {
                references.Add(currentValue);
            }

            return references;
        }

        private bool IsRecursiveReference(object value, Type type, HashSet<object> ancestorReferences)
        {
            return ancestorReferences != null && ShouldTrackReference(value) && ancestorReferences.Contains(value);
        }

        private bool ShouldTrackReference(object value)
        {
            return value != null && value.GetType().IsValueType == false && value is not string;
        }

        private DataPeekerModelItem CreateRecursiveReferenceItem(int indentLevel, DataPeekerModelItem parent, Type type)
        {
            return new DataPeekerModelItem("[Recursive Reference]", type.Name, typeof(string), indentLevel, parent);
        }

        private bool CanHaveChildren(Type type)
        {
            if (type == null)
            {
                return false;
            }

            Type resolvedType = Nullable.GetUnderlyingType(type) ?? type;
            return typeof(IList).IsAssignableFrom(resolvedType) ||
                   typeof(IDictionary).IsAssignableFrom(resolvedType) ||
                   (!IsSystemType(resolvedType) && !IsSimpleTypeOrNamespace(resolvedType) &&
                    (resolvedType.IsClass || (resolvedType.IsValueType && resolvedType.IsPrimitive == false && resolvedType.IsEnum == false)));
        }

        private Type GetCollectionElementType(Type collectionType)
        {
            if (collectionType != null && collectionType.IsGenericType)
            {
                Type[] genericArguments = collectionType.GetGenericArguments();
                if (genericArguments.Length > 0)
                {
                    return genericArguments[0];
                }
            }

            return typeof(object);
        }

        private Type GetDictionaryValueType(Type dictionaryType)
        {
            if (dictionaryType != null && dictionaryType.IsGenericType)
            {
                Type[] genericArguments = dictionaryType.GetGenericArguments();
                if (genericArguments.Length > 1)
                {
                    return genericArguments[1];
                }
            }

            return typeof(object);
        }

        private bool IsSystemType(Type type)
        {
            return type.Namespace != null && (type.Namespace.StartsWith("System") || type.Namespace.StartsWith("Microsoft"));
        }

        // NEW: wraps your existing _simpleTypes and adds namespace support
        private bool IsSimpleTypeOrNamespace(Type type)
        {
            if (type == null)
                return true;

            // Treat Nullable<T> as "T" for the purpose of this check
            Type underlying = Nullable.GetUnderlyingType(type);
            if (underlying != null)
                type = underlying;

            if (_simpleTypes.Contains(type))
                return true;

            return IsTypeInSimpleNamespaces(type);
        }

        private bool IsTypeInSimpleNamespaces(Type type)
        {
            string ns = type.Namespace;
            if (string.IsNullOrEmpty(ns))
                return false;

            foreach (string simpleNs in _simpleNamespaces)
            {
                // exact namespace or any sub-namespace
                if (ns.Equals(simpleNs, StringComparison.Ordinal) ||
                    ns.StartsWith(simpleNs + ".", StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private void DisplayModelItem(DataPeekerModelItem item)
        {
            object currentValue = item.GetBoundValue();
            Type currentType = currentValue?.GetType() ?? item.Type;

            EditorGUI.indentLevel = item.IndentLevel;
            float labelHeight = EditorGUIUtility.singleLineHeight;

            // Calculate available space for the label
            float previousLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 300;

            try
            {
                Rect rect = EditorGUILayout.BeginVertical(GetRowStyle(), GUILayout.ExpandWidth(true));
                int rowIndex = _visibleRowIndex++;
                DrawRowBackground(rect, rowIndex, item);
                CacheSelectedItemRect(rect, item);
                GUILayout.Space(1f);

                // Add some content to the vertical layout before calling GetLastRect
                if (item.IsBackingField == true)
                {
                    CreateBackingFieldLabel(item);
                }

                EditorGUILayout.BeginVertical();

                if (currentValue == null || IsSimpleTypeOrNamespace(currentType))
                {
                    string displayValue = currentValue == null ? "null" : currentValue.ToString();
                    EditorGUILayout.LabelField(CreateIndentedLabel(item.IndentLevel, item.Name), displayValue, GUILayout.Height(labelHeight));

                    DrawHierarchyLines(item.IndentLevel, rect);
                    EditorGUILayout.EndVertical();
                    EditorGUILayout.EndVertical();
                    return;
                }

                if (item.Type.IsGenericType && item.Type.GetGenericTypeDefinition() == typeof(Nullable<>))
                {
                    EditorGUILayout.LabelField(CreateIndentedLabel(item.IndentLevel, item.Name), currentValue.ToString() ?? "null");
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
            finally
            {
                EditorGUIUtility.labelWidth = previousLabelWidth; // Restore the label width
            }
        }

        private void DrawItemField(DataPeekerModelItem item, float labelHeight)
        {
            // Handle different types with specific UI controls
            GUILayoutOption[] guiLayoutOption = { GUILayout.Height(labelHeight), GUILayout.MinWidth(150) };

            string indentedLabel = CreateIndentedLabel(item.IndentLevel, item.Name);
            Type currentType = item.Value?.GetType() ?? item.Type;
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
            else if (item.Type == typeof(string))
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
                item.SetBoundValue(EditorGUILayout.Vector3Field(GUIContent.none, (Vector3)item.Value, guiLayoutOption));
                EditorGUILayout.EndHorizontal();
            }
            else if (item.Type == typeof(Vector4) || item.Type == typeof(Vector4?))
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(indentedLabel, GUILayout.Height(labelHeight));
                item.SetBoundValue(EditorGUILayout.Vector4Field(GUIContent.none, (Vector4)item.Value, guiLayoutOption));
                EditorGUILayout.EndHorizontal();
            }
            else if (typeof(IList).IsAssignableFrom(currentType))
            {
                DisplayListField(item);
            }
            else if (typeof(IDictionary).IsAssignableFrom(currentType))
            {
                DisplayDictionaryField(item);
            }
            else if (CanHaveChildren(currentType))
            {
                if (IsItemExpanded(item, $"{item.Name} ({item.Type.Name})"))
                {
                    RefreshSearchAfterLazyLoad(item.EnsureChildrenLoaded());
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

        private void RefreshSearchAfterLazyLoad(bool didReloadChildren)
        {
            if (didReloadChildren && _isSearchActive)
            {
                UpdateSearchMatches(_searchTerm);
            }
        }

        private void HandleKeyboardSelection()
        {
            Event currentEvent = Event.current;
            if (currentEvent.type != EventType.KeyDown)
            {
                return;
            }

            if (IsNavigationKey(currentEvent.keyCode) == false)
            {
                return;
            }

            if (ShouldIgnoreKeyboardSelection())
            {
                return;
            }

            if (currentEvent.keyCode == KeyCode.LeftArrow)
            {
                NavigateLeft();
                currentEvent.Use();
                Repaint();
                return;
            }

            if (currentEvent.keyCode == KeyCode.RightArrow)
            {
                NavigateRight();
                currentEvent.Use();
                Repaint();
                return;
            }

            NavigateVertical(currentEvent.keyCode == KeyCode.UpArrow ? -1 : 1);
            currentEvent.Use();
            Repaint();
        }

        private bool IsNavigationKey(KeyCode keyCode)
        {
            return keyCode == KeyCode.UpArrow ||
                   keyCode == KeyCode.DownArrow ||
                   keyCode == KeyCode.LeftArrow ||
                   keyCode == KeyCode.RightArrow;
        }

        private bool ShouldIgnoreKeyboardSelection()
        {
            if (_root == null)
            {
                return true;
            }

            if (_treeHasKeyboardFocus)
            {
                return false;
            }

            return EditorGUIUtility.editingTextField ||
                   GUI.GetNameOfFocusedControl() == "SearchField";
        }

        private void SelectItem(DataPeekerModelItem item, bool scrollIntoView = false)
        {
            _selectedItem = item;
            _treeHasKeyboardFocus = true;
            _scrollSelectedItemIntoView |= scrollIntoView;
            EditorGUIUtility.editingTextField = false;
            GUI.FocusControl(null);
            GUIUtility.keyboardControl = 0;
        }

        private void NavigateVertical(int direction)
        {
            List<DataPeekerModelItem> visibleItems = GetVisibleItems();
            if (visibleItems.Count == 0)
            {
                return;
            }

            int selectedIndex = _selectedItem == null ? -1 : visibleItems.IndexOf(_selectedItem);
            int nextIndex;

            if (direction < 0)
            {
                nextIndex = selectedIndex < 0 ? visibleItems.Count - 1 : Mathf.Max(0, selectedIndex - 1);
            }
            else
            {
                nextIndex = selectedIndex < 0 ? 0 : Mathf.Min(visibleItems.Count - 1, selectedIndex + 1);
            }

            SelectItem(visibleItems[nextIndex], scrollIntoView: true);
        }

        private void NavigateLeft()
        {
            if (_selectedItem == null)
            {
                NavigateVertical(1);
                return;
            }

            if (IsExpandedForNavigation(_selectedItem))
            {
                SetExpandedForNavigation(_selectedItem, false);
                return;
            }

            if (_selectedItem.Parent != null && _selectedItem.Parent != _root)
            {
                SelectItem(_selectedItem.Parent, scrollIntoView: true);
            }
        }

        private void NavigateRight()
        {
            if (_selectedItem == null)
            {
                NavigateVertical(1);
                return;
            }

            if (CanExpandForNavigation(_selectedItem) == false)
            {
                return;
            }

            bool didReloadChildren = _selectedItem.EnsureChildrenLoaded();
            RefreshSearchAfterLazyLoad(didReloadChildren);

            if (IsExpandedForNavigation(_selectedItem) == false)
            {
                SetExpandedForNavigation(_selectedItem, true);
                return;
            }

            DataPeekerModelItem firstVisibleChild = _selectedItem.Children.FirstOrDefault(IsVisibleInCurrentSearch);
            if (firstVisibleChild != null)
            {
                SelectItem(firstVisibleChild, scrollIntoView: true);
            }
        }

        private bool CanExpandForNavigation(DataPeekerModelItem item)
        {
            if (item == null)
            {
                return false;
            }

            object currentValue = item.GetBoundValue();
            Type currentType = currentValue?.GetType() ?? item.Type;
            return item.HasPendingChildren || item.Children.Count > 0 || CanHaveChildren(currentType);
        }

        private void SetExpandedForNavigation(DataPeekerModelItem item, bool isExpanded)
        {
            if (_isSearchActive)
            {
                item.IsExpandedBySearch = isExpanded;
                return;
            }

            item.IsExpanded = isExpanded;
        }

        private List<DataPeekerModelItem> GetVisibleItems()
        {
            List<DataPeekerModelItem> visibleItems = new List<DataPeekerModelItem>();
            AddVisibleChildren(_root, visibleItems);
            return visibleItems;
        }

        private void AddVisibleChildren(DataPeekerModelItem root, List<DataPeekerModelItem> visibleItems)
        {
            if (root == null)
            {
                return;
            }

            foreach (DataPeekerModelItem child in root.Children)
            {
                if (IsVisibleInCurrentSearch(child) == false)
                {
                    continue;
                }

                visibleItems.Add(child);

                if (IsExpandedForNavigation(child))
                {
                    AddVisibleChildren(child, visibleItems);
                }
            }
        }

        private bool IsVisibleInCurrentSearch(DataPeekerModelItem item)
        {
            return _isSearchActive == false || item.MatchesSearch;
        }

        private bool IsExpandedForNavigation(DataPeekerModelItem item)
        {
            return _isSearchActive ? item.IsExpandedBySearch : item.IsExpanded;
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
            TryRecoverAfterDomainReload();
        }

        private void TryRecoverAfterDomainReload()
        {
            if (_modelType != null || string.IsNullOrEmpty(_modelTypeName))
            {
                return;
            }

            _modelType = Type.GetType(_modelTypeName);

            if (_modelType == null)
            {
                Debug.LogWarning($"Data Peeker could not resolve model type '{_modelTypeName}' after domain reload.");
                return;
            }

            openWindows[_modelType] = this;
            InitializeModelInstance();
        }

        private void OnEditorUpdate()
        {
            if (_isDefaultInstance && DataPeekerModelsListWindow.SharedContext != null)
            {
                object contextModelInstance = TryGetContextModelInstance();

                if (contextModelInstance != null)
                {
                    _modelInstance = contextModelInstance;
                    _isDefaultInstance = false;
                    RebuildTree();
                }
            }

            Repaint();
        }

        private void DisplayListField(DataPeekerModelItem item)
        {
            object itemValue = item.GetBoundValue();
            IList list = itemValue as IList;
            if (list == null)
                return;

            if (IsItemExpanded(item, $"{item.Name} (List) [{list.Count}]") == false)
            {
                return;
            }

            RefreshSearchAfterLazyLoad(item.EnsureChildrenLoaded());

            foreach (DataPeekerModelItem child in item.Children)
            {
                if (_isSearchActive == false || child.MatchesSearch == true)
                {
                    DisplayModelItem(child);
                }
            }
        }

        private void DisplayDictionaryField(DataPeekerModelItem item)
        {
            object itemValue = item.GetBoundValue();
            IDictionary dictionary = itemValue as IDictionary;
            if (dictionary == null)
                return;

            if (IsItemExpanded(item, $"{item.Name} (Dictionary) [{dictionary.Count}]") == true)
            {
                RefreshSearchAfterLazyLoad(item.EnsureChildrenLoaded());
                foreach (DataPeekerModelItem child in item.Children)
                {
                    if (_isSearchActive == false || child.MatchesSearch == true)
                    {
                        DisplayModelItem(child);
                    }
                }
            }
        }

        private bool IsItemExpanded(DataPeekerModelItem item, string label)
        {
            if (item == null)
                return false;

            // Reserve space for the foldout
            Rect itemRect = GUILayoutUtility.GetRect(18f, 18f, GUILayout.ExpandWidth(true));
            Rect foldoutRect = GetFoldoutRect(itemRect, item.IndentLevel);

            // Get triangle area manually (16x16 box on left), rest is label
            Rect triangleRect = new Rect(foldoutRect.x, foldoutRect.y, FoldoutTriangleSize, FoldoutTriangleSize);
            bool clickedLabel = itemRect.Contains(Event.current.mousePosition) && !triangleRect.Contains(Event.current.mousePosition);

            // Handle selection if clicking the label (not the arrow)
            if (Event.current.type == EventType.MouseDown && clickedLabel)
            {
                SelectItem(item);
                Repaint();
            }

            // Draw the foldout
            int previousIndentLevel = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;
            if (_isSearchActive)
            {
                item.IsExpandedBySearch = EditorGUI.Foldout(foldoutRect, item.IsExpandedBySearch, label, true);
                EditorGUI.indentLevel = previousIndentLevel;
                return item.IsExpandedBySearch;
            }

            item.IsExpanded = EditorGUI.Foldout(foldoutRect, item.IsExpanded, label, true);
            EditorGUI.indentLevel = previousIndentLevel;
            return item.IsExpanded;
        }

        private void OnDestroy()
        {
            EditorApplication.update -= OnEditorUpdate;

            if (_modelType != null &&
                openWindows != null &&
                openWindows.TryGetValue(_modelType, out DataPeekerModelWindow registeredWindow) &&
                registeredWindow == this)
            {
                openWindows.Remove(_modelType);
            }
        }

        private string CreateIndentedLabel(int indentLevel, string label)
        {
            return new string(' ', indentLevel * 2) + label;
        }

        private GUIStyle GetRowStyle()
        {
            if (_rowStyle == null)
            {
                _rowStyle = new GUIStyle(GUI.skin.box);
                _rowStyle.normal.background = null;
                _rowStyle.hover.background = null;
                _rowStyle.active.background = null;
                _rowStyle.focused.background = null;
                _rowStyle.onNormal.background = null;
                _rowStyle.onHover.background = null;
                _rowStyle.onActive.background = null;
                _rowStyle.onFocused.background = null;
            }

            return _rowStyle;
        }

        private void DrawHierarchyLines(int depth, Rect rect)
        {
            if (Event.current.type != EventType.Repaint || depth <= 0)
            {
                return;
            }

            int lineDepth = depth - 1;
            float lineX = GetHierarchyLineX(lineDepth);
            float connectorEndX = GetFoldoutX(depth) - ConnectorEndPadding;
            float connectorWidth = Mathf.Max(0f, connectorEndX - lineX);
            float rowCenterY = rect.yMin + GetRowStyle().padding.top + 1f + EditorGUIUtility.singleLineHeight * 0.5f;
            Color lineColor = GetHierarchyLineColor(lineDepth);

            EditorGUI.DrawRect(new Rect(lineX, rect.yMin, 2, rect.height), lineColor);
            EditorGUI.DrawRect(new Rect(lineX, rowCenterY, connectorWidth, 2), lineColor);
        }

        private Rect GetFoldoutRect(Rect rowRect, int depth)
        {
            float foldoutX = GetFoldoutX(depth);
            return new Rect(foldoutX, rowRect.y, Mathf.Max(0f, rowRect.xMax - foldoutX), rowRect.height);
        }

        private float GetFoldoutX(int depth)
        {
            if (depth <= 0)
            {
                return HierarchyLineOffset;
            }

            return GetHierarchyLineX(depth - 1) + FoldoutOffsetFromLine;
        }

        private float GetHierarchyLineX(int depth)
        {
            return HierarchyLineOffset + HierarchyIndentWidth * depth;
        }

        private void DrawRowBackground(Rect rect, int rowIndex, DataPeekerModelItem item)
        {
            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            EditorGUI.DrawRect(rect, GetRowBackgroundColor(rowIndex));

            if (_selectedItem == item)
            {
                DrawSelectionBackground(rect);
            }
        }

        private void CacheSelectedItemRect(Rect rect, DataPeekerModelItem item)
        {
            if (Event.current.type != EventType.Repaint || _selectedItem != item)
            {
                return;
            }

            _selectedItemRect = GetRowHeaderRect(rect);
            _didFindSelectedItemRect = true;
        }

        private void ScrollSelectedItemIntoViewIfNeeded()
        {
            if (_scrollSelectedItemIntoView == false || _didFindSelectedItemRect == false)
            {
                return;
            }

            float viewportHeight = _scrollViewRect.height > 0f ? _scrollViewRect.height : position.height;
            if (viewportHeight <= 0f)
            {
                return;
            }

            const float scrollPadding = 4f;
            float selectedTop = _selectedItemRect.yMin - scrollPadding;
            float selectedBottom = _selectedItemRect.yMax + scrollPadding;
            float viewportTop = _scrollPosition.y;
            float viewportBottom = viewportTop + viewportHeight;

            if (selectedTop < viewportTop)
            {
                _scrollPosition.y = Mathf.Max(0f, selectedTop);
                _scrollSelectedItemIntoView = false;
                Repaint();
                return;
            }

            if (selectedBottom > viewportBottom)
            {
                _scrollPosition.y = Mathf.Max(0f, selectedBottom - viewportHeight);
                _scrollSelectedItemIntoView = false;
                Repaint();
                return;
            }

            _scrollSelectedItemIntoView = false;
        }

        private void DrawSelectionBackground(Rect rect)
        {
            Rect headerRect = GetRowHeaderRect(rect);
            Color selectionColor = new Color(0.24f, 0.49f, 0.90f, 0.15f);
            EditorGUI.DrawRect(headerRect, selectionColor);
        }

        private Rect GetRowHeaderRect(Rect rect)
        {
            GUIStyle rowStyle = GetRowStyle();
            float y = rect.yMin + rowStyle.padding.top;
            float height = EditorGUIUtility.singleLineHeight + 2f;
            return new Rect(rect.xMin, y, rect.width, height);
        }

        private Color GetRowBackgroundColor(int rowIndex)
        {
            bool isEvenRow = rowIndex % 2 == 0;
            if (EditorGUIUtility.isProSkin)
            {
                return isEvenRow
                    ? new Color(0.18f, 0.18f, 0.18f, 1f)
                    : new Color(0.23f, 0.23f, 0.23f, 1f);
            }

            return isEvenRow
                ? new Color(0.88f, 0.88f, 0.88f, 1f)
                : new Color(0.95f, 0.95f, 0.95f, 1f);
        }

        private Color GetHierarchyLineColor(int depth)
        {
            return HierarchyLineColors[depth % HierarchyLineColors.Length];
        }

        private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
        {
            public new bool Equals(object x, object y)
            {
                return ReferenceEquals(x, y);
            }

            public int GetHashCode(object obj)
            {
                return obj == null ? 0 : RuntimeHelpers.GetHashCode(obj);
            }
        }
    }
}
