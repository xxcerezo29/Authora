using System.Security.Claims;
using Authora.Core.Abstractions;

namespace Authora.SampleApi.Services;

public sealed class DemoSubjectResolver : IAuthoraSubjectResolver
{
  public Task<AuthoraSubject?> ResolveAsync(
    string subjectId,
    CancellationToken cancellationToken = default
  )
  {
    // Demonstration user only.
    // Replace this with your real user repository.
    if (subjectId != "demo-user")
    {
      return Task.FromResult<AuthoraSubject?>(null);
    }

    var claims = new List<Claim>
    {
      new(ClaimTypes.Name, "Demo User"),
      new(ClaimTypes.Role, "Developer"),
    };

    AuthoraSubject subject = new(Id: subjectId, Claims: claims);

    return Task.FromResult<AuthoraSubject?>(subject);
  }
}
