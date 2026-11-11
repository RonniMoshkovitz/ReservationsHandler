using Dapper;

namespace ReservationsHandler.Api.Data;

public static class DapperConfiguration
{
    private static int _registered;

    /// <summary>
    /// Registers the type handlers. Dapper's registry is process-wide, so this runs only once even
    /// when called repeatedly.
    /// </summary>
    public static void RegisterTypeHandlers()
    {
        if (Interlocked.Exchange(ref _registered, 1) == 1)
        {
            return;
        }

        // DateTimeOffset: needed for both directions. Microsoft.Data.Sqlite would otherwise write it as
        // "2026-08-01 00:00:00+00:00" (keeping the original offset), and Dapper only calls a handler's
        // SetValue for a built-in type after its built-in mapping is removed.
        SqlMapper.RemoveTypeMap(typeof(DateTimeOffset));
        SqlMapper.RemoveTypeMap(typeof(DateTimeOffset?));
        SqlMapper.AddTypeHandler(new DateTimeOffsetTypeHandler());

        // decimal: needed for reading only. The built-in mapping is kept, because Microsoft.Data.Sqlite already
        // writes decimals as invariant text.
        SqlMapper.AddTypeHandler(new DecimalTypeHandler());
    }
}
