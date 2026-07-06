using JADirect.Data.Infrastructure;
using JADirect.Domain.Entities;
using MySql.Data.MySqlClient;

namespace JADirect.Data.Repositories;


/// <summary>
///Purpose: Repositório responsável pela leitura dos itens do checklist de inspeção.
/// Retorna os itens filtrados por tenant e tipo de veículo para
/// renderização dinâmica do formulário de walkaround.
/// Consumed by: WalkaroundController (Web layer)
/// </summary>
public class ChecklistItemRepository
{
    private readonly DbConnectionFactory _connectionFactory;

    public ChecklistItemRepository(DbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }
    
        /// <summary>
    /// Retorna todos os itens ativos do checklist para o tipo de veículo informado,
    /// ordenados por sort_order para exibição correta no formulário.
    /// </summary>
    /// <param name="tenantId">ID do tenant para filtrar os itens corretos.</param>
    /// <param name="vehicleTypeId">Tipo do veículo: 1=Van, 2=RigidTruck, 3=ArticulatedTruck.</param>
    /// <returns>Lista de ChecklistItem ordenada por sort_order.</returns>
    public List<ChecklistItem> GetItemsByVehicleType(int tenantId, int vehicleTypeId)
    {
        var items = new List<ChecklistItem>();
        using var connection = (MySqlConnection)_connectionFactory.CreateConnection();

        const string sql = @"
            SELECT id, tenant_id, vehicle_type_id, category, label, sort_order, is_active
            FROM checklist_items
            WHERE tenant_id = @TenantId
              AND vehicle_type_id = @VehicleTypeId
              AND is_active = 1
            ORDER BY sort_order ASC";

        using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@TenantId", tenantId);
        command.Parameters.AddWithValue("@VehicleTypeId", vehicleTypeId);

        connection.Open();
        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            items.Add(MapChecklistItemFromReader(reader));
        }

        return items;
    }

    /// <summary>
    /// Helper privado para mapear o resultado do banco para a entidade ChecklistItem.
    /// </summary>
    /// <param name="reader">Reader posicionado em um registro válido.</param>
    /// <returns>Instância de ChecklistItem preenchida.</returns>
    private ChecklistItem MapChecklistItemFromReader(MySqlDataReader reader)
    {
        return new ChecklistItem
        {
            Id = Convert.ToInt32(reader["id"]),
            TenantId = Convert.ToInt32(reader["tenant_id"]),
            VehicleTypeId = Convert.ToInt32(reader["vehicle_type_id"]),
            Category = reader["category"].ToString() ?? string.Empty,
            Label = reader["label"].ToString() ?? string.Empty,
            SortOrder = Convert.ToInt32(reader["sort_order"]),
            IsActive = Convert.ToBoolean(reader["is_active"])
        };
    }
}