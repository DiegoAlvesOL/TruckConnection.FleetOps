using System.Data;
using System.Data.Common;
using System.Globalization;
using JADirect.Data.Infrastructure;
using JADirect.Domain.Entities;
using JADirect.Domain.Enums;

namespace JADirect.Data.Repositories;


/// <summary>
/// Repositório responsável pela persistência de dados dos usuários no MySql.
/// </summary>
public class UserRepository
{
    private readonly DbConnectionFactory _connectionFactory;

    public UserRepository(DbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    /// <summary>
    /// Insere um novo usuário no banco de dados.
    /// </summary>
    /// <param name="user"></param>
    public void Add(User user)
    {
        using (var connection = _connectionFactory.CreateConnection())
        {
            const string sql =
                @"INSERT INTO users(first_name, surname, email, phone_number, password_hash, role_id, status_id, created_at) 
                VALUES(@FirstName, @Surname, @Email, @PhoneNumber, @PasswordHash, @RoleId, @StatusId, @CreatedAt)";

            var textInfo = CultureInfo.CurrentCulture.TextInfo;
            using (var command = connection.CreateCommand())
            {
                command.CommandText = sql;
                AddParameter(command, "@FirstName", textInfo.ToTitleCase(user.FirstName.Trim().ToLower()));
                AddParameter(command, "@Surname", textInfo.ToTitleCase(user.Surname.Trim().ToLower()));
                AddParameter(command, "@Email", user.Email.Trim().ToLower());
                AddParameter(command, "@PhoneNumber", user.PhoneNumber.Trim());
                AddParameter(command, "@PasswordHash", user.PasswordHash);
                AddParameter(command, "@RoleId", (int)user.Role);
                AddParameter(command, "@StatusId", (int)user.Status);
                AddParameter(command, "@CreatedAt", user.CreatedAt);

                connection.Open();
                command.ExecuteNonQuery();
            }
        }
    }

    /// <summary>
    /// Busca um usuário pelo e-mail único.
    /// </summary>
    /// <param name="email"></param>
    /// <returns></returns>
    public User GetByEmail(string email)
    {
        using (var connection = _connectionFactory.CreateConnection())
        {
            const string sql = "SELECT * FROM users WHERE email = @Email";

            using (var command = connection.CreateCommand())
            {
                command.CommandText = sql;
                AddParameter(command, "@Email", email);
                
                connection.Open();
                using (var reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        return MapUserFromReader((DbDataReader)reader);
                    }
                }
            }
            
        }

        return null;
    }

    /// <summary>
    /// Helper para adicionar parâmetros de forma segura a ideia é evitar SQL Injection.
    /// </summary>
    /// <param name="command"></param>
    /// <param name="name"></param>
    /// <param name="value"></param>
    private void AddParameter(IDbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    /// <summary>
    /// Mapeia o resultado do banco de dados (IDataReader) para a entidade de domínio User.
    /// </summary>
    /// <param name="reader"></param>
    /// <returns></returns>
    private User MapUserFromReader(DbDataReader reader)
    {
        return new User()
        {
            Id = Convert.ToInt32(reader["id"]),
            FirstName = reader["first_name"].ToString(),
            Surname = reader["surname"].ToString(),
            Email = reader["email"].ToString(),
            PhoneNumber = reader["phone_number"].ToString(),
            PasswordHash = reader["password_hash"].ToString(),
            Role = (UserRoles)Convert.ToInt32(reader["role_id"]),
            Status = (UserStatus)Convert.ToInt32(reader["status_id"]),
            CreatedAt = Convert.ToDateTime(reader["created_at"])
        };
    }

    /// <summary>
    /// Lista todos os usuário gravados no banco.
    /// </summary>
    /// <returns>Lista completa de usuários.</returns>
    public List<User> GetAll(string? search = null)
    {
        var users = new List<User>();
        using (var connection = _connectionFactory.CreateConnection())
        {

            var sql = "SELECT * FROM users WHERE 1=1";

            if (!string.IsNullOrWhiteSpace(search))
            {
                sql = sql + " AND (first_name LIKE @search OR surname LIKE @search OR email LIKE @search)";
            }
            
            sql = sql + " ORDER BY first_name ASC";
            
            using (var command = connection.CreateCommand())
            {
                command.CommandText = sql;

                if (!string.IsNullOrWhiteSpace(search))
                {
                    var parameter = command.CreateParameter();
                    parameter.ParameterName = "@search";
                    
                    parameter.Value = $"%{search}%";
                    command.Parameters.Add(parameter);
                }
                
                connection.Open();
                using (var reader = (System.Data.Common.DbDataReader)command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        users.Add(MapUserFromReader(reader));
                    }
                }
            }
        }
        return users;
    }

    /// <summary>
    /// Atualiza o status de um usuário para 'Canceled'.
    /// </summary>
    /// <param name="userId">ID do usuário a ser desativado.</param>
    public void Deactivate(int userId)
    {
        using (var connection = _connectionFactory.CreateConnection())
        {
            const string sql = "UPDATE users SET status_id = @StatusId WHERE id = @Id";
            using (var command = connection.CreateCommand())
            {
                command.CommandText = sql;
                AddParameter(command, "@StatusId", (int)UserStatus.Canceled);
                AddParameter(command, "@Id", userId);
                connection.Open();
                command.ExecuteNonQuery();
            }
        }
        
    }

    /// <summary>
    /// Atualiza o status de um usuário para 'Active'.
    /// </summary>
    /// <param name="userId"></param>
    public void Activate(int userId)
    {
        using (var connection = _connectionFactory.CreateConnection())
        {
            const string sqlActivate = "UPDATE users SET status_id = @StatusId WHERE id = @Id";
            using (var command = connection.CreateCommand())
            {
                command.CommandText = sqlActivate;
                AddParameter(command, "@StatusId", (int)UserStatus.Active);
                AddParameter(command, "@Id", userId);
                connection.Open();
                command.ExecuteNonQuery();
            }
        }
    }
    

    /// <summary>
    /// /// Busca um usuário específico utilizando a Primary Key da tabela 'users'.
    /// </summary>
    /// <param name="Id"></param>
    /// <returns></returns>
    public User GetById(int id)
    {
        using (var connection = _connectionFactory.CreateConnection())
        {
            const string sql = "SELECT * FROM  users WHERE id = @Id";

            using (var command = connection.CreateCommand())
            {
                command.CommandText = sql;
                AddParameter(command, "@Id", id);
                
                connection.Open();
                using (var reader = (System.Data.Common.DbDataReader)command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        return MapUserFromReader(reader);
                    }
                }
            }
        }
        return null;
    }

    /// <summary>
    /// Ação exclusiva do Manager. Permite alterar dados cadastrais e o cargo.
    /// O ID é usado apenas como filtro no WHERE, nunca no SET.
    /// </summary>
    /// <param name="user"></param>
    public void UpdateUserAsManager(User user)
    {
        using (var connection = _connectionFactory.CreateConnection())
        {
            const string sql = @"UPDATE users
                                 SET first_name = @FirstName,
                                     surname = @Surname,
                                     email = @Email,
                                     phone_number = @PhoneNumber,
                                     role_id = @RoleId,
                                     status_id = @StatusId
                                 WHERE id = @Id";
            using (var command = connection.CreateCommand())
            {
                command.CommandText = sql;
                AddParameter(command, "@FirstName", user.FirstName.Trim());
                AddParameter(command, "@Surname", user.Surname.Trim());
                AddParameter(command, "@Email", user.Email.Trim());
                AddParameter(command, "@PhoneNumber", user.PhoneNumber.Trim());
                AddParameter(command, "@RoleId", (int)user.Role);
                AddParameter(command, "@StatusId", (int)user.Status);
                AddParameter(command, "@Id", user.Id);
                
                connection.Open();
                command.ExecuteNonQuery();
            }
        }
    }

    
    /// <summary>
    /// /// Ação de Autonomia do Usuário. Permite alterar apenas telefone e senha.
    /// </summary>
    /// <param name="userid"></param>
    /// <param name="phoneNumber"></param>
    public void UpdateOwnProfile(int userid, string phoneNumber)
    {
        using (var connection = _connectionFactory.CreateConnection())
        {
            const string sql = "UPDATE users SET phone_number = @PhoneNumber WHERE id = @Id";
            using (var command = connection.CreateCommand())
            {
                command.CommandText = sql;
                AddParameter(command, "@PhoneNumber", phoneNumber);
                AddParameter(command, "@Id", userid);
                
                connection.Open();
                command.ExecuteNonQuery();
            }
        }
    }

    /// <summary>
    /// Sobrescreve o hash da senha de um usuário específico. Este método é chamado pelo Controller/UsersController/ChangeOwnPassword
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="newHash"></param>
    public void UpdatePassword(int userId, string newHash)
    {
        using (var connection = _connectionFactory.CreateConnection())
        {
            const string sqlNewHash = "UPDATE users SET password_hash = @NewHash WHERE id = @Id";
            using (var command = connection.CreateCommand())
            {
                command.CommandText = sqlNewHash;
                AddParameter(command, "@NewHash", newHash);
                AddParameter(command, "@Id", userId);
                
                connection.Open();
                command.ExecuteNonQuery();
            }
        }
    }
    
    /// <summary>
    /// Atualiza apenas o status de um usuário para qualquer UserStatus válido.
    /// Usado pelo AvailabilityAutoReactivationJob para marcar drivers como OnLeave/Sick.
    /// </summary>
    /// <param name="userId">ID do usuário a alterar.</param>
    /// <param name="status">Novo status (Active, OnLeave, Sick, Suspended, Canceled).</param>
    public void UpdateStatus(int userId, UserStatus status)
    {
        using (var connection = _connectionFactory.CreateConnection())
        {
            const string sql = "UPDATE users SET status_id = @StatusId WHERE id = @Id";
            using (var command = connection.CreateCommand())
            {
                command.CommandText = sql;
                AddParameter(command, "@StatusId", (int)status);
                AddParameter(command, "@Id", userId);
            
                connection.Open();
                command.ExecuteNonQuery();
            }
        }
    }
    
}