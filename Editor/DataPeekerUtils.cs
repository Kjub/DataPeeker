namespace Editor.Editor
{
    public static class DataPeekerUtils
    {
        public static string GetTestJSON()
        {
            return
             @"
{
""Data"": {
""jobs"": [
    {
      ""definitionId"": ""lc_1930_col_raw_coal"",
      ""materialProgress"": {
        ""definitionId"": ""coal"",
        ""totalAmount"": -1,
        ""currentAmount"": -1
      },
      ""createdAt"": ""31.03.2025 07:41:25"",
      ""fuelCost"": 18,
      ""speedUpFuelCost"": 9,
      ""durabilityCost"": 9
    },
    {
      ""definitionId"": ""lc_1930_col_raw_iron"",
      ""materialProgress"": {
        ""definitionId"": ""iron_ore"",
        ""totalAmount"": -1,
        ""currentAmount"": -1
      },
      ""createdAt"": ""31.03.2025 08:10:02"",
      ""fuelCost"": 18,
      ""speedUpFuelCost"": 9,
      ""durabilityCost"": 9
    },
    {
      ""definitionId"": ""lc_1930_oak_farm_rail_3"",
      ""materialProgress"": {
        ""definitionId"": ""hand_tools"",
        ""totalAmount"": 330,
        ""currentAmount"": 220
      },
      ""rewards"": {
        ""items"": [
          {
            ""id"": 1,
            ""value"": ""money"",
            ""amount"": 2500
          },
          {
            ""id"": 1,
            ""value"": ""xp"",
            ""amount"": 100
          },
          {
            ""id"": 1,
            ""value"": ""level_point"",
            ""amount"": 8
          },
          {
            ""id"": 1,
            ""value"": ""map_point"",
            ""amount"": 1
          },
          {
            ""id"": 9,
            ""value"": ""hh_1930_oak_moo_key""
          },
          {
            ""id"": 19,
            ""value"": ""lc_1930_oak_farm_rail"",
            ""amount"": 1
          }
        ]
      },
      ""createdAt"": ""01.04.2025 17:44:33"",
      ""fuelCost"": 22,
      ""speedUpFuelCost"": 11,
      ""durabilityCost"": 11
    },
    {
      ""definitionId"": ""lc_1930_oak_raw_oakwood"",
      ""materialProgress"": {
        ""definitionId"": ""oak_wood"",
        ""totalAmount"": -1,
        ""currentAmount"": -1
      },
      ""createdAt"": ""31.03.2025 07:34:22"",
      ""fuelCost"": 18,
      ""speedUpFuelCost"": 9,
      ""durabilityCost"": 9
    },
    {
      ""definitionId"": ""lc_1930_oak_raw_oakwood_tutorial"",
      ""materialProgress"": {
        ""definitionId"": ""oak_wood"",
        ""totalAmount"": -1,
        ""currentAmount"": -1
      },
      ""createdAt"": ""31.03.2025 07:34:22"",
      ""fuelCost"": 10,
      ""speedUpFuelCost"": 5
    },
    {
      ""definitionId"": ""lc_1930_pit_passenger_8"",
      ""materialProgress"": {
        ""definitionId"": ""passenger"",
        ""totalAmount"": 380,
        ""currentAmount"": 111
      },
      ""rewards"": {
        ""items"": [
          {
            ""id"": 1,
            ""value"": ""money"",
            ""amount"": 24600
          },
          {
            ""id"": 1,
            ""value"": ""level_point"",
            ""amount"": 5
          },
          {
            ""id"": 1,
            ""value"": ""map_point"",
            ""amount"": 1
          }
        ]
      },
      ""createdAt"": ""01.04.2025 16:32:35"",
      ""fuelCost"": 9,
      ""speedUpFuelCost"": 9,
      ""durabilityCost"": 11
    },
    {
      ""definitionId"": ""lc_e01_boulder_city_4"",
      ""materialProgress"": {
        ""definitionId"": ""steel_beam"",
        ""totalAmount"": 105,
        ""currentAmount"": 105
      },
      ""rewards"": {
        ""items"": [
          {
            ""id"": 1,
            ""value"": ""money"",
            ""amount"": 4300
          },
          {
            ""id"": 1,
            ""value"": ""event_point"",
            ""amount"": 90
          },
          {
            ""id"": 1,
            ""value"": ""level_point"",
            ""amount"": 7
          }
        ]
      },
      ""createdAt"": ""01.04.2025 17:56:50"",
      ""fuelCost"": 18,
      ""speedUpFuelCost"": 9,
      ""durabilityCost"": 11
    },
    {
      ""definitionId"": ""lc_e01_dam_construction_9"",
      ""materialProgress"": {
        ""definitionId"": ""material_e01_concrete"",
        ""totalAmount"": 195,
        ""currentAmount"": 85
      },
      ""rewards"": {
        ""items"": [
          {
            ""id"": 1,
            ""value"": ""money"",
            ""amount"": 6300
          },
          {
            ""id"": 1,
            ""value"": ""event_point"",
            ""amount"": 130
          },
          {
            ""id"": 1,
            ""value"": ""level_point"",
            ""amount"": 11
          }
        ]
      },
      ""createdAt"": ""01.04.2025 17:49:33"",
      ""fuelCost"": 18,
      ""speedUpFuelCost"": 9,
      ""durabilityCost"": 11
    },
    {
      ""definitionId"": ""lc_e01_hydropower_plant_6"",
      ""materialProgress"": {
        ""definitionId"": ""steel_rope"",
        ""totalAmount"": 150,
        ""currentAmount"": 100
      },
      ""rewards"": {
        ""items"": [
          {
            ""id"": 1,
            ""value"": ""money"",
            ""amount"": 5100
          },
          {
            ""id"": 1,
            ""value"": ""event_point"",
            ""amount"": 105
          },
          {
            ""id"": 1,
            ""value"": ""level_point"",
            ""amount"": 9
          }
        ]
      },
      ""createdAt"": ""01.04.2025 17:45:56"",
      ""fuelCost"": 17,
      ""speedUpFuelCost"": 9,
      ""durabilityCost"": 10
    },
    {
      ""definitionId"": ""lc_e01_passenger_1"",
      ""materialProgress"": {
        ""definitionId"": ""passenger"",
        ""totalAmount"": 410,
        ""currentAmount"": 310
      },
      ""rewards"": {
        ""items"": [
          {
            ""id"": 1,
            ""value"": ""money"",
            ""amount"": 16900
          },
          {
            ""id"": 1,
            ""value"": ""event_point"",
            ""amount"": 70
          },
          {
            ""id"": 1,
            ""value"": ""level_point"",
            ""amount"": 6
          }
        ]
      },
      ""createdAt"": ""01.04.2025 17:27:04"",
      ""fuelCost"": 9,
      ""speedUpFuelCost"": 9,
      ""durabilityCost"": 11
    },
    {
      ""definitionId"": ""lc_e01_raw_cement"",
      ""materialProgress"": {
        ""definitionId"": ""material_e01_cement"",
        ""totalAmount"": -1,
        ""currentAmount"": -1
      },
      ""createdAt"": ""31.03.2025 17:50:32"",
      ""fuelCost"": 18,
      ""speedUpFuelCost"": 9,
      ""durabilityCost"": 11
    },
    {
      ""definitionId"": ""lc_e01_raw_copper"",
      ""materialProgress"": {
        ""definitionId"": ""material_e01_copper"",
        ""totalAmount"": -1,
        ""currentAmount"": -1
      },
      ""createdAt"": ""31.03.2025 17:49:57"",
      ""fuelCost"": 18,
      ""speedUpFuelCost"": 9,
      ""durabilityCost"": 11
    }
  ]
}
}";

        }
    }
}