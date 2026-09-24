namespace Authora.Tests.TestSupport;

internal sealed class TestTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
  public DateTimeOffset UtcNow { get; set; } = utcNow;

  public override DateTimeOffset GetUtcNow() => UtcNow;
}
