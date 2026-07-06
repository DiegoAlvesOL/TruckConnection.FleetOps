using JADirect.Data.Infrastructure;
using JADirect.Domain.Entities;
using MySql.Data.MySqlClient;

namespace JADirect.Data.Repositories;


/// <summary>
/// Purpose: Repositório responsável pela leitura das regras de bloqueio de veículo por tenant.
/// As regras definem quais combinações de estado e ação resultam em bloqueio.
/// Consumed by: WalkaroundService (Application layer)
/// </summary>
public class BlockingRuleRepository
{
    private readonly DbConnectionFactory _connectionFactory;

    public BlockingRuleRepository(DbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public List<BlockingRule> GetRulesByTenant(int tenantId)
    {
        var rules = new List<BlockingRule>();
        using var connection = (MySqlConnection)_connectionFactory.CreateConnection();
        
        const string sql = @"
            SELECT id, tenant_id, item_state, action_taken, blocks_vehicle
            FROM walkaround_blocking_rules
            WHERE tenant_id = @TenantId";

        using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@TenantId", tenantId);
        connection.Open();
        using var reader = command.ExecuteReader();
        
        while (reader.Read())
        {
            rules.Add(MapBlockingRuleFromReader(reader));
        }
        return rules;
    }

    private BlockingRule MapBlockingRuleFromReader(MySqlDataReader reader)
    {
        return new BlockingRule
        {
            Id = Convert.ToInt32(reader["id"]),
            TenantId = Convert.ToInt32(reader["tenant_id"]),
            ItemState = reader["item_state"].ToString() ?? string.Empty,
            ActionTaken = reader["action_taken"].ToString() ?? string.Empty,
            BlocksVehicle = Convert.ToBoolean(reader["blocks_vehicle"]),
        };
    }
}