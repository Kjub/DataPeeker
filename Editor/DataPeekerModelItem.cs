using System;
using System.Collections;
using System.Collections.Generic;

namespace Kjub.DataPeeker.Editor
{
    public class DataPeekerModelItem
    {
        public string Name { get; set; }
        public object Value { get; set; }
        public Type Type { get; set; }
        public int IndentLevel { get; set; }
        public List<DataPeekerModelItem> Children { get; set; }
        public bool IsExpanded { get; set; }
        public bool IsExpandedBySearch { get; set; }
        public bool MatchesSearch { get; set; }
        public DataPeekerModelItem Parent { get; set; }
        public bool IsBackingField { get; set; }  // Track if this item is a backing field
        
        private Func<object> _getter;
        private Action<object> _setter;
        private Func<List<DataPeekerModelItem>> _childrenBuilder;
        private object _lastChildrenSource;
        private int? _lastCollectionCount;


    // Default constructor
        public DataPeekerModelItem(string name, object value, Type type, int indentLevel, DataPeekerModelItem parent = null, bool isBackingField = false)
        {
            Name = name;
            Value = value;
            Type = type;
            IndentLevel = indentLevel;
            Children = new List<DataPeekerModelItem>();
            IsExpanded = false;
            MatchesSearch = true; // Default to true to show all items when no search is applied
            Parent = parent;
            IsBackingField = isBackingField; // Initialize backing field status
        }

    // Special constructor for the root node
        public DataPeekerModelItem()
        {
            Name = "Root";
            Children = new List<DataPeekerModelItem>();
            IsExpanded = true; // Root is always expanded
            MatchesSearch = true; // Root should always match search to show children
            IndentLevel = -1; // Root level (not visible)
            IsBackingField = false; // Root is not a backing field
        }
    
        public void SetBinding(Func<object> getter, Action<object> setter)
        {
            _getter = getter;
            _setter = setter;
        }

        public bool HasPendingChildren => _childrenBuilder != null;
        public bool AreChildrenLoaded { get; private set; }

        public object GetBoundValue()
        {
            Value = _getter != null ? _getter() : Value;
            return Value;
        }

        public void SetBoundValue(object newValue)
        {
            if (_setter != null)
            {
                _setter(newValue);
                Value = newValue;
            }
        }

        public void SetChildrenBuilder(Func<List<DataPeekerModelItem>> childrenBuilder, bool loadImmediately = false)
        {
            _childrenBuilder = childrenBuilder;

            if (loadImmediately)
            {
                EnsureChildrenLoaded(forceRefresh: true);
            }
        }

        public bool EnsureChildrenLoaded(bool forceRefresh = false)
        {
            if (_childrenBuilder == null)
            {
                return false;
            }

            object currentValue = GetBoundValue();
            bool sourceChanged = IsTrackedReference(currentValue) && !ReferenceEquals(_lastChildrenSource, currentValue);
            bool collectionChanged = TryGetCollectionCount(currentValue, out int collectionCount) &&
                (_lastCollectionCount.HasValue == false || _lastCollectionCount.Value != collectionCount);

            if (AreChildrenLoaded && forceRefresh == false && sourceChanged == false && collectionChanged == false)
            {
                return false;
            }

            Children = _childrenBuilder() ?? new List<DataPeekerModelItem>();
            AreChildrenLoaded = true;
            _lastChildrenSource = IsTrackedReference(currentValue) ? currentValue : null;
            _lastCollectionCount = TryGetCollectionCount(currentValue, out collectionCount) ? collectionCount : null;
            return true;
        }

        private static bool IsTrackedReference(object value)
        {
            return value != null && value.GetType().IsValueType == false && value is not string;
        }

        private static bool TryGetCollectionCount(object value, out int count)
        {
            switch (value)
            {
                case IList list:
                    count = list.Count;
                    return true;
                case IDictionary dictionary:
                    count = dictionary.Count;
                    return true;
                default:
                    count = 0;
                    return false;
            }
        }
    }
}
