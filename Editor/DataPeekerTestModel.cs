using System.Collections.Generic;
using UnityEngine;

namespace Editor.Editor
{
    
    public class DataPeekerTestModel
    {
        public List<DataPeekTestItem> Items;

        public DataPeekerTestModel()
        {
            Items = new List<DataPeekTestItem>();
            for (int i = 0; i < 10; i++)
            {
                Items.Add(new DataPeekTestItem
                {
                    Id = "Item" + i,
                    Value = "Value" + i,
                    Amount = i * 10,
                    Position2 = new Vector2(i, i),
                    Position3 = new Vector3(i, i, i),
                    Position4 = new Vector4(i, i, i, i)
                });
            }
        }
    }
    
    public class DataPeekTestItem
    {
        public string Id;
        public string Value;
        public int Amount;
        public Vector2 Position2;
        public Vector3 Position3;
        public Vector4 Position4;
    }
}