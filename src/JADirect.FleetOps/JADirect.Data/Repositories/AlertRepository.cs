using System.Data;
using JADirect.Data.Infrastructure;
using JADirect.Domain.Enums;
using JADirect.Domain.Models;
using MySql.Data.MySqlClient;

namespace JADirect.Data.Repositories;

/// <summary>
/// Repositório responsável pelas queries de conformidade de Daily Log.
/// </summary>
public class AlertRepository
{
    private readonly DbConnectionFactory _connectionFactory;
 
    public AlertRepository(DbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }
 
    /// <summary>
    /// Retorna motoristas com status Active que não registraram Daily Log na data informada.
    /// Filtra apenas usuários ativos (status_id = 1) com número de telefone válido.
    /// Nota: Indisponibilidade (férias/doença) é gerenciada via sincronização automática de status
    /// no AvailabilityAutoReactivationJob, que altera users.status quando períodos começam/terminam.
    /// </summary>
    /// <param name="tenantId">Identificador do tenant.</param>
    /// <param name="date">Data a verificar.</param>
    public List<PendingDriverAlert> GetDriversPendingDailyLog(int tenantId, DateOnly date)
    {
        var pendingDrivers = new List<PendingDriverAlert>();
        using var connection = (MySqlConnection)_connectionFactory.CreateConnection();
 
        const string sql = @"
            SELECT u.id,
                   u.first_name,
                   u.surname,
                   u.role_id,
                   u.phone_number
            FROM users u
            WHERE u.status_id = 1
                AND u.role_id = @VanDriverRole
                AND u.phone_number IS NOT NULL
                AND u.phone_number != ''
                AND u.phone_number != '00000000'
                AND NOT EXISTS(
                    SELECT 1
                    FROM daily_logs dl
                    WHERE dl.user_id = u.id
                        AND DATE(dl.log_date) = @Date
                )";
 
        using var command = new MySqlCommand(sql, connection);
        AddParameter(command, "@TenantId", DbType.Int32, tenantId);
        AddParameter(command, "@Date", DbType.Date, date.ToDateTime(TimeOnly.MinValue));
        AddParameter(command, "@VanDriverRole", DbType.Int32, (int)UserRoles.VanDriver);
        connection.Open();
        using var reader = command.ExecuteReader();
 
        while (reader.Read())
        {
            pendingDrivers.Add(new PendingDriverAlert
            {
                UserId = Convert.ToInt32(reader["id"]),
                FirstName = reader["first_name"].ToString()!,
                Surname = reader["surname"].ToString()!,
                Role = (UserRoles)Convert.ToInt32(reader["role_id"]),
                PhoneNumber = reader["phone_number"].ToString()!
            });
        }
        return pendingDrivers;
    }
 
 
    /// <summary>
    /// Verifica se o motorista iniciou conversa com o número da empresa nas últimas 24 horas.
    /// Sessão ativa permite envio gratuito sem consumo de template pago.
    /// </summary>
    /// <param name="userId">Identificador do motorista.</param>
    public bool HasActiveSession(int userId)
    {
        using var connection = (MySqlConnection)_connectionFactory.CreateConnection();
        const string sql = @"
            SELECT COUNT(1)
            FROM whatsapp_sessions
            WHERE user_id = @UserId
                AND expires_at > @Now";
 
        using var command = new MySqlCommand(sql, connection);
        AddParameter(command, "@UserId", DbType.Int32, userId);
        AddParameter(command, "@Now", DbType.DateTime, DateTime.UtcNow);
        connection.Open();
        
        return Convert.ToInt32(command.ExecuteScalar()) > 0;
    }
 
 
    private void AddParameter(MySqlCommand command, string name, DbType dbType, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = dbType;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
    
}