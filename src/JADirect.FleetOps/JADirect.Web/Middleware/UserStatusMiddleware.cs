using JADirect.Data.Repositories;
using JADirect.Domain.Enums;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Caching.Memory;

namespace JADirect.Web.Middleware;

public class UserStatusMiddleware
{
    private readonly RequestDelegate _next;
    
    private readonly IMemoryCache _cache;
    
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(60);

    private const string CacheKeyPrefix = "user_active_";

    public UserStatusMiddleware(RequestDelegate next, IMemoryCache cache)
    {
        _next = next;
        _cache = cache;
    }


    
    public async Task InvokeAsync(HttpContext context, UserRepository userRepository)
    {
        // Verificação 1: usuário não autenticado não precisa ser verificado.
        //  Isso cobre a tela de Login e recursos estáticos (CSS, JS, imagens).
        bool userStatusAutenticated = context.User.Identity?.IsAuthenticated ?? false;

        if (!userStatusAutenticated)
        {
            await _next(context);
            return;
        }

        // Verificação 2: lê o UserId do Claim gerado no AccountController
        // durante o login. Se não existir, algo está errado com o cookie
        // e passamos para o próximo middleware sem bloquear.
        string? userIdClaimValue = context.User.FindFirst("UserId")?.Value;
        
        bool claimEstaAbsent = string.IsNullOrEmpty(userIdClaimValue);

        if (claimEstaAbsent)
        {
            await _next(context);
            return;
        }
        int userId = int.Parse(userIdClaimValue!);
        string cacheKey = $"{CacheKeyPrefix}{userId}";
        
        //Verificação 3: consulta o cache antes de ir ao banco.
        //Se o UserId estiver em cache como ativo, liberamos imediatamente.
        //Esse é o caminho mais rápido — a maioria das requisições passa por aqui.
        bool userInCache = _cache.TryGetValue(cacheKey, out bool activeUserIncache);

        if (userInCache && activeUserIncache)
        {
            await _next(context);
            return;
        }
        
        // Verificação 4: cache miss — consultamos o banco de dados.
        // GetById já existe no UserRepository e retorna o status atual.
        var userFromDatabase = userRepository.GetById(userId);

        bool userNotExistInDatabase = userFromDatabase == null;

        if (userNotExistInDatabase)
        {
            await LogoutUser(context);
            return;
        }

        bool activeUser = userFromDatabase!.Status == UserStatus.Active;

        if (activeUser)
        {
            // Usuário ativo: armazenamos no cache pelo período definido.
            // As próximas requisições deste usuário não consultarão o banco até o TTL expirar.
            _cache.Set(cacheKey, true, CacheDuration);
            await _next(context);
            return;
        }
        
        // Usuário suspenso ou cancelado: invalidamos o cookie de autenticação
        // e redirecionamos para a tela de Login imediatamente.
        // Também é removido do cache para garantir consistência.
        _cache.Remove(cacheKey);
        await LogoutUser(context);
    }

    /// <summary>
    /// Método auxiliar privado que centraliza a lógica de desconexão.
    /// Invalida o cookie de autenticação e redireciona para Login.
    /// Separado para evitar duplicação nos caminhos de erro acima.
    /// </summary>
    /// <param name="context">Contexto da requisição HTTP atual.</param>
    private async Task LogoutUser(HttpContext context)
    {
        await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        context.Response.Redirect("/Account/Login");
    }
}