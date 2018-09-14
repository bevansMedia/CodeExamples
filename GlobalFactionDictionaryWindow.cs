using System;
using System.CodeDom;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Text;
using System.Linq;

public class GlobalFactionDictionaryWindow : EditorWindow
{
    [MenuItem("SALVO/Global Faction Dictionary", priority = 21)]
    public static void Init()
    {
        var window = (GlobalFactionDictionaryWindow)GetWindow(typeof(GlobalFactionDictionaryWindow), false, "Global Faction Dictionary");
        window.Show();
    }

    private GlobalFactionDictionaryData _globalDictionary;
    private EntityData _entityData;
    private StatData _statData;
    private Vector2 _contentScrollPosition = Vector2.zero;
    private bool _showCreatePanel;
    private bool _showMissingStatsPanel;
    private bool _showFiltersPanel;
    private readonly List<StatData> _displayedStats = new List<StatData>();
    private string[] _factionNames = new string[0];
    private readonly List<FactionData> _factionData = new List<FactionData>();
    private int _factionSelection = -1;
 
    private const int TableEntryWidth = 175;
    
    private void OnGUI()
    {
        DrawFactionSelection();
        DrawCreatePanel();
        DrawMissingStatsPanel();
        DrawFiltersPanel();
        DrawDataTable();
        DrawDebugButton();
    }

    private void DrawFactionSelection()
    {
        //Find all available "FactionData" in Project
        if(_factionNames.Length <= 0)
        {
            var guids = AssetDatabase.FindAssets("t: FactionData");

            _factionNames = new string[guids.Length];

            for(var i = 0; i < guids.Length; i++)
            {
                var faction = (FactionData)AssetDatabase.LoadAssetAtPath(AssetDatabase.GUIDToAssetPath(guids[i]),typeof(FactionData));
                _factionData.Add(faction);
                _factionNames[i] = faction.factionName;
            }
        }

        //Display the Faction Selection
        var newFaction = Mathf.Max(GUILayout.Toolbar(_factionSelection, _factionNames), 0);
        
        //If it's changed since last GUI update then load the new selection
        if(newFaction != _factionSelection)
        {
            Debug.Log($"Changing Faction to: {newFaction}");
            
            //If Asset is missing, create it
            if(!_factionData[newFaction].globalFactionDictionary)
            {
                var dictionary = CreateInstance<GlobalFactionDictionaryData>();
                AssetDatabase.CreateAsset(
                     dictionary,
                    $"Assets/Data/Global Faction Data/{_factionData[newFaction].factionName} Data Dictionary.asset");

                _factionData[newFaction].globalFactionDictionary = dictionary;
                EditorUtility.SetDirty(_factionData[newFaction]);
                AssetDatabase.SaveAssets();
            }

            _globalDictionary = _factionData[newFaction].globalFactionDictionary;
            _factionSelection = newFaction;
        }
    }

    private void DrawCreatePanel()
    {
        _showCreatePanel = EditorGUILayout.Foldout(_showCreatePanel, "Create Panel");

        if (!_showCreatePanel) return;
        
        using (new GUILayout.VerticalScope("box"))
        {
            _entityData = (EntityData)EditorGUILayout.ObjectField("Entity Data", _entityData, typeof(EntityData), false);

            using (new EditorGUILayout.HorizontalScope())
            {
                _statData = (StatData)EditorGUILayout.ObjectField("Stat Data", _statData, typeof(StatData), false);

                if (GUILayout.Button("Add", GUILayout.ExpandWidth(false)))
                {
                    if(_statData && !_displayedStats.Contains(_statData))
                        _displayedStats.Add(_statData);
                }
            }           
                
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(_entityData == null || _statData == null))
                {
                    if (_entityData != null && _statData != null)
                    {
                        switch (_statData.statTypeEnum)
                        {
                            case StatTypeEnum.INT:
                                if (GUILayout.Button("Add Int value"))
                                    CreateIntEntry(_entityData.entityName, _statData.Name);
                                break;
                            case StatTypeEnum.BOOL:
                                if (GUILayout.Button("Add Bool value"))
                                    CreateBoolEntry(_entityData.entityName, _statData.Name);
                                break;
                            case StatTypeEnum.FLOAT:
                                if (GUILayout.Button("Add Float value"))
                                    CreateFloatEntry(_entityData.entityName, _statData.Name);
                                break;
                            default:
                                throw new ArgumentOutOfRangeException();
                        }
                    }                
                }
            }
        }
    }

    private void DrawMissingStatsPanel()
    {
        _showMissingStatsPanel = EditorGUILayout.Foldout(_showMissingStatsPanel, "Missing Stats");

        if (!_showMissingStatsPanel) return;
        
        using (new GUILayout.HorizontalScope())
        {
            //Cycle through Faction Buildings and add any that are missing from the Faction Data Dictionary
            if (GUILayout.Button("Fill Out Entities",GUILayout.MinWidth(150)))
            {
                foreach (var entity in _factionData[_factionSelection].factionBuildings)
                {
                    if (_globalDictionary.data.Count(o => o.entityData == entity) > 0) continue;
                    
                    Debug.LogWarning($"Missing: {entity.entityName}");
                    
                    var item = new EntityStatDictionary
                    {
                        entityData = entity,
                        statValues = new List<EntityStatValues>()
                    };
                    
                    _globalDictionary.data.Add(item);
                }
            }

            GUI.color = Color.red;
            GUILayout.Label(
                $"Entities in Dictionary: {_globalDictionary.data.Count}/{_factionData[_factionSelection].factionBuildings.Count}");
            GUI.color = Color.white;

            GUILayout.FlexibleSpace();
        }

        if(GUILayout.Button("Fill Out Stats", GUILayout.MinWidth(150), GUILayout.MaxWidth(150)))
        {
            //TODO: Functionality to automatically create StatData for any missing entry
        }
    }

    private void DrawFiltersPanel()
    {
        _showFiltersPanel = EditorGUILayout.Foldout(_showFiltersPanel, "Show Filters");

        if (!_showFiltersPanel)
            return;

        //TODO: Add Filters: String Comparison | Toggle Default Data | Entity Type Only (e.g. Buildings / Units / Research)
        GUILayout.Label("Filters go here");
    }

    private void DrawDataTable()
    {
        using (new GUILayout.HorizontalScope())
        {
            using (new GUILayout.VerticalScope())
            {
                //Display X Axis - (Stat Data Identifier)
                DrawHeaders();

                //Draw Y Axis - (Entity Names)
                //And also the Entity's data after it
                using (new GUILayout.HorizontalScope())
                {
                    using (new GUILayout.ScrollViewScope(_contentScrollPosition, 
                            GUIStyle.none,
                            GUIStyle.none,
                            GUILayout.MinWidth(TableEntryWidth + 20),
                            GUILayout.MaxWidth(TableEntryWidth + 20)))
                    {
                       DrawYAxisData();
                    }

                    DrawTableContents();
                }         
            }
        }

        GUILayout.FlexibleSpace();
    }
    
    // Draw Cell (0,0)
    private void DrawOrigin()
    {
        using (new GUILayout.VerticalScope("box"))
            GUILayout.Label(
                _globalDictionary.name,
                EditorStyles.boldLabel,
                GUILayout.ExpandWidth(true),
                GUILayout.MinWidth(TableEntryWidth),
                GUILayout.MaxWidth(TableEntryWidth));
    }

    private void DrawHeaders()
    {
        //Display X Axis - (Stat Data Names)
        using (new GUILayout.HorizontalScope())
        {
            DrawOrigin();

            //Get stat headers
            foreach (var entity in _globalDictionary.data)
            {
                foreach (var statValue in entity.statValues)
                {
                    if (!_displayedStats.Contains(statValue.statData))
                        _displayedStats.Add(statValue.statData);
                }
            }

            GUILayout.Space(4);

            using (new GUILayout.ScrollViewScope(
                _contentScrollPosition,
                GUIStyle.none,
                GUIStyle.none))
            {
                using (new GUILayout.HorizontalScope())
                {
                    //Display stat headers
                    foreach (var headerStat in _displayedStats)
                    {
                        //Display
                        using (new GUILayout.HorizontalScope("box"))
                        {
                            GUILayout.Label(
                                headerStat.Name,
                                EditorStyles.boldLabel,
                                GUILayout.MinWidth(TableEntryWidth),
                                GUILayout.MaxWidth(TableEntryWidth));
                        }
                    }
                }
            }

            GUILayout.Space(14);
            GUILayout.FlexibleSpace();
        }
    }

    private void DrawYAxisData()
    {
        using (new GUILayout.VerticalScope())
        {
            var odd = false;

            foreach (var entity in _globalDictionary.data)
            {
                GUI.backgroundColor = odd ? Color.white : Color.black;
                odd = !odd;

                using (new GUILayout.VerticalScope("box"))
                {
                    if(GUILayout.Button(
                        entity.entityData.entityName,
                        EditorStyles.label,
                        GUILayout.MinWidth(TableEntryWidth),
                        GUILayout.MaxWidth(TableEntryWidth)))
                    {
                        EditorGUIUtility.PingObject(entity.entityData);
                    }    
                }
            }

            GUILayout.Space(14);
            GUI.backgroundColor = Color.white;
        }
    }

    private void DrawTableContents()
    {
        var odd = false;
        
        //Draw table data
        using (var scrollView = new GUILayout.ScrollViewScope(_contentScrollPosition))
        {
            _contentScrollPosition = scrollView.scrollPosition;

            using (new EditorGUILayout.VerticalScope())
            {
                odd = !odd;

                //Display table data - (Various types)
                //Each data structure is a unique Entity Data
                foreach (var data in _globalDictionary.data)
                {
                    odd = !odd;

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        foreach (var stat in _displayedStats)
                        {
                            var selectedData = data.statValues.FirstOrDefault(o => o.statData == stat);

                            GUI.backgroundColor = odd ? Color.white : Color.black;

                            DrawDataEntry(data, stat, selectedData);
                        }
                    }
                }
            }
        }
    }
    
    private void DrawDataEntry(EntityStatDictionary data, StatData stat, EntityStatValues selectedData)
    {
        if (selectedData != null)
        {
            //Draw data for Entity, if available
            using (new GUILayout.HorizontalScope("box"))
            {
                GUI.backgroundColor = Color.white;

                var obj = new SerializedObject(selectedData.value);
                obj.Update();

                var serializedProp = obj.FindProperty("value");
                EditorGUILayout.PropertyField(
                    serializedProp,
                    new GUIContent(string.Empty),
                    GUILayout.MinWidth(TableEntryWidth),
                    GUILayout.MaxWidth(TableEntryWidth));

                obj.ApplyModifiedProperties();
            }
        }
        else
        {
            //No data for Entity, draw "N/A"
            using (new GUILayout.HorizontalScope(
                "box",
                GUILayout.MinWidth(TableEntryWidth + 8),
                GUILayout.MaxWidth(TableEntryWidth + 8)))
            {
                GUI.backgroundColor = Color.white;

                GUILayout.Label("N/A");

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("+", GUILayout.MaxHeight(15)))
                {
                    _entityData = data.entityData;
                    _statData = stat;

                    switch (stat.statTypeEnum)
                    {
                        case StatTypeEnum.BOOL:
                            CreateBoolEntry(data.entityData.entityName, stat.Name);
                            break;
                        case StatTypeEnum.FLOAT:
                            CreateFloatEntry(data.entityData.entityName, stat.Name);
                            break;
                        case StatTypeEnum.INT:
                            CreateIntEntry(data.entityData.entityName, stat.Name);
                            break;
                    }
                }
            }
        }
    }

    private void DrawDebugButton()
    {
        if (GUILayout.Button("Debug Log"))
        {
            var sb = new StringBuilder();

            foreach (var entity in _globalDictionary.data)
            {
                foreach (var statValue in entity.statValues)
                {
                    sb.AppendLine(
                        $"{entity.entityData.entityName} ({statValue.statData.Name}): {statValue.value}");
                }
            }

            Debug.Log(sb.ToString());
        }
    }

    #region Create Scriptable Objects

    private bool DoesEntityDataAlreadyExist(EntityData entityData)
    {
        return _globalDictionary.data.Any(o => o.entityData == entityData);
    }

    private void CreateBoolEntry(string entityDataName, string statValue)
    {
        CreateStatEntry(entityDataName, statValue, CreateInstance<StatTypeBool>());
    }

    private void CreateIntEntry(string entityDataName, string statValue)
    {
        CreateStatEntry(entityDataName, statValue, CreateInstance<StatTypeInt>());
    }

    private void CreateFloatEntry(string entityDataName, string statValue)
    {
        CreateStatEntry(entityDataName, statValue, CreateInstance<StatTypeFloat>());
    }
    
    private void CreateStatEntry(string entityDataName, string statValue, StatType statTypeValue)
    {
        Undo.RecordObject(_globalDictionary, "Add new dictionary stat");

        var statValues = new EntityStatValues {statData = _statData, value = statTypeValue};

        AssetDatabase.CreateAsset(
            statValues.value,
            $"Assets/_SALVO/Data/Stat Values/{entityDataName}_{statValue}.asset");

        EntityStatDictionary stat;

        //Create container
        if (!DoesEntityDataAlreadyExist(_entityData))
        {
            stat = new EntityStatDictionary
            {
                entityData = _entityData, statValues = new List<EntityStatValues> {statValues}
            };

            _globalDictionary.data.Add(stat);
        }
        else
        {
            //Entity Data - Already exists, just add to previous dictionaries
            stat = _globalDictionary.data.First(o => o.entityData == _entityData);
            stat.statValues.Add(statValues);
        }

        EditorUtility.SetDirty(_globalDictionary);
        AssetDatabase.SaveAssets();
    }

    #endregion
}
