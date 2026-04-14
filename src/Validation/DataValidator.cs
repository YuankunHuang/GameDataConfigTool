using GameDataTool.Core;
using GameDataTool.Models;

namespace GameDataTool.Validation;

public sealed class DataValidator
{
    private readonly List<string> _errors = new();

    /// <summary>Returns empty list on success.</summary>
    public IReadOnlyList<string> Validate(GameData data, Core.Validation rules)
    {
        _errors.Clear();

        if (rules.EnableTypeCheck)           CheckRanges(data);
        if (rules.EnforceNonNullableColumns) CheckNonNullable(data);
        CheckEnumRefs(data);
        CheckDuplicateIds(data);
        CheckFieldNames(data);
        CheckForeignKeys(data);

        return _errors;
    }

    // ─── Checks ─────────────────────────────────────────────

    private void CheckRanges(GameData data)
    {
        foreach (var t in data.Tables)
        for (var r = 0; r < t.Rows.Count; r++)
        for (var c = 0; c < t.Fields.Count && c < t.Rows[r].Values.Count; c++)
        {
            var field = t.Fields[c];
            if (!field.RangeMin.HasValue || !field.RangeMax.HasValue) continue;
            var val = t.Rows[r].Values[c];
            if (val.IsEmpty) continue;

            var num = val.AsInt();
            if (num < field.RangeMin.Value || num >= field.RangeMax.Value)
                Err(t.Name, r, c, field.Name, $"value {num} out of range [{field.RangeMin}, {field.RangeMax})");
        }
    }

    private void CheckNonNullable(GameData data)
    {
        foreach (var t in data.Tables)
        for (var r = 0; r < t.Rows.Count; r++)
        for (var c = 0; c < t.Fields.Count && c < t.Rows[r].Values.Count; c++)
        {
            var f = t.Fields[c];
            if (!f.Nullable && t.Rows[r].Values[c].Typed is null)
                Err(t.Name, r, c, f.Name, "non-nullable field is empty");
        }
    }

    private void CheckEnumRefs(GameData data)
    {
        foreach (var t in data.Tables)
        for (var c = 0; c < t.Fields.Count; c++)
        {
            var f = t.Fields[c];
            if (f.Type != FieldType.Enum) continue;

            var et = data.Enums.Find(e => e.Name.Equals(f.EnumType, StringComparison.OrdinalIgnoreCase));
            if (et is null) { _errors.Add($"[{t.Name}] field '{f.Name}': enum '{f.EnumType}' not found"); continue; }

            for (var r = 0; r < t.Rows.Count; r++)
            {
                if (c >= t.Rows[r].Values.Count) continue;
                var v = t.Rows[r].Values[c];
                if (v.IsEmpty) continue;
                if (!et.Values.Exists(ev => ev.Value == v.AsInt()))
                    Err(t.Name, r, c, f.Name, $"enum int {v.AsInt()} not in '{f.EnumType}'");
            }
        }
    }

    private void CheckDuplicateIds(GameData data)
    {
        foreach (var t in data.Tables)
        {
            var seen = new HashSet<int>();
            for (var r = 0; r < t.Rows.Count; r++)
            {
                var v = t.Rows[r].Values[0];
                if (v.IsEmpty) continue;
                if (!seen.Add(v.AsInt()))
                    Err(t.Name, r, 0, "id", $"duplicate id {v.AsInt()}");
            }
        }
    }

    private void CheckFieldNames(GameData data)
    {
        foreach (var t in data.Tables)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < t.Fields.Count; i++)
            {
                var n = t.Fields[i].Name;
                if (string.IsNullOrWhiteSpace(n))
                    _errors.Add($"[{t.Name}] col {i + 1}: empty field name");
                else if (!names.Add(n))
                    _errors.Add($"[{t.Name}] col {i + 1}: duplicate '{n}'");
                else if (n.Any(c => !char.IsLetterOrDigit(c) && c != '_'))
                    _errors.Add($"[{t.Name}] col {i + 1}: '{n}' has invalid chars");
            }
        }
    }

    private void CheckForeignKeys(GameData data)
    {
        foreach (var t in data.Tables)
        for (var c = 0; c < t.Fields.Count; c++)
        {
            var f = t.Fields[c];
            if (f.RefTable is null || f.RefField is null) continue;

            var rt = data.Tables.Find(x => x.Name.Equals(f.RefTable, StringComparison.OrdinalIgnoreCase));
            if (rt is null) { _errors.Add($"[{t.Name}] field '{f.Name}': ref table '{f.RefTable}' not found"); continue; }

            var ri = rt.Fields.FindIndex(x => x.Name.Equals(f.RefField, StringComparison.OrdinalIgnoreCase));
            if (ri < 0) { _errors.Add($"[{t.Name}] field '{f.Name}': ref field '{f.RefField}' not in '{f.RefTable}'"); continue; }

            var refSet = new HashSet<string>(rt.Rows.Select(r => ri < r.Values.Count ? r.Values[ri].Raw : ""), StringComparer.Ordinal);

            for (var r = 0; r < t.Rows.Count; r++)
            {
                if (c >= t.Rows[r].Values.Count) continue;
                var v = t.Rows[r].Values[c];
                if (v.IsEmpty) continue;
                if (!refSet.Contains(v.Raw))
                    Err(t.Name, r, c, f.Name, $"'{v.Raw}' not found in {f.RefTable}.{f.RefField}");
            }
        }
    }

    private void Err(string table, int row, int col, string field, string msg) =>
        _errors.Add($"[{table}] row {row + 2}, col {col + 1} ({field}): {msg}");
}
