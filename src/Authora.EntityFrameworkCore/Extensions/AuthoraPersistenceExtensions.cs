using Authora.Core.Abstractions;
using Authora.EntityFrameworkCore.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Authora.EntityFrameworkCore.Extensions;

/// <summary>
/// Registers EF Core implementations of Authora persistence contracts.
/// </summary>
public static class AuthoraPersistenceExtensions
{
  /// <summary>
  /// Registers the Authora EF Core stores for the application's DbContext.
  /// </summary>
  public static IServiceCollection AddAuthoraEntityFrameworkCore<TDbContext>(
    this IServiceCollection services
  )
    where TDbContext : DbContext
  {
    services.AddScoped<IAuthoraTokenStore, EfAuthoraTokenStore<TDbContext>>();
    services.AddScoped<IAuthoraRefreshStore, EfAuthoraRefreshStore<TDbContext>>();
    services.AddScoped<IAuthoraBrowserSessionStore, EfAuthoraBrowserSessionStore<TDbContext>>();

    return services;
  }
}
