using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace PlantApp.Services;

/// <summary>
/// Bryggar den cookie-autentiserade användaren från statisk SSR (där <see cref="HttpContext"/>
/// finns) in i den interaktiva Blazor Server-kretsen (där <see cref="HttpContext"/> är null).
/// Under SSR läses principalen från <see cref="IHttpContextAccessor"/> och persisteras via
/// <see cref="PersistentComponentState"/>; i kretsen återställs den från det persisterade läget.
/// </summary>
public sealed class PersistentAuthenticationStateProvider : AuthenticationStateProvider, IDisposable
{
    private const string PersistenceKey = "PlantApp.UserInfo";
    private const string AuthenticationType = "DemoCookieAuth";

    private static readonly Task<AuthenticationState> AnonymousState =
        Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity())));

    private readonly PersistentComponentState _state;
    private readonly PersistingComponentStateSubscription? _subscription;
    private Task<AuthenticationState> _authenticationStateTask = AnonymousState;

    public PersistentAuthenticationStateProvider(
        PersistentComponentState state,
        IHttpContextAccessor httpContextAccessor)
    {
        _state = state;

        var user = httpContextAccessor.HttpContext?.User;
        if (user?.Identity is { IsAuthenticated: true })
        {
            // Statisk SSR med en autentiserad HttpContext -> använd principalen och persistera den.
            _authenticationStateTask = Task.FromResult(new AuthenticationState(user));
            _subscription = _state.RegisterOnPersisting(PersistAsync);
        }
        else if (_state.TryTakeFromJson<UserInfo>(PersistenceKey, out var userInfo) && userInfo is not null)
        {
            // Interaktiv krets -> återskapa principalen från det persisterade läget.
            _authenticationStateTask = Task.FromResult(new AuthenticationState(CreatePrincipal(userInfo)));
        }
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync() => _authenticationStateTask;

    private async Task PersistAsync()
    {
        var authenticationState = await _authenticationStateTask;
        var principal = authenticationState.User;

        if (principal.Identity?.IsAuthenticated == true)
        {
            var name = principal.FindFirst(ClaimTypes.Name)?.Value ?? principal.Identity.Name ?? string.Empty;
            var roles = principal.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray();
            _state.PersistAsJson(PersistenceKey, new UserInfo(name, roles));
        }
    }

    private static ClaimsPrincipal CreatePrincipal(UserInfo userInfo)
    {
        var claims = new List<Claim> { new(ClaimTypes.Name, userInfo.Name) };
        claims.AddRange(userInfo.Roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var identity = new ClaimsIdentity(claims, AuthenticationType, ClaimTypes.Name, ClaimTypes.Role);
        return new ClaimsPrincipal(identity);
    }

    public void Dispose() => _subscription?.Dispose();

    private sealed record UserInfo(string Name, string[] Roles);
}
