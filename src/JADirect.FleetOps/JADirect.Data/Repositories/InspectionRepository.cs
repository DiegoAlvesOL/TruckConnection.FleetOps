using System.Data;
using JADirect.Data.Infrastructure;
using JADirect.Domain.Models;
using MySql.Data.MySqlClient;
using System.Text.Json;
using JADirect.Domain.Entities;

namespace JADirect.Data.Repositories;

/// <summary>
/// Repositório especializado na persistência de inspeções veiculares (Walkaround Checks).
/// Todos os métodos usam AddParameter tipado para eliminar ambiguidade de inferência
/// de tipo do driver MySQL e proteger contra SQL injection.
/// </summary>
public class InspectionRepository
{
    private readonly DbConnectionFactory _connectionFactory;

    public InspectionRepository(DbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    /// <summary>
    /// Persiste uma nova inspeção de walkaround e atualiza o status do veículo.
    /// O repositório apenas grava os dados que recebe. Toda decisão de negócio
    /// é responsabilidade do WalkaroundService, que preenche a entidade antes
    /// de chamar este método.
    /// </summary>
    /// <param name="check">Entidade com todos os dados da inspeção já calculados.</param>
    /// <param name="vehicleStatusId">Status do veículo calculado pelo WalkaroundService: 4=bloqueado, 1=operacional.</param>
    public void Add(WalkaroundCheck check, int vehicleStatusId)
    {
        using var connection = (MySqlConnection)_connectionFactory.CreateConnection();
        connection.Open();

        using var transaction = connection.BeginTransaction();
        try
        {
            const string sqlInsert = @"
                INSERT INTO walkaround_checks
                    (check_date, user_id, vehicle_id, odometer, checklist_json,
                     has_defect, latitude, longitude)
                VALUES
                    (NOW(), @UserId, @VehicleId, @Odometer, @ChecklistJson,
                     @HasDefect, @Latitude, @Longitude)";

            using var commandInsert = new MySqlCommand(sqlInsert, connection, transaction);
            AddParameter(commandInsert, "@UserId",        DbType.Int32,   check.UserId);
            AddParameter(commandInsert, "@VehicleId",     DbType.Int32,   check.VehicleId);
            AddParameter(commandInsert, "@Odometer",      DbType.Int32,   check.Odometer);
            AddParameter(commandInsert, "@ChecklistJson", DbType.String,  check.ChecklistJson);
            AddParameter(commandInsert, "@HasDefect",     DbType.Int32,   check.HasDefect ? 1 : 0);
            AddNullableParameter(commandInsert, "@Latitude",  DbType.Decimal, check.Latitude);
            AddNullableParameter(commandInsert, "@Longitude", DbType.Decimal, check.Longitude);
            commandInsert.ExecuteNonQuery();

            const string sqlUpdate = @"
                UPDATE vehicles
                SET status_id          = @StatusId,
                    last_walkaround_at = NOW()
                WHERE id = @VehicleId";

            using var commandUpdate = new MySqlCommand(sqlUpdate, connection, transaction);
            AddParameter(commandUpdate, "@StatusId",  DbType.Int32, vehicleStatusId);
            AddParameter(commandUpdate, "@VehicleId", DbType.Int32, check.VehicleId);
            commandUpdate.ExecuteNonQuery();

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    /// <summary>
    /// Cria um registro de walkaround check com status Draft no banco de dados.
    /// O registro serve de âncora para as fotos tiradas durante o preenchimento.
    /// O id retornado deve ser armazenado na sessão HTTP para vincular cada foto
    /// ao walkaround correto antes do submit final.
    /// O veículo NÃO é atualizado neste momento — apenas no CompleteDraft.
    /// </summary>
    /// <param name="check">Entidade com os dados iniciais da inspeção, incluindo AssignmentId.</param>
    /// <returns>O id gerado pelo banco para o novo registro Draft.</returns>
    public int CreateDraft(WalkaroundCheck check)
    {
        using var connection = (MySqlConnection)_connectionFactory.CreateConnection();

        const string sql = @"
        INSERT INTO walkaround_checks
            (check_date, user_id, vehicle_id, assignment_id, odometer, checklist_json,
             has_defect, status, latitude, longitude)
        VALUES
            (NOW(), @UserId, @VehicleId, @AssignmentId, @Odometer, @ChecklistJson,
             0, 'Draft', @Latitude, @Longitude);
        SELECT LAST_INSERT_ID();";

        using var command = new MySqlCommand(sql, connection);
        AddParameter(command, "@UserId",        DbType.Int32,  check.UserId);
        AddParameter(command, "@VehicleId",     DbType.Int32,  check.VehicleId);
        AddNullableParameter(command, "@AssignmentId", DbType.Int32,   check.AssignmentId);
        AddParameter(command, "@Odometer",      DbType.Int32,  check.Odometer);
        AddParameter(command, "@ChecklistJson", DbType.String, check.ChecklistJson);
        AddNullableParameter(command, "@Latitude",    DbType.Decimal, check.Latitude);
        AddNullableParameter(command, "@Longitude",   DbType.Decimal, check.Longitude);

        connection.Open();

        var generatedId = Convert.ToInt32(command.ExecuteScalar());

        return generatedId;
    }

    /// <summary>
    /// Promove um walkaround check de Draft para Completed, gravando o checklist
    /// final e atualizando o status do veículo.
    /// Este é o único ponto que atualiza last_walkaround_at no veículo,
    /// garantindo que apenas inspeções concluídas contam para o ciclo de compliance.
    /// CompleteDraft só atualiza registros com status=Draft para evitar double-submit.
    /// </summary>
    /// <param name="walkaroundCheckId">ID do draft a ser concluído.</param>
    /// <param name="checklistJson">JSON final do checklist preenchido pelo motorista.</param>
    /// <param name="hasDefect">Indica se algum defeito foi registrado na inspeção.</param>
    /// <param name="vehicleStatusId">Status do veículo calculado pelo WalkaroundService.</param>
    /// <param name="vehicleId">ID do veículo a ter o last_walkaround_at atualizado.</param>
    /// <returns>True se o registro foi encontrado e atualizado. False se o id não existir.</returns>
    public bool CompleteDraft(
        int walkaroundCheckId,
        string checklistJson,
        bool hasDefect,
        int vehicleStatusId,
        int vehicleId,
        int odometer,
        decimal? latitude,
        decimal? longitude)
    {
        using var connection = (MySqlConnection)_connectionFactory.CreateConnection();
        connection.Open();

        using var transaction = connection.BeginTransaction();
        try
        {
            const string sqlUpdate = @"
                UPDATE walkaround_checks
                SET status = 'Completed',
                    checklist_json = @ChecklistJson,
                    has_defect = @HasDefect,
                    odometer = @Odometer,
                    latitude = @Latitude,
                    longitude = @Longitude
                WHERE id = @WalkaroundCheckId
                  AND status = 'Draft'";

            using var commandUpdate = new MySqlCommand(sqlUpdate, connection, transaction);
            AddParameter(commandUpdate, "@ChecklistJson", DbType.String, checklistJson);
            AddParameter(commandUpdate, "@HasDefect", DbType.Int32,  hasDefect ? 1 : 0);
            AddParameter(commandUpdate, "@Odometer", DbType.Decimal, odometer);
            AddParameter(commandUpdate, "@Latitude", DbType.Decimal, latitude);
            AddParameter(commandUpdate, "@Longitude", DbType.Decimal, longitude);
            AddParameter(commandUpdate, "@WalkaroundCheckId", DbType.Int32,  walkaroundCheckId);

            int rowsAffected = commandUpdate.ExecuteNonQuery();

            if (rowsAffected == 0)
            {
                transaction.Rollback();
                return false;
            }

            const string sqlVehicle = @"
                UPDATE vehicles
                SET status_id = @StatusId,
                    last_walkaround_at = NOW()
                WHERE id = @VehicleId";

            using var commandVehicle = new MySqlCommand(sqlVehicle, connection, transaction);
            AddParameter(commandVehicle, "@StatusId",  DbType.Int32, vehicleStatusId);
            AddParameter(commandVehicle, "@VehicleId", DbType.Int32, vehicleId);
            commandVehicle.ExecuteNonQuery();

            transaction.Commit();

            return true;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    /// <summary>
    /// Recupera o histórico completo de inspeções concluídas de um veículo específico.
    /// Registros com status Draft são excluídos para não poluir o histórico de auditoria.
    /// </summary>
    /// <param name="vehicleId">ID do veículo.</param>
    /// <returns>Lista de WalkaroundHistoryViewModel com itens individuais desserializados.</returns>
    public List<WalkaroundHistoryViewModel> GetHistoryByVehicleId(int vehicleId)
    {
        var history = new List<WalkaroundHistoryViewModel>();
        using var connection = (MySqlConnection)_connectionFactory.CreateConnection();

        const string sql = @"
            SELECT
                wc.id, wc.check_date, u.first_name, u.surname, v.registration_no,
                wc.odometer, wc.checklist_json, wc.latitude, wc.longitude
            FROM walkaround_checks wc
            INNER JOIN users u ON wc.user_id = u.id
            INNER JOIN vehicles v ON wc.vehicle_id = v.id
            WHERE wc.vehicle_id = @VehicleId
              AND wc.status     = 'Completed'
            ORDER BY wc.check_date DESC
            LIMIT 30";

        using var command = new MySqlCommand(sql, connection);
        AddParameter(command, "@VehicleId", DbType.Int32, vehicleId);

        connection.Open();
        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            history.Add(MapWalkaroundHistoryFromReader(reader));
        }

        return history;
    }

    /// <summary>
    /// Recupera o histórico de todas as inspeções concluídas da frota.
    /// Registros com status Draft são excluídos para não poluir o histórico de auditoria.
    /// </summary>
    /// <returns>Lista de WalkaroundHistoryViewModel com itens individuais desserializados.</returns>
    public List<WalkaroundHistoryViewModel> GetAllHistory()
    {
        var historyList = new List<WalkaroundHistoryViewModel>();
        using var connection = (MySqlConnection)_connectionFactory.CreateConnection();

        const string sql = @"
            SELECT
                wc.id, wc.check_date, u.first_name, u.surname, v.registration_no,
                wc.odometer, wc.checklist_json, wc.latitude, wc.longitude
            FROM walkaround_checks wc
            INNER JOIN users u ON wc.user_id = u.id
            INNER JOIN vehicles v ON wc.vehicle_id = v.id
            WHERE wc.status = 'Completed'
            ORDER BY wc.check_date DESC";

        using var command = new MySqlCommand(sql, connection);
        connection.Open();

        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            historyList.Add(MapWalkaroundHistoryFromReader(reader));
        }

        return historyList;
    }

    public int DeleteAbandonedDrafts(DateTime olderThan)
    {
        using var connection = (MySqlConnection)_connectionFactory.CreateConnection();
        const string sql = @"
            DELETE FROM walkaround_checks
            WHERE status = 'Draft'
                AND check_date < @OlderThan";
        
        using var command = new MySqlCommand(sql, connection);
        AddParameter(command, "@OlderThan", DbType.DateTime, olderThan);
        
        connection.Open();

        return command.ExecuteNonQuery();
    }


    /// <summary>
    /// Busca um walkaround check completo por ID para geração de documentos.
    /// Retorna null se o registro não existir ou não estiver com status Completed.
    /// Drafts são excluídos intencionalmente, apenas inspeções concluídas
    /// podem gerar documentos oficiais.
    /// </summary>
    /// <param name="walkaroundCheckId">ID do walkaround check.</param>
    /// <returns>WalkaroundDetailViewModel preenchido ou null se não encontrado.</returns>
    public WalkaroundDetailViewModel? GetWalkaroundById(int walkaroundCheckId)
    {
        using var connection = (MySqlConnection)_connectionFactory.CreateConnection();
        
        const string sql = @"
            SELECT
                wc.id,
                wc.check_date,
                wc.odometer,
                wc.checklist_json,
                wc.has_defect,
                u.first_name,
                u.surname,
                v.registration_no,
                v.manufacturer,
                v.model,
                v.vehicle_type_id
            FROM walkaround_checks wc
            INNER JOIN users u    ON wc.user_id    = u.id
            INNER JOIN vehicles v ON wc.vehicle_id = v.id
            WHERE wc.id     = @WalkaroundCheckId
              AND wc.status = 'Completed'";
        
        using var command = new MySqlCommand(sql, connection);
        AddParameter(command, "@WalkaroundCheckId", DbType.Int32, walkaroundCheckId);

        connection.Open();
        using var reader = command.ExecuteReader();

        if (!reader.Read())
        {
            return null;
        }
        
        var checklistJson = reader["checklist_json"]?.ToString() ??"[]";
        var items = new List<ChecklistItemResult>();

        try
        {
            items = JsonSerializer.Deserialize<List<ChecklistItemResult>>(
                checklistJson,
                JsonReadOptions) ?? new List<ChecklistItemResult>();
        }
        catch
        {
            items = new List<ChecklistItemResult>();
        }

        return new WalkaroundDetailViewModel
        {
            WalkaroundId = Convert.ToInt32(reader["id"]),
            CheckDate = Convert.ToDateTime(reader["check_date"]),
            DriverName = string.Format("{0} {1}", reader["first_name"], reader["surname"]),
            VehicleRegistration = reader["registration_no"].ToString() ?? string.Empty,
            VehicleMake = reader["manufacturer"].ToString() ?? string.Empty,
            VehicleModel = reader["model"].ToString() ?? string.Empty,
            VehicleType = ResolveVehicleTypeLabel(Convert.ToInt32(reader["vehicle_type_id"])),
            Odometer = Convert.ToInt32(reader["odometer"]),
            HasDefect = Convert.ToInt32(reader["has_defect"]) == 1,
            Items = items
        };
    }
    
    /// <summary>
    /// Converte o vehicle_type_id numérico para o label legível usado em documentos.
    /// Centralizado aqui para não depender do enum VehicleType na camada de dados
    /// e garantir formatação consistente com espaços ("Rigid Truck", não "RigidTruck").
    /// </summary>
    private static string ResolveVehicleTypeLabel(int vehicleTypeId)
    {
        return vehicleTypeId switch
        {
            1 => "Van",
            2 => "Rigid Truck",
            3 => "Articulated Truck",
            _ => "Vehicle"
        };
    }
    

    /// <summary>
    /// Adiciona um parâmetro tipado ao comando SQL, eliminando ambiguidade de inferência
    /// de tipo do driver MySQL e protegendo contra SQL injection.
    /// </summary>
    /// <param name="command">Comando ao qual o parâmetro será adicionado.</param>
    /// <param name="name">Nome do parâmetro com prefixo @.</param>
    /// <param name="dbType">Tipo SQL explícito do parâmetro.</param>
    /// <param name="value">Valor do parâmetro.</param>
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
    /// Usado para colunas nullable como Latitude e Longitude.
    /// </summary>
    /// <param name="command">Comando ao qual o parâmetro será adicionado.</param>
    /// <param name="name">Nome do parâmetro com prefixo @.</param>
    /// <param name="dbType">Tipo SQL explícito do parâmetro.</param>
    /// <param name="value">Valor nullable do parâmetro.</param>
    private static void AddNullableParameter(MySqlCommand command, string name, DbType dbType, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = dbType;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private static readonly JsonSerializerOptions JsonReadOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Helper privado centralizado para mapear o resultado do banco
    /// para o WalkaroundHistoryViewModel com itens individuais desserializados.
    /// </summary>
    /// <param name="reader">Reader posicionado em um registro válido.</param>
    /// <returns>WalkaroundHistoryViewModel preenchido com itens do checklist.</returns>
    private WalkaroundHistoryViewModel MapWalkaroundHistoryFromReader(MySqlDataReader reader)
    {
        var checklistJson = reader["checklist_json"]?.ToString() ?? "[]";

        var items = new List<ChecklistItemResult>();

        try
        {
            items = JsonSerializer.Deserialize<List<ChecklistItemResult>>(
                checklistJson,
                JsonReadOptions) ?? new List<ChecklistItemResult>();
        }
        catch
        {
            items = new List<ChecklistItemResult>();
        }

        return new WalkaroundHistoryViewModel
        {
            WalkaroundId = Convert.ToInt32(reader["id"]),
            CheckDate    = Convert.ToDateTime(reader["check_date"]),
            DriverName   = string.Format("{0} {1}", reader["first_name"], reader["surname"]),
            RegistrationNo = reader["registration_no"].ToString() ?? string.Empty,
            Odometer     = Convert.ToInt32(reader["odometer"]),
            Items        = items,
            Latitude     = reader["latitude"] != DBNull.Value
                ? Convert.ToDecimal(reader["latitude"])
                : null,
            Longitude    = reader["longitude"] != DBNull.Value
                ? Convert.ToDecimal(reader["longitude"])
                : null
        };
    }
}