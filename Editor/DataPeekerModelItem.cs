using System;
using System.Collections.Generic;

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

    public object GetBoundValue() => _getter != null ? _getter() : Value;
    public void SetBoundValue(object newValue)
    {
        if (_setter != null)
        {
            _setter(newValue);
            Value = newValue;
        }
    }
}