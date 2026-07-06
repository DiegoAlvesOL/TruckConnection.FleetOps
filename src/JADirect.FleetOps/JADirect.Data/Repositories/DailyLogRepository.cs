// Purpose   : Repositório responsável por todas as operações de banco de dados
//             relacionadas aos logs diários de operação dos motoristas.
// Consumed by: DailyLogService (Application), DriverController (Web), ManagerController (Web)
// Layer     : Data — Repositories

using System.Data;
using JADirect.Data.Infrastructure;
using JADirect.Domain.Entities;
using JADirect.Domain.Enums;
using JADirect.Domain.Models;
using MySql.Data.MySqlClient;

namespace JADirect.Data.Repositories;

/// <summary>
/// Repositório responsável pela persistência e consulta de logs diários no MySQL.
/// Todos os métodos usam AddParameter tipado para eliminar ambiguidade de inferência
/// de tipo do driver MySQL e proteger contra SQL injection.
/// </summary>
public class DailyLogRepository
{
    private readonly DbConnectionFactory _connectionFactory;

    public DailyLogRepository(DbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    /// <summary>
    /// Insere um novo log diário no banco de dados.
    /// O assignment_id é populado automaticamente pelo DailyLogService antes desta chamada.
    /// </summary>
    /// <param name="log">Entidade com todos os dados do log já preenchidos.</param>
    public void Add(DailyLog log)
    {
        using var connection = (MySqlConnection)_connectionFactory.CreateConnection();

        const string sql = @"
            INSERT INTO daily_logs
                (log_date, user_id, vehicle_id, assignment_id, deliveries, collections,
                 returns, current_odometer, notes, created_at)
            VALUES
                (@LogDate, @UserId, @VehicleId, @AssignmentId, @Deliveries, @Collections,
                 @Returns, @CurrentOdometer, @Notes, NOW())";

        using var command = new MySqlCommand(sql, connection);
        AddParameter(command, "@LogDate", DbType.DateTime,log.LogDate);
        AddParameter(command, "@UserId", DbType.Int32,log.UserId);
        AddParameter(command, "@VehicleId", DbType.Int32,log.VehicleId);
        AddNullableParameter(command, "@AssignmentId",DbType.Int32,log.AssignmentId);
        AddParameter(command, "@Deliveries", DbType.Int32,log.Deliveries);
        AddParameter(command, "@Collections", DbType.Int32,log.Collections);
        AddParameter(command, "@Returns", DbType.Int32,log.Returns);
        AddNullableParameter(command, "@CurrentOdometer", DbType.Int32,log.CurrentOdometer);
        AddNullableParameter(command, "@Notes",DbType.String,log.Notes);

        connection.Open();
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Verifica se já existe um log registrado para o motorista na data informada.
    /// </summary>
    /// <param name="userId">ID do motorista.</param>
    /// <param name="date">Data do log a verificar.</param>
    /// <returns>True se já existir um registro. False se a data estiver livre.</returns>
    public bool HasLogForDate(int userId, DateTime date)
    {
        using var connection = (MySqlConnection)_connectionFactory.CreateConnection();

        const string sql = @"
            SELECT COUNT(*)
            FROM daily_logs
            WHERE user_id  = @UserId
              AND DATE(log_date) = DATE(@Date)";

        using var command = new MySqlCommand(sql, connection);
        AddParameter(command, "@UserId", DbType.Int32, userId);
        AddParameter(command, "@Date", DbType.DateTime, date.Date);

        connection.Open();

        var count = Convert.ToInt64(command.ExecuteScalar());

        return count > 0;
    }

    /// <summary>
    /// Retorna os logs mais recentes do motorista para exibição no feed de atividades.
    /// </summary>
    /// <param name="userId">ID do motorista.</param>
    /// <param name="limit">Número máximo de registros a retornar.</param>
    public List<RecentActivityItem> GetRecentLogs(int userId, int limit = 5)
    {
        var logs = new List<RecentActivityItem>();
        using var connection = (MySqlConnection)_connectionFactory.CreateConnection();

        const string sql = @"
            SELECT dl.log_date, v.registration_no, dl.deliveries, dl.collections, dl.returns
            FROM daily_logs dl
            INNER JOIN vehicles v ON dl.vehicle_id = v.id
            WHERE dl.user_id = @UserId
            ORDER BY dl.log_date DESC
            LIMIT @Limit";

        using var command = new MySqlCommand(sql, connection);
        AddParameter(command, "@UserId", DbType.Int32, userId);
        AddParameter(command, "@Limit",  DbType.Int32, limit);

        connection.Open();
        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            logs.Add(new RecentActivityItem
            {
                LogDate = reader.GetDateTime("log_date"),
                RegistrationNo = reader.GetString("registration_no"),
                Deliveries = reader.GetInt32("deliveries"),
                Collections = reader.GetInt32("collections"),
                Returns = reader.GetInt32("returns")
            });
        }

        return logs;
    }

    /// <summary>
    /// Retorna os totais agregados de entregas, coletas, retornos e quilometragem
    /// para o período informado. Usado pelo dashboard do manager.
    /// </summary>
    public PerformanceReportViewModel GetDashboardTotals(DateTime startDate, DateTime endDate)
    {
        var report = new PerformanceReportViewModel { StartDate = startDate, EndDate = endDate };
        using var connection = (MySqlConnection)_connectionFactory.CreateConnection();

        const string sql = @"
            SELECT
                SUM(deliveries)  AS total_deliveries,
                SUM(collections) AS total_collections,
                SUM(returns)     AS total_returns,
                (MAX(current_odometer) - MIN(current_odometer)) AS total_km
            FROM daily_logs
            WHERE log_date BETWEEN @StartDate AND @EndDate";

        using var command = new MySqlCommand(sql, connection);
        AddParameter(command, "@StartDate", DbType.DateTime, startDate.Date);
        AddParameter(command, "@EndDate",   DbType.DateTime, endDate.Date.AddDays(1).AddTicks(-1));

        connection.Open();
        using var reader = command.ExecuteReader();

        if (reader.Read())
        {
            report.TotalDeliveries = reader["total_deliveries"] != DBNull.Value ? Convert.ToInt32(reader["total_deliveries"]) : 0;
            report.TotalCollections = reader["total_collections"] != DBNull.Value ? Convert.ToInt32(reader["total_collections"]) : 0;
            report.TotalReturns = reader["total_returns"] != DBNull.Value ? Convert.ToInt32(reader["total_returns"]) : 0;
            report.TotalKmTraveled = reader["total_km"] != DBNull.Value ? Convert.ToInt32(reader["total_km"]) : 0;
        }

        return report;
    }

    /// <summary>
    /// Preenche os detalhes do dashboard do manager: ranking de motoristas e lista detalhada de logs.
    /// </summary>
    public void FillDashboardDetails(PerformanceReportViewModel report)
    {
        using var connection = (MySqlConnection)_connectionFactory.CreateConnection();
        connection.Open();

        const string sqlRanking = @"
            SELECT
                CONCAT(u.first_name, ' ', u.surname) AS driver_name,
                v.vehicle_type_id,
                v.registration_no,
                SUM(dl.deliveries)  AS total_deliveries,
                SUM(dl.collections) AS total_collections,
                SUM(dl.returns)     AS total_returns
            FROM daily_logs dl
            INNER JOIN users    u ON dl.user_id    = u.id
            INNER JOIN vehicles v ON dl.vehicle_id = v.id
            WHERE dl.log_date BETWEEN @Start AND @End
            GROUP BY u.id, v.vehicle_type_id, v.registration_no
            ORDER BY total_deliveries DESC";

        using var commandRanking = new MySqlCommand(sqlRanking, connection);
        AddParameter(commandRanking, "@Start", DbType.DateTime, report.StartDate.Date);
        AddParameter(commandRanking, "@End",   DbType.DateTime, report.EndDate.Date.AddDays(1).AddTicks(-1));

        using (var readerRanking = commandRanking.ExecuteReader())
        {
            while (readerRanking.Read())
            {
                report.DriverRanking.Add(new VehiclePerformanceSummary
                {
                    DriverName     = readerRanking["driver_name"].ToString()    ?? string.Empty,
                    VehicleType    = readerRanking["vehicle_type_id"].ToString() ?? string.Empty,
                    RegistrationNo = readerRanking["registration_no"].ToString() ?? string.Empty,
                    Deliveries     = Convert.ToInt32(readerRanking["total_deliveries"]),
                    Collections    = Convert.ToInt32(readerRanking["total_collections"]),
                    Returns        = Convert.ToInt32(readerRanking["total_returns"])
                });
            }
        }

        string sqlDetails = @"
            SELECT
                dl.log_date,
                CONCAT(u.first_name, ' ', u.surname) AS driver_name,
                v.vehicle_type_id,
                v.registration_no,
                dl.deliveries,
                dl.collections,
                dl.returns
            FROM daily_logs dl
            INNER JOIN users    u ON dl.user_id    = u.id
            INNER JOIN vehicles v ON dl.vehicle_id = v.id
            WHERE dl.log_date BETWEEN @Start AND @End";

        if (!string.IsNullOrEmpty(report.DriverSearch))
        {
            sqlDetails += " HAVING driver_name LIKE @Search";
        }

        sqlDetails += " ORDER BY dl.log_date DESC";

        using var commandDetails = new MySqlCommand(sqlDetails, connection);
        AddParameter(commandDetails, "@Start", DbType.DateTime, report.StartDate.Date);
        AddParameter(commandDetails, "@End",   DbType.DateTime, report.EndDate.Date.AddDays(1).AddTicks(-1));

        if (!string.IsNullOrEmpty(report.DriverSearch))
        {
            AddParameter(commandDetails, "@Search", DbType.String, $"%{report.DriverSearch}%");
        }

        using var readerDetails = commandDetails.ExecuteReader();

        while (readerDetails.Read())
        {
            report.DetailedLogs.Add(MapDailyLogDetailFromReader(readerDetails));
        }
    }

    /// <summary>
    /// Preenche as exceções de compliance: motoristas sem log no dia atual.
    /// </summary>
    public void FillComplianceExceptions(PerformanceReportViewModel report)
    {
        using var connection = (MySqlConnection)_connectionFactory.CreateConnection();
        connection.Open();

        const string sqlMissingLogs = @"
            SELECT u.id,                                                   
                   u.phone_number,                                         
                   CONCAT_WS(' ', u.first_name, u.surname) AS full_name
            FROM users u
            LEFT JOIN daily_logs dl
                ON u.id = dl.user_id AND DATE(dl.log_date) = CURDATE()
            WHERE u.role_id  = 2                                           
              AND u.status_id = 1
              AND dl.id IS NULL
            ORDER BY u.first_name ASC";

        using var commandLogs = new MySqlCommand(sqlMissingLogs, connection);
        using var readerLogs  = commandLogs.ExecuteReader();

        while (readerLogs.Read())
        {
            report.PendingDailyLogs.Add(new ComplianceExceptionViewModel
            {
                UserId = Convert.ToInt32(readerLogs["id"]),
                PhoneNumber = readerLogs["phone_number"].ToString() ?? string.Empty,
                DriverName = readerLogs["full_name"].ToString() ?? string.Empty,
                Message    = "No data received today",
                Severity   = "warning"
            });
        }
    }

    /// <summary>
    /// Retorna todos os veículos ativos com o nome do último motorista que realizou walkaround.
    /// Usado pelo dashboard de compliance do manager.
    /// </summary>
    public List<(Vehicle Vehicle, string LastDriver)> GetAllVehiclesForComplianceCheck()
    {
        var data = new List<(Vehicle Vehicle, string LastDriver)>();
        using var connection = (MySqlConnection)_connectionFactory.CreateConnection();

        const string sql = @"
            SELECT
                v.id, v.registration_no, v.manufacturer, v.model,
                v.vehicle_type_id, v.current_km, v.status_id, v.last_walkaround_at,
                (SELECT CONCAT(u.first_name, ' ', u.surname)
                 FROM walkaround_checks wc
                 INNER JOIN users u ON wc.user_id = u.id
                 WHERE wc.vehicle_id = v.id
                 ORDER BY wc.check_date DESC LIMIT 1) AS last_driver
            FROM vehicles v
            WHERE v.status_id != 3";

        using var command = new MySqlCommand(sql, connection);
        connection.Open();

        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            var vehicle = new Vehicle
            {
                Id               = Convert.ToInt32(reader["id"]),
                RegistrationNo   = reader["registration_no"].ToString() ?? string.Empty,
                Manufacturer     = reader["manufacturer"].ToString()    ?? string.Empty,
                Model            = reader["model"].ToString()           ?? string.Empty,
                VehicleType      = (VehicleType)Convert.ToInt32(reader["vehicle_type_id"]),
                Status           = (VehicleStatus)Convert.ToInt32(reader["status_id"]),
                LastWalkaroundAt = reader["last_walkaround_at"] != DBNull.Value
                    ? Convert.ToDateTime(reader["last_walkaround_at"])
                    : null
            };

            string driverName = reader["last_driver"] != DBNull.Value
                ? reader["last_driver"].ToString() ?? "No driver recorded"
                : "No driver recorded";

            data.Add((vehicle, driverName));
        }

        return data;
    }

    /// <summary>
    /// Helper privado para mapear uma linha do reader para DailyLogDetailItem.
    /// Centraliza o mapeamento evitando duplicação entre os métodos de leitura.
    /// </summary>
    private static DailyLogDetailItem MapDailyLogDetailFromReader(MySqlDataReader reader)
    {
        return new DailyLogDetailItem
        {
            LogDate        = Convert.ToDateTime(reader["log_date"]),
            DriverName     = reader["driver_name"].ToString()     ?? string.Empty,
            VehicleType    = reader["vehicle_type_id"].ToString() ?? string.Empty,
            RegistrationNo = reader["registration_no"].ToString() ?? string.Empty,
            Deliveries     = Convert.ToInt32(reader["deliveries"]),
            Collections    = Convert.ToInt32(reader["collections"]),
            Returns        = Convert.ToInt32(reader["returns"])
        };
    }

    /// <summary>
    /// Adiciona um parâmetro tipado ao comando SQL, eliminando ambiguidade de inferência
    /// de tipo do driver MySQL e protegendo contra SQL injection.
    /// </summary>
    private static void AddParameter(MySqlCommand command, string name, DbType dbType, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType        = dbType;
        parameter.Value         = value;
        command.Parameters.Add(parameter);
    }

    /// <summary>
    /// Adiciona um parâmetro tipado que aceita null, convertendo para DBNull quando necessário.
    /// Usado para colunas nullable como AssignmentId, CurrentOdometer e Notes.
    /// </summary>
    private static void AddNullableParameter(MySqlCommand command, string name, DbType dbType, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType        = dbType;
        parameter.Value         = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }
}