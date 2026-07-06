using System.Data;
using System.Data.Common;
using System.Globalization;
using JADirect.Data.Infrastructure;
using JADirect.Domain.Entities;
using JADirect.Domain.Enums;

namespace JADirect.Data.Repositories;


/// <summary>
/// Repositório responsável pela persistência de dados dos veículos no MySql.
/// </summary>
public class VehicleRepository
{
    private readonly DbConnectionFactory _connectionFactory;

    public VehicleRepository(DbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    /// <summary>
    /// Insere um novo veículo no banco de dados.
    /// </summary>
    /// <param name="vehicle"></param>
    public void Add(Vehicle vehicle)
    {
        using (var connection = _connectionFactory.CreateConnection())
        {
            const string sql = @"INSERT INTO vehicles(registration_no, manufacturer, model, vehicle_type_id, current_km, status_id, created_at, last_walkaround_at)
                            VALUES(@RegistrationNo, @Manufacturer, @Model, @VehicleTypeId, @CurrentKm, @StatusId, @CreatedAt, @LastWalkaroundAt)";
            
            var textInfo = CultureInfo.CurrentCulture.TextInfo;
            using (var command = connection.CreateCommand())
            {
                command.CommandText = sql;
                AddParameter(command, "@RegistrationNo", vehicle.RegistrationNo.Trim().ToUpper());
                AddParameter(command, "@Manufacturer", textInfo.ToTitleCase(vehicle.Manufacturer.Trim().ToLower()));
                AddParameter(command, "@Model", textInfo.ToTitleCase(vehicle.Model.Trim().ToLower()));
                AddParameter(command, "@VehicleTypeId", (int)vehicle.VehicleType);
                AddParameter(command, "@CurrentKm", vehicle.CurrentKm);
                AddParameter(command, "@StatusId", (int)vehicle.Status);
                AddParameter(command, "@CreatedAt", vehicle.CreatedAt);
                AddParameter(command, "@LastWalkaroundAt", vehicle.LastWalkaroundAt);
                
                connection.Open();
                command.ExecuteNonQuery();
            }
        }
    }

    /// <summary>
    /// Lista todos os veículos gravados no banco.
    /// </summary>
    /// <returns>A Lista completa de veículos.</returns>
    public List<Vehicle> GetAll()
    {
        var vehicles = new List<Vehicle>();
        using (var connection = _connectionFactory.CreateConnection())
        {
            const string sql = "SELECT * FROM vehicles ORDER BY registration_no ASC";
            using (var command = connection.CreateCommand())
            {
                command.CommandText = sql;
                connection.Open();
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        vehicles.Add(MapVehicleFromReader((DbDataReader)reader));
                    }
                }
            }

        }
        return vehicles;
    }
    
    
    /// <summary>
    /// Versão da função GetAll mas com filtro de Busca no argumento se Não tiver nada chama a função GetAll tradicional.. 
    /// Lista veículos filtrando por placa ou modelo.
    /// </summary>
    public List<Vehicle> GetAllFilter(string? search)
    {
        // Se não houver busca, chama o GetAll original que já existe
        if (string.IsNullOrWhiteSpace(search))
        {
            return GetAll();
        }

        var vehicles = new List<Vehicle>();
        using (var connection = _connectionFactory.CreateConnection())
        {
            // SQL com filtro LIKE para placa, fabricante ou modelo
            const string sql = @"SELECT * FROM vehicles 
                             WHERE registration_no LIKE @search 
                                OR manufacturer LIKE @search 
                                OR model LIKE @search 
                             ORDER BY registration_no ASC";

            using (var command = connection.CreateCommand())
            {
                command.CommandText = sql;
            
                var parameter = command.CreateParameter();
                parameter.ParameterName = "@search";
                parameter.Value = $"%{search}%";
                command.Parameters.Add(parameter);

                connection.Open();
                using (var reader = (DbDataReader)command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        vehicles.Add(MapVehicleFromReader(reader));
                    }
                }
            }
        }
        return vehicles;
    }

    /// <summary>
    /// Verifica se um veículo com a mesma placa já existe.
    /// </summary>
    /// <param name="registrationNo"></param>
    /// <returns></returns>
    public bool Exists(string registrationNo)
    {
        using (var connection = _connectionFactory.CreateConnection())
        {
            const string sql = "SELECT COUNT(1) FROM vehicles WHERE registration_no = @RegistrationNo";
            using (var command = connection.CreateCommand())
            {
                command.CommandText = sql;
                AddParameter(command, "@RegistrationNo", registrationNo.Trim().ToUpper());
                
                connection.Open();
                return Convert.ToInt32(command.ExecuteScalar()) > 0;
            }
        }
    }

    /// <summary>
    /// Helper para adicionar parâmetros de forma segura evitando SQL Injection.
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
    /// Mapeia o resultado do banco de dados (DbDataReader) para a entidade Vehicle.
    /// </summary>
    /// <param name="reader"></param>
    /// <returns></returns>
    private Vehicle MapVehicleFromReader(DbDataReader reader)
    {
        var vehicle = new Vehicle()
        {
            Id = Convert.ToInt32(reader["id"]),
            RegistrationNo = reader["registration_no"].ToString(),
            Manufacturer = reader["manufacturer"].ToString(),
            Model = reader["model"].ToString(),
            VehicleType = (VehicleType)Convert.ToInt32(reader["vehicle_type_id"]),
            CurrentKm = Convert.ToInt32(reader["current_km"]),
            Status = (VehicleStatus)Convert.ToInt32(reader["status_id"]),
            CreatedAt = Convert.ToDateTime(reader["created_at"])
        };

        if (reader["last_walkaround_at"] != DBNull.Value)
        {
            vehicle.LastWalkaroundAt = Convert.ToDateTime(reader["last_walkaround_at"]);
        }

        return vehicle;
    }

    /// <summary>
    /// Lista apenas veículos operacionais (Active) para seleção do motorista.
    /// </summary>
    /// <returns></returns>
    public List<Vehicle> GetOperationalVehicles()
    {
        var list = new List<Vehicle>();
        using (var connection = _connectionFactory.CreateConnection())
        {
            const string sql = "SELECT * FROM vehicles WHERE status_id = 1 ORDER BY registration_no ASC";
            using (var command = connection.CreateCommand())
            {
                command.CommandText = sql;
                connection.Open();
                using (var reader = (DbDataReader)command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        list.Add(MapVehicleFromReader(reader));
                    }
                }

            }

        }
        return list;
    }


    /// <summary>
    /// Atualiza a data da última inspeção realizada para um veículo específico.
    /// Este método ajuda a renovar o ciclo de conformidade de 7 dias no FleetService.
    /// </summary>
    /// <param name="vehicleId">ID primário do veículo.</param>
    /// <param name="checkDate">Data e hora em que a inspeção foi concluída.</param>
    public void UpdateLastInspectionDate(int vehicleId, DateTime checkDate)
    {
        using (var connection = _connectionFactory.CreateConnection())
        {
            const string sql = "UPDATE vehicles SET last_walkaround_at = @CheckDate WHERE id = @VehicleId";
            
            using (var command = connection.CreateCommand())
            {
                command.CommandText = sql;

                var paramDate = command.CreateParameter();
                paramDate.ParameterName = "@CheckDate";
                paramDate.Value = checkDate;
                command.Parameters.Add(paramDate);


                var paramId = command.CreateParameter();
                paramId.ParameterName = "@VehicleId";
                paramId.Value = vehicleId;
                command.Parameters.Add(paramId);
                
                connection.Open();
                command.ExecuteNonQuery();
            }
        }
    }

    public JADirect.Domain.Entities.Vehicle? GetById(int vehicleId)
    {
        using (var connection = _connectionFactory.CreateConnection())
        {
            const string sql = "SELECT * FROM vehicles WHERE id = @VehicleId";

            using (var command = connection.CreateCommand())
            {
                command.CommandText = sql;
                var parameterVehicleId = command.CreateParameter();
                parameterVehicleId.ParameterName = "@VehicleId";
                parameterVehicleId.Value = vehicleId;
                command.Parameters.Add(parameterVehicleId);

                connection.Open();
                using (var reader = (System.Data.Common.DbDataReader)command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        return MapVehicleFromReader(reader);
                    }
                }
            }
        }
        return null;
    }


    /// <summary>
    /// Atualiza as informações básicas de registro do veículo (Fabricante, Modelo, KM e Tipo).
    /// </summary>
    /// <param name="vehicle"></param>
    public void UpdateVehicleDetails(Vehicle vehicle)
    {
        using (var connection = _connectionFactory.CreateConnection())
        {
            const string sqlUpdateVehicle = @"UPDATE vehicles 
                                              SET manufacturer = @Manufacturer,
                                                  model = @Model,
                                                  current_km = @CurrentKm,
                                                  vehicle_type_id = @VehicleTypeId
                                              WHERE id = @Id";
            
            var textInfo = CultureInfo.CurrentCulture.TextInfo;
            using (var command = connection.CreateCommand())
            {
                command.CommandText = sqlUpdateVehicle;
                AddParameter(command, "@Manufacturer", textInfo.ToTitleCase(vehicle.Manufacturer.Trim().ToLower()));
                AddParameter(command, "@Model", textInfo.ToTitleCase(vehicle.Model.Trim().ToLower()));
                AddParameter(command, "@CurrentKm", vehicle.CurrentKm);
                AddParameter(command, "@VehicleTypeId", ( int)vehicle.VehicleType);
                AddParameter(command, "@Id", vehicle.Id);
                
                connection.Open();
                command.ExecuteNonQuery();
            }
        }
    }


    /// <summary>
    /// Atualiza especificamente o status operacional do veículo no banco de dados.
    /// </summary>
    /// <param name="vehicleId"></param>
    /// <param name="newStatus"></param>
    public void UpdateVehicleStatus(int vehicleId, VehicleStatus newStatus)
    {
        using (var connection = _connectionFactory.CreateConnection())
        {
            const string sqlUpdateVehicleStatus = @"UPDATE vehicles
                                                    SET status_id = @StatusId
                                                    WHERE id = @Id";

            using (var command = connection.CreateCommand())
            {
                command.CommandText = sqlUpdateVehicleStatus;
                AddParameter(command, "@StatusId", (int)newStatus);
                AddParameter(command, "@Id", vehicleId);
                
                connection.Open();
                command.ExecuteNonQuery();
            }
        }
    }

    /// <summary>
    /// Busca um veículo pela placa de registro.
    /// Retorna null se nenhum veículo com a placa informada for encontrado.
    /// </summary>
    /// <param name="registrationNo">Placa do veículo no formato original (ex: 181-D-4812).</param>
    /// <returns>Entidade Vehicle correspondente, ou null se não existir.</returns>
    public Vehicle? GetByRegistrationNo(string registrationNo)
    {
        using (var connection = _connectionFactory.CreateConnection())
        {
            const string sql = @"
                SELECT * 
                FROM vehicles 
                WHERE REPLACE(registration_no, '-', '') = @RegistrationNo";

            using (var command = connection.CreateCommand())
            {
                command.CommandText = sql;
                AddParameter(command, "@RegistrationNo", registrationNo);

                connection.Open();
                using (var reader = (DbDataReader)command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        return MapVehicleFromReader(reader);
                    }
                }
            }
        }

        return null;
    }

}