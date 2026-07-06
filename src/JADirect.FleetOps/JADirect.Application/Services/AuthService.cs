using JADirect.Data.Repositories;
using JADirect.Domain.Entities;
using JADirect.Domain.Enums;

namespace JADirect.Application.Services;

/// <summary>
/// Serviço responsável por orquestrar a autenticação e segurança de acesso.
/// </summary>
public class AuthService
{
    private readonly UserRepository _userRepository;

    public AuthService(UserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    /// <summary>
    /// Valida se as credenciais fornecidas são compatíveis com um usuário ativo.
    /// </summary>
    /// <param name="email">E-mail digitado no login.</param>
    /// <param name="password">Senha em texto plano digitada no login.</param>
    /// <returns>Retorna a entidade User se válido, ou null se falhar.</returns>
    public User ValidateUser(string email, string password)
    {
        //1. Busca o usuário no repositorio JADirect.Data/Repositories/UserRepository.cs
        var user = _userRepository.GetByEmail(email);

        //2. Se o usuário não exitir ou estiver com status Caceled/Suspended haverá uma falha no login
        if (user == null || user.Status == UserStatus.Canceled || user.Status == UserStatus.Suspended)
        {
            return null;
        }
        
        //3. Verifica se o hash da senha digitada bate com o que está no banco
        bool isPasswordValid = BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);

        if (isPasswordValid)
        {
            return user;
        }
        return null;
    }
    
}