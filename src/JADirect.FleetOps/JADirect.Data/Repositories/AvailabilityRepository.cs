using System.Data;
using JADirect.Data.Infrastructure;
using JADirect.Domain.Entities;
using JADirect.Domain.Enums;
using MySql.Data.MySqlClient;

namespace JADirect.Data.Repositories;

/// <summary>
/// Repositório especializado na persistência de períodos de
/// indisponibilidade de motoristas. Todos os métodos usam AddParameter
/// tipado para eliminar ambiguidade de inferência de tipo do driver MySQL
/// e proteger contra SQL injection.
/// </summary>
public class AvailabilityRepository
{
    private readonly DbConnectionFactory _connectionFactory;

    public AvailabilityRepository(DbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    /// <summary>
    /// Insere um novo período de indisponibilidade para um motorista.
    /// Novo período sempre inicia com status 'active'.
    /// </summary>
    /// <param name="tenantId">ID do tenant proprietário do registro.</param>
    /// <param name="driverId">ID do motorista que ficará indisponível.</param>
    /// <param name="status">Status durante período: OnLeave ou Sick.</param>
    /// <param name="fromDate">Primeira data indisponível.</param>
    /// <param name="toDate">Última data indisponível (inclusive).</param>
    /// <param name="reason">Motivo da indisponibilidade (ex: "Vacation", "Medical").</param>
    public void Add(int tenantId, int driverId, UserStatus status, DateTime fromDate, DateTime toDate,
        string? reason = null)
    {
        using var connection = (MySqlConnection)_connectionFactory.CreateConnection();
        const string sql = @"
            INSERT INTO driver_availability_periods
            (tenant_id, driver_id, status_during_period, availability_from_date,
             availability_to_date, reason, auto_reactivate, status, created_at, updated_at)
            VALUES
            (@TenantId, @DriverId, @Status, @FromDate,
             @ToDate, @Reason, @AutoReactivate, @PeriodStatus, NOW(), NOW())";

        using var command = new MySqlCommand(sql, connection);
        AddParameter(command, "@TenantId", DbType.Int32, tenantId);
        AddParameter(command, "@DriverId", DbType.Int32, driverId);
        AddParameter(command, "@Status", DbType.Int32, (int)status);
        AddParameter(command, "@FromDate", DbType.DateTime, fromDate.Date);
        AddParameter(command, "@ToDate", DbType.DateTime, toDate.Date);
        AddParameter(command, "@Reason", DbType.String, reason);
        AddParameter(command, "@AutoReactivate", DbType.Boolean, true);
        AddParameter(command, "@PeriodStatus", DbType.String, AvailabilityPeriodStatus.Active.ToString().ToLower());
        
        connection.Open();
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Marca um período como expirado (sua data de retorno foi atingida).
    /// Período é preservado no banco para auditoria e histórico.
    /// </summary>
    /// <param name="periodId">ID do período a marcar como expirado.</param>
    public void MarkAsExpired(int periodId)
    {
        using var connection = (MySqlConnection)_connectionFactory.CreateConnection();

        const string sql = @"
            UPDATE driver_availability_periods
            SET status = 'expired',
                updated_at = NOW()
            WHERE id = @Id";

        using var command = new MySqlCommand(sql, connection);
        AddParameter(command, "@Id", DbType.Int32, periodId);

        connection.Open();
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Marca um período como cancelado (manager cancelou antes da data de retorno).
    /// Período é preservado no banco para auditoria.
    /// </summary>
    /// <param name="periodId">ID do período a marcar como cancelado.</param>
    public void MarkAsCanceled(int periodId)
    {
        using var connection = (MySqlConnection)_connectionFactory.CreateConnection();

        const string sql = @"
            UPDATE driver_availability_periods
            SET status = 'canceled',
                updated_at = NOW()
            WHERE id = @Id";

        using var command = new MySqlCommand(sql, connection);
        AddParameter(command, "@Id", DbType.Int32, periodId);

        connection.Open();
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Verifica se um motorista está indisponível em uma data específica.
    /// Retorna o período ativo se houver, null caso contrário.
    /// Busca apenas períodos com status 'active'.
    /// </summary>
    /// <param name="driverId">ID do motorista a verificar.</param>
    /// <param name="tenantId">ID do tenant (para isolamento multi-tenant).</param>
    /// <param name="date">Data a verificar (incluídas as datas de início/fim).</param>
    /// <returns>AvailabilityPeriod se driver está indisponível, null caso contrário.</returns>
    public AvailabilityPeriod? GetActiveByDriver(int driverId, int tenantId, DateOnly date)
    {
        using var connection = (MySqlConnection)_connectionFactory.CreateConnection();

        const string sql = @"
            SELECT id, tenant_id, driver_id, status_during_period, availability_from_date,
                   availability_to_date, reason, auto_reactivate, status, created_at, updated_at
            FROM driver_availability_periods
            WHERE driver_id = @DriverId
              AND tenant_id = @TenantId
              AND availability_from_date <= @Date
              AND availability_to_date >= @Date
              AND status = 'active'
            LIMIT 1";

        using var command = new MySqlCommand(sql, connection);
        AddParameter(command, "@DriverId", DbType.Int32, driverId);
        AddParameter(command, "@TenantId", DbType.Int32, tenantId);
        AddParameter(command, "@Date", DbType.DateTime, date.ToDateTime(TimeOnly.MinValue));

        connection.Open();
        using var reader = command.ExecuteReader();

        if (reader.Read())
        {
            return MapAvailabilityPeriodFromReader((MySqlDataReader)reader);
        }

        return null;
    }

    /// <summary>
    /// Retorna todos os períodos que expiram hoje (availability_to_date = hoje).
    /// Busca apenas períodos com status 'active'.
    /// Usado por AvailabilityAutoReactivationJob para marcar como expirado.
    /// </summary>
    /// <param name="tenantId">ID do tenant.</param>
    /// <returns>Lista de períodos vencidos hoje.</returns>
    public List<AvailabilityPeriod> GetAllExpiredToday(int tenantId)
    {
        var periods = new List<AvailabilityPeriod>();
        using var connection = (MySqlConnection)_connectionFactory.CreateConnection();

        const string sql = @"
            SELECT id, tenant_id, driver_id, status_during_period,
                   availability_from_date, availability_to_date, reason,
                   auto_reactivate, status, created_at, updated_at
            FROM driver_availability_periods
            WHERE tenant_id = @TenantId
              AND DATE(availability_to_date) <= CURDATE()
              AND status = 'active'
              AND auto_reactivate = 1";

        using var command = new MySqlCommand(sql, connection);
        AddParameter(command, "@TenantId", DbType.Int32, tenantId);

        connection.Open();
        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            periods.Add(MapAvailabilityPeriodFromReader((MySqlDataReader)reader));
        }

        return periods;
    }

    /// <summary>
    /// Retorna todos os períodos que INICIAM hoje (availability_from_date = hoje).
    /// Busca apenas períodos com status 'active'.
    /// Usado por AvailabilityAutoReactivationJob para desativar drivers.
    /// </summary>
    /// <param name="tenantId">ID do tenant.</param>
    /// <returns>Lista de períodos que começam hoje.</returns>
    public List<AvailabilityPeriod> GetAllStartingToday(int tenantId)
    {
        var periods = new List<AvailabilityPeriod>();
        using var connection = (MySqlConnection)_connectionFactory.CreateConnection();

        const string sql = @"
            SELECT id, tenant_id, driver_id, status_during_period,
                   availability_from_date, availability_to_date, reason,
                   auto_reactivate, status, created_at, updated_at
            FROM driver_availability_periods
            WHERE tenant_id = @TenantId
              AND DATE(availability_from_date) = CURDATE()
              AND status = 'active'
              AND auto_reactivate = 1";

        using var command = new MySqlCommand(sql, connection);
        AddParameter(command, "@TenantId", DbType.Int32, tenantId);

        connection.Open();
        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            periods.Add(MapAvailabilityPeriodFromReader((MySqlDataReader)reader));
        }
        return periods;
    }

    /// <summary>
    /// Helper para buscar um período específico por driver e data.
    /// Busca apenas períodos com status 'active'.
    /// Usado para validações e testes.
    /// </summary>
    /// <param name="driverId">ID do motorista.</param>
    /// <param name="date">Data de referência.</param>
    /// <returns>Período se encontrado, null caso contrário.</returns>
    public AvailabilityPeriod? GetByDriverAndDate(int driverId, DateOnly date)
    {
        using var connection = (MySqlConnection)_connectionFactory.CreateConnection();

        const string sql = @"
            SELECT id, tenant_id, driver_id, status_during_period,
                   availability_from_date, availability_to_date, reason,
                   auto_reactivate, status, created_at, updated_at
            FROM driver_availability_periods
            WHERE driver_id = @DriverId
              AND availability_from_date <= @Date
              AND availability_to_date >= @Date
              AND status = 'active'
            LIMIT 1";

        using var command = new MySqlCommand(sql, connection);
        AddParameter(command, "@DriverId", DbType.Int32, driverId);
        AddParameter(command, "@Date", DbType.Date, date.ToDateTime(TimeOnly.MinValue));

        connection.Open();
        using var reader = command.ExecuteReader();

        if (reader.Read())
        {
            return MapAvailabilityPeriodFromReader((MySqlDataReader)reader);
        }

        return null;
    }
    
    /// <summary>
    /// Retorna todos os períodos com status 'active' de um motorista,
    /// incluindo períodos futuros agendados. Usado pela tela Manage
    /// para exibir a tabela de disponibilidade do motorista.
    /// </summary>
    /// <param name="driverId">ID do motorista.</param>
    /// <param name="tenantId">ID do tenant (isolamento multi-tenant).</param>
    /// <returns>Lista de períodos ativos, ordenada pela data de início.</returns>
    public List<AvailabilityPeriod> GetAllActiveByDriver(int driverId, int tenantId)
    {
        var periods = new List<AvailabilityPeriod>();
        using var connection = (MySqlConnection)_connectionFactory.CreateConnection();

        const string sql = @"
            SELECT id, tenant_id, driver_id, status_during_period,
                   availability_from_date, availability_to_date, reason,
                   auto_reactivate, status, created_at, updated_at
            FROM driver_availability_periods
            WHERE driver_id = @DriverId
              AND tenant_id = @TenantId
              AND status = 'active'
            ORDER BY availability_from_date ASC";

        using var command = new MySqlCommand(sql, connection);
        AddParameter(command, "@DriverId", DbType.Int32, driverId);
        AddParameter(command, "@TenantId", DbType.Int32, tenantId);

        connection.Open();
        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            periods.Add(MapAvailabilityPeriodFromReader((MySqlDataReader)reader));
        }

        return periods;
    }

    /// <summary>
    /// Retorna um período de indisponibilidade específico pelo seu ID.
    /// Usado para carregar dados antes de editar ou cancelar um período.
    /// </summary>
    /// <param name="periodId">ID do período.</param>
    /// <returns>Período encontrado, ou null se não existir.</returns>
    public AvailabilityPeriod? GetById(int periodId)
    {
        using var connection = (MySqlConnection)_connectionFactory.CreateConnection();

        const string sql = @"
            SELECT id, tenant_id, driver_id, status_during_period,
                   availability_from_date, availability_to_date, reason,
                   auto_reactivate, status, created_at, updated_at
            FROM driver_availability_periods
            WHERE id = @Id
            LIMIT 1";

        using var command = new MySqlCommand(sql, connection);
        AddParameter(command, "@Id", DbType.Int32, periodId);

        connection.Open();
        using var reader = command.ExecuteReader();

        if (reader.Read())
        {
            return MapAvailabilityPeriodFromReader((MySqlDataReader)reader);
        }

        return null;
    }

    /// <summary>
    /// Verifica se existe outro período ativo do mesmo motorista que se
    /// sobrepõe ao intervalo informado. Usado para validar edições e
    /// evitar dois períodos ativos conflitantes na mesma data.
    /// </summary>
    /// <param name="driverId">ID do motorista.</param>
    /// <param name="tenantId">ID do tenant.</param>
    /// <param name="fromDate">Nova data de início a validar.</param>
    /// <param name="toDate">Nova data de retorno a validar.</param>
    /// <param name="excludePeriodId">ID do período atual, para ignorá-lo na checagem (edição).</param>
    /// <returns>Período conflitante encontrado, ou null se não há sobreposição.</returns>
    public AvailabilityPeriod? GetOverlappingActivePeriod(int driverId, int tenantId, DateTime fromDate,
        DateTime toDate, int? excludePeriodId = null)
    {
        using var connection = (MySqlConnection)_connectionFactory.CreateConnection();

        const string sql = @"
            SELECT id, tenant_id, driver_id, status_during_period,
                   availability_from_date, availability_to_date, reason,
                   auto_reactivate, status, created_at, updated_at
            FROM driver_availability_periods
            WHERE driver_id = @DriverId
              AND tenant_id = @TenantId
              AND status = 'active'
              AND id != @ExcludePeriodId
              AND availability_from_date <= @ToDate
              AND availability_to_date >= @FromDate
            LIMIT 1";

        using var command = new MySqlCommand(sql, connection);
        AddParameter(command, "@DriverId", DbType.Int32, driverId);
        AddParameter(command, "@TenantId", DbType.Int32, tenantId);
        AddParameter(command, "@FromDate", DbType.DateTime, fromDate.Date);
        AddParameter(command, "@ToDate", DbType.DateTime, toDate.Date);
        AddParameter(command, "@ExcludePeriodId", DbType.Int32, excludePeriodId ?? 0);

        connection.Open();
        using var reader = command.ExecuteReader();

        if (reader.Read())
        {
            return MapAvailabilityPeriodFromReader((MySqlDataReader)reader);
        }

        return null;
    }

    /// <summary>
    /// Atualiza as datas e o motivo de um período de indisponibilidade existente.
    /// Usado pelo fluxo de edição no painel do manager.
    /// </summary>
    /// <param name="periodId">ID do período a atualizar.</param>
    /// <param name="fromDate">Nova data de início.</param>
    /// <param name="toDate">Nova data de retorno.</param>
    /// <param name="reason">Novo motivo (opcional).</param>
    public void UpdateDates(int periodId, DateTime fromDate, DateTime toDate, string? reason)
    {
        using var connection = (MySqlConnection)_connectionFactory.CreateConnection();

        const string sql = @"
            UPDATE driver_availability_periods
            SET availability_from_date = @FromDate,
                availability_to_date = @ToDate,
                reason = @Reason,
                updated_at = NOW()
            WHERE id = @Id";

        using var command = new MySqlCommand(sql, connection);
        AddParameter(command, "@FromDate", DbType.DateTime, fromDate.Date);
        AddParameter(command, "@ToDate", DbType.DateTime, toDate.Date);
        AddNullableParameter(command, "@Reason", DbType.String, reason);
        AddParameter(command, "@Id", DbType.Int32, periodId);

        connection.Open();
        command.ExecuteNonQuery();
    }
    

    /// <summary>
    /// Helper privado para mapear uma linha do reader em AvailabilityPeriod.
    /// Centraliza conversão de tipos para evitar duplicação.
    /// </summary>
    private static AvailabilityPeriod MapAvailabilityPeriodFromReader(MySqlDataReader reader)
    {
        var statusString = reader["status_during_period"].ToString() ?? "Active";
        var userStatus = Enum.TryParse<UserStatus>(statusString, out var parsedStatus)
            ? parsedStatus
            : UserStatus.Active;

        var periodStatusString = reader["status"].ToString() ?? "active";
        var periodStatus = Enum.TryParse<AvailabilityPeriodStatus>(
            char.ToUpper(periodStatusString[0]) + periodStatusString.Substring(1),
            out var parsedPeriodStatus)
            ? parsedPeriodStatus
            : AvailabilityPeriodStatus.Active;

        return new AvailabilityPeriod
        {
            Id = Convert.ToInt32(reader["id"]),
            TenantId = Convert.ToInt32(reader["tenant_id"]),
            DriverId = Convert.ToInt32(reader["driver_id"]),
            StatusDuringPeriod = userStatus,
            AvailabilityFromDate = Convert.ToDateTime(reader["availability_from_date"]),
            AvailabilityToDate = Convert.ToDateTime(reader["availability_to_date"]),
            Reason = reader["reason"] != DBNull.Value ? reader["reason"].ToString() : null,
            AutoReactivate = Convert.ToBoolean(reader["auto_reactivate"]),
            Status = periodStatus,
            CreatedAt = Convert.ToDateTime(reader["created_at"]),
            UpdatedAt = Convert.ToDateTime(reader["updated_at"])
        };
    }

    /// <summary>
    /// Adiciona um parâmetro tipado ao comando SQL, eliminando ambiguidade
    /// de inferência de tipo do driver MySQL e protegendo contra SQL injection.
    /// </summary>
    private static void AddParameter(MySqlCommand command, string name, DbType dbType, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = dbType;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    /// <summary>
    /// Adiciona um parâmetro tipado que aceita null, convertendo para DBNull quando necessário.
    /// Usado para colunas nullable como Reason.
    /// </summary>
    private static void AddNullableParameter(MySqlCommand command, string name, DbType dbType, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = dbType;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }
}