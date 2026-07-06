using System.Data;
using JADirect.Data.Infrastructure;
using JADirect.Domain.Entities;
using MySql.Data.MySqlClient;

namespace JADirect.Data.Repositories;


/// <summary>
/// Repositório responsável pela leitura dos dados de tenant no MySQL.
/// </summary>
public class TenantRepository
{
    private readonly DbConnectionFactory _connectionFactory;

    public TenantRepository(DbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    /// <summary>
    /// Retorna todos os tenants ativos na plataforma.
    /// </summary>
    public List<Tenant> GetAllActive()
    {
        var tenants = new List<Tenant>();
        using var connection = (MySqlConnection)_connectionFactory.CreateConnection();

        const string sql = @"
            SELECT id, name, whatsapp_manager_phone,
                   daily_log_deadline_hour, alert_driver_hour,
                   alert_manager_hour, timezone, is_active, created_at
            FROM tenants
            WHERE is_active = 1";

        using var command = new MySqlCommand(sql, connection);
        connection.Open();
        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            tenants.Add(MapFromReader(reader));
            
        }
        return tenants;
    }

    /// <summary>
    /// Retorna um tenant pelo identificador. Retorna null se não encontrado.
    /// </summary>
    /// <param name="tenantId">Identificador do tenant.</param>
    public Tenant? GetById(int tenantId)
    {
        using var connection = (MySqlConnection)_connectionFactory.CreateConnection();
        
        const string sql = @"
            SELECT id, name, whatsapp_manager_phone,
                   daily_log_deadline_hour, alert_driver_hour,
                   alert_manager_hour, timezone, is_active, created_at
            FROM tenants
            WHERE id = @TenantId";
        
        using var command = new MySqlCommand(sql, connection);
        AddParameter(command, "@TenantId", DbType.Int32, tenantId);
        connection.Open();
        using var reader = command.ExecuteReader();

        if (reader.Read())
        {
            return MapFromReader(reader);
        }
        return null;
    }


    private static Tenant MapFromReader(MySqlDataReader reader)
    {
        return new Tenant()
        {
            Id = Convert.ToInt32(reader["id"]),
            Name = reader["name"].ToString()!,
            WhatsappManagerPhone = reader["whatsapp_manager_phone"].ToString()!,
            DailyLogDeadlineHour = Convert.ToInt32(reader["daily_log_deadline_hour"]),
            AlertDriverHour = Convert.ToInt32(reader["alert_driver_hour"]),
            AlertManagerHour = Convert.ToInt32(reader["alert_manager_hour"]),
            Timezone = reader["timezone"].ToString()!,
            IsActive = Convert.ToBoolean(reader["is_active"]),
            CreatedAt = Convert.ToDateTime(reader["created_at"])
        };
    }

    private static void AddParameter(MySqlCommand command, string name, DbType dbType, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = dbType;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}