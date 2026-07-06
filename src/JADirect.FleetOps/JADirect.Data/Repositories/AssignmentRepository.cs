using System.Data;
using JADirect.Data.Infrastructure;
using JADirect.Domain.Entities;
using MySql.Data.MySqlClient;

namespace JADirect.Data.Repositories;

/// <summary>
/// Repositório responsável pela persistência e consulta de driver assignments no MySQL.
/// Todas as queries usam CURDATE() para garantir isolamento por dia calendário,
/// sem depender de horário ou fuso horário da aplicação.
/// </summary>
public class AssignmentRepository
{
    private readonly DbConnectionFactory _dbConnectionFactory;

    public AssignmentRepository(DbConnectionFactory dbConnectionFactory)
    {
        _dbConnectionFactory = dbConnectionFactory;
    }

    /// <summary>
    /// Insere um novo driver assignment no banco de dados e retorna o id gerado.
    /// </summary>
    /// <param name="assignment">Entidade com os dados do assignment a ser criado.</param>
    /// <returns>O id gerado pelo banco para o novo registro.</returns>
    public int CreateAssignment(DriverAssignment assignment)
    {
        using var connection = (MySqlConnection)_dbConnectionFactory.CreateConnection();

        const string sql = @"
            INSERT INTO driver_assignments (driver_id, vehicle_id, assignment_date, created_at)
            VALUES (@DriverId, @VehicleId, @AssignmentDate, NOW());
            SELECT LAST_INSERT_ID();";

        using var command = new MySqlCommand(sql, connection);
        AddParameter(command, "@DriverId", DbType.Int32, assignment.DriverId);
        AddParameter(command, "@VehicleId", DbType.Int32, assignment.VehicleId);
        AddParameter(command, "@AssignmentDate", DbType.Date, assignment.AssignmentDate.ToDateTime(TimeOnly.MinValue));
        
        connection.Open();
        
        var generatedId = Convert.ToInt32(command.ExecuteScalar());
        return generatedId;
    }


    /// <summary>
    /// Busca o assignment ativo do motorista no dia atual.
    /// Retorna null se o motorista ainda não assumiu um veículo hoje.
    /// </summary>
    /// <param name="driverId">ID do motorista a consultar.</param>
    /// <returns>O assignment de hoje, ou null se não existir.</returns>
    public DriverAssignment? GetTodayAssignmentByDriver(int driverId)
    {
        using var connection = (MySqlConnection)_dbConnectionFactory.CreateConnection();

        const string sql = @"
            SELECT id, driver_id, vehicle_id, assignment_date, created_at
            FROM driver_assignments
            WHERE driver_id = @DriverId
              AND assignment_date = CURDATE()
            LIMIT 1";
        
        
        using var command = new MySqlCommand(sql, connection);
        AddParameter(command, "@DriverId", DbType.Int32, driverId);
        
        connection.Open();
        
        using var reader = command.ExecuteReader();
        
        if (reader.Read())
        
        {
            return MapAssignmentFromReader(reader);
        }
        return null;
    }

    /// <summary>
    /// Verifica se um veículo já possui um assignment ativo no dia atual.
    /// Usado para impedir que dois motoristas assumam o mesmo veículo no mesmo dia.
    /// </summary>
    /// <param name="vehicleId">ID do veículo a verificar.</param>
    /// <returns>True se o veículo já tiver um assignment hoje. False se estiver disponível.</returns>
    public bool ExistsActiveAssignmentForVehicle(int vehicleId)
    {
        using var connection = (MySqlConnection)_dbConnectionFactory.CreateConnection();
        
        const string sql = @"
            SELECT COUNT(1)
            FROM driver_assignments
            WHERE vehicle_id = @VehicleId
              AND assignment_date = CURDATE()";

        using var command = new MySqlCommand(sql, connection);
        AddParameter(command, "@VehicleId", DbType.Int32, vehicleId);
        
        connection.Open();
        
        var count = Convert.ToInt32(command.ExecuteScalar());
        
        return count > 0;
    }
    
    /// <summary>
    /// Remove o assignment ativo do motorista no dia atual.
    /// Permite que o motorista devolva o veículo a qualquer momento do dia,
    /// independentemente de ter realizado o walkaround check.
    /// O walkaround pertence ao veículo, não ao assignment, e permanece válido
    /// para o próximo motorista que assumir o mesmo veículo.
    /// </summary>
    /// <param name="driverId">ID do motorista que está devolvendo o veículo.</param>
    /// <returns>True se um registro foi removido. False se não havia assignment ativo.</returns>
    public bool DeleteTodayAssignment(int driverId)
    {
        using var connection = (MySqlConnection)_dbConnectionFactory.CreateConnection();

        const string sql = @"
            DELETE FROM driver_assignments
            WHERE driver_id = @DriverId
              AND assignment_date = CURDATE()";

        using var command = new MySqlCommand(sql, connection);
        AddParameter(command, "@DriverId", DbType.Int32, driverId);

        connection.Open();

        int rowsAffected = command.ExecuteNonQuery();

        return rowsAffected > 0;
    }
    

    /// <summary>
    /// Mapeia uma linha do MySqlDataReader para a entidade DriverAssignment.
    /// O banco retorna DATE como DateTime, por isso a conversão para DateOnly é necessária.
    /// </summary>
    /// <param name="reader">Reader posicionado em uma linha válida.</param>
    /// <returns>Entidade DriverAssignment preenchida com os dados da linha.</returns>
    private static DriverAssignment MapAssignmentFromReader(MySqlDataReader reader)
    {
        return new DriverAssignment
        {
            Id = reader.GetInt32("id"),
            DriverId = reader.GetInt32("driver_id"),
            VehicleId = reader.GetInt32("vehicle_id"),
            AssignmentDate = DateOnly.FromDateTime(reader.GetDateTime("assignment_date")),
            CreatedAt = reader.GetDateTime("created_at")
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