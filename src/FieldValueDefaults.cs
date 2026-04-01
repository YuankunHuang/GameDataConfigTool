namespace GameDataTool;

/// <summary>Canonical string forms for empty nullable cells (aligned with binary export in OutputGenerator).</summary>
public static class FieldValueDefaults
{
    /// <summary>
    /// <see cref="DateTime.MinValue"/> in <c>yyyy-MM-dd HH:mm:ss</c>. Ticks are within <see cref="DateTime.MinValue"/>/<see cref="DateTime.MaxValue"/>;
    /// generated Unity <c>ReadValidDateTime</c> rejects corrupt tick values without overflow.
    /// </summary>
    /// <remarks>
    /// Not the same as SQL Server <c>datetime</c> minimum (1753-01-01). If you persist to SQL, map or use a dedicated "no date" convention.
    /// </remarks>
    public const string DateTimeMinValueIso = "0001-01-01 00:00:00";
}
