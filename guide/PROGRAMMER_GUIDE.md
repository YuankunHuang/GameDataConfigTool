# 💻 Programmer Guide

## Unity Integration

### 1. Copy Generated Files to Unity Project

Copy all files from `output/code/` to your Unity project's Scripts directory:
```
Assets/Scripts/GameData/
├── Character.cs
├── Enums.cs
├── DataLoader.cs
└── GameDataManager.cs
```

Copy all files from `output/json/` to your Unity project's StreamingAssets directory:
```
Assets/StreamingAssets/GameData/
├── Character.json
├── CharacterType.json
└── ... (other data files)
```

### 2. Initialize Data at Game Startup

```csharp
// Call in GameManager or the main scene's Start method
void Start()
{
    // Initialize game data
    GameDataManager.Initialize();
}
```

### 3. Use Data in Game

```csharp
// Get all character data
Character[] allCharacters = GameDataManager.GetCharacters();

// Get a specific character by ID
Character warrior = GameDataManager.GetCharacter(1);

// Get character type enums
Dictionary<string, int> characterTypes = GameDataManager.GetCharacterTypes();

// Use character data
if (warrior != null)
{
    Debug.Log($"Character Name: {warrior.Name}");
    Debug.Log($"Character Level: {warrior.Level}");
    Debug.Log($"Character Cost: {warrior.Cost}");
}
```

### 4. Hot-Reload Data (Optional)

```csharp
// Call when you need to reload data
GameDataManager.ReloadData();
```

## 📊 Data Structures

### Character Class
```csharp
public class Character
{
    public int? ID { get; set; }        // Character ID
    public string? Name { get; set; }   // Character Name
    public int? Type { get; set; }      // Character Type
    public int? Level { get; set; }     // Level
    public float? Cost { get; set; }    // Cost
}
```

### CharacterType Enum
```csharp
public enum CharacterType
{
    Warrior = 1,
    Mage = 2
}
```

## 🔧 Adding New Data Tables

When designers add new Excel files:

1. **Regenerate Data:** Ask designers to run the generation tool
2. **Update GameDataManager:** Add new table loading in `LoadAllData()`
3. **Add Getter Methods:** Add methods to get the new data table

Example, adding an Item table:
```csharp
// In GameDataManager
public static Item[] GetItems()
{
    return GetData<Item[]>("Item");
}

// In LoadAllData()
LoadJsonData("Item");
```

## ⚡ Performance Optimization

- Data is loaded and cached at game startup
- Strong-typed access to avoid runtime errors
- Supports hot-reload for debugging and testing

## 🐛 Debugging Tips

1. **Check data loading:** See GameDataManager logs in the Console
2. **Validate data integrity:** Ensure JSON file format is correct
3. **Type safety:** Use strong-typed methods to get data, avoid null references 