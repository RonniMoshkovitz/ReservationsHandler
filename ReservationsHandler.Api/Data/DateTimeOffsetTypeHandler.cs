using System.Data;
using System.Globalization;
using Dapper;

namespace ReservationsHandler.Api.Data;

/// <summary>
/// Stores <see cref="DateTimeOffset"/> as fixed-width ISO-8601 UTC text, e.g. <c>2026-08-01T00:00:00.0000000Z</c>.
/// The fixed width and single offset make the text sort and compare chronologically.
/// </summary>
public sealed class DateTimeOffsetTypeHandler : SqlMapper.TypeHandler<DateTimeOffset>
{
    public const string Format = "yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'";

    public override void SetValue(IDbDataParameter parameter, DateTimeOffset value)
    {
        parameter.DbType = DbType.String;
        parameter.Value = value.UtcDateTime.ToString(Format, CultureInfo.InvariantCulture);
    }

    /// <exception cref="FormatException">The text is not in <see cref="Format"/>.</exception>
    /// <exception cref="InvalidCastException">The database value is not text.</exception>
    public override DateTimeOffset Parse(object value)
    {
        if (value is not string text)
        {
            throw new InvalidCastException(
                $"Expected ISO-8601 text for a DateTimeOffset column but got {value.GetType().Name}.");
        }

        return DateTimeOffset.ParseExact(
            text,
            Format,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
    }
}
