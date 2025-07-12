using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Linq;

namespace GameData
{
    /// <summary>
    /// Unity Game Data Manager
    /// Automatically loads and caches all game data
    /// </summary>
    public static class GameDataManager
    {
        private static Dictionary<string, object> _dataCache = new();
        private static bool _isInitialized = false;
        private static string _dataPath;

        /// <summary>
        /// Initialize the data manager
        /// Call once at game startup
        /// </summary>
        public static void Initialize()
        {
            if (_isInitialized) return;

            try
            {
                _dataPath = Path.Combine(Application.streamingAssetsPath, "GameData");
                
                // Check if data directory exists
                if (!Directory.Exists(_dataPath))
                {
                    Debug.LogError($"GameDataManager: Data directory does not exist: {_dataPath}");
                    Debug.LogError("Please make sure the generated JSON files are copied to Unity's StreamingAssets/GameData/ directory");
                    return;
                }

                // Auto-discover and load all data tables
                LoadAllData();
                _isInitialized = true;
                Debug.Log($"GameDataManager: Data loaded, total tables: {_dataCache.Count}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"GameDataManager: Data loading failed - {ex.Message}");
            }
        }

        /// <summary>
        /// Get all loaded table names
        /// </summary>
        public static string[] GetLoadedTableNames()
        {
            return _dataCache.Keys.ToArray();
        }

        /// <summary>
        /// Check if a table exists
        /// </summary>
        public static bool HasTable(string tableName)
        {
            return _dataCache.ContainsKey(tableName);
        }

        /// <summary>
        /// Generic data getter
        /// </summary>
        public static T GetData<T>(string tableName) where T : class
        {
            if (!_isInitialized)
            {
                Debug.LogWarning("GameDataManager: Please call Initialize() first");
                return null;
            }

            if (_dataCache.TryGetValue(tableName, out var data))
            {
                return data as T;
            }

            Debug.LogError($"GameDataManager: Table not found: {tableName}");
            Debug.LogError($"Available tables: {string.Join(", ", _dataCache.Keys)}");
            return null;
        }

        /// <summary>
        /// Reload all data (for hot-reload)
        /// </summary>
        public static void ReloadData()
        {
            _dataCache.Clear();
            _isInitialized = false;
            Initialize();
        }

        private static void LoadAllData()
        {
            try
            {
                // Get all JSON files
                var jsonFiles = Directory.GetFiles(_dataPath, "*.json");
                
                foreach (var jsonFile in jsonFiles)
                {
                    var tableName = Path.GetFileNameWithoutExtension(jsonFile);
                    LoadJsonData(tableName);
                }

                if (_dataCache.Count == 0)
                {
                    Debug.LogWarning("GameDataManager: No data files found");
                    Debug.LogWarning($"Please check directory: {_dataPath}");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"GameDataManager: Error loading data - {ex.Message}");
            }
        }

        private static void LoadJsonData(string tableName)
        {
            try
            {
                var jsonPath = Path.Combine(_dataPath, $"{tableName}.json");
                if (File.Exists(jsonPath))
                {
                    var json = File.ReadAllText(jsonPath);
                    
                    // Decide data type by table name
                    switch (tableName)
                    {
                        case "Character":
                            var characters = JsonSerializer.Deserialize<Character[]>(json);
                            _dataCache[tableName] = characters;
                            Debug.Log($"GameDataManager: Loaded Character data, records: {characters?.Length ?? 0}");
                            break;
                        case "CharacterType":
                            var characterTypes = JsonSerializer.Deserialize<Dictionary<string, int>>(json);
                            _dataCache[tableName] = characterTypes;
                            Debug.Log($"GameDataManager: Loaded CharacterType data, types: {characterTypes?.Count ?? 0}");
                            break;
                        default:
                            Debug.LogWarning($"GameDataManager: Unknown table type {tableName}, skipped");
                            break;
                    }
                }
                else
                {
                    Debug.LogWarning($"GameDataManager: Data file does not exist: {jsonPath}");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"GameDataManager: Failed to load table {tableName} - {ex.Message}");
            }
        }
    }
} 