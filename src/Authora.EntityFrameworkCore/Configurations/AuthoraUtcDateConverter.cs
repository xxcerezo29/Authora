using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Authora.EntityFrameworkCore.Configurations;

/// <summary>
/// Converts between UTC DateTimeOffset values and database DateTime values.
/// </summary>
public static class AuthoraUtcDateConverter
{
  /// <summary>
  /// Value converter that stores UTC values and restores them with UTC offset semantics.
  /// </summary>
  public static readonly ValueConverter<DateTimeOffset, DateTime> Instance = new(
    value => value.UtcDateTime,
    value => new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc))
  );
}
