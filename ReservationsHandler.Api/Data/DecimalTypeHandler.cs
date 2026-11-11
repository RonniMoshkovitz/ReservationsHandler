using System.Data;
using System.Globalization;
using Dapper;

namespace ReservationsHandler.Api.Data;

/// <summary>
/// Reads <see cref="decimal"/> values that are stored as text (SQLite has no decimal type, and its REAL type is a
/// binary float that would drift on values like 0.1). Needed for reading only: Dapper cannot materialize text into
/// a decimal constructor parameter without a handler, while writes already use invariant text.
/// </summary>
public sealed class DecimalTypeHandler : SqlMapper.TypeHandler<decimal>
{
    // Required by the base class, but never called: DapperConfiguration deliberately keeps the built-in
    // decimal mapping for writes (no RemoveTypeMap), so Dapper does not route parameters through here.
    public override void SetValue(IDbDataParameter parameter, decimal value) => parameter.Value = value;

    /// <exception cref="FormatException">The text is not a valid decimal.</exception>
    /// <exception cref="InvalidCastException">The database value is not text.</exception>
    public override decimal Parse(object value) => value is string text
        ? decimal.Parse(text, NumberStyles.Number, CultureInfo.InvariantCulture)
        : throw new InvalidCastException($"Expected text for a decimal column but got {value.GetType().Name}.");
}
