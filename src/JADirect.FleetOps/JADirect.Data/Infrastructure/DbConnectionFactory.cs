using MySql.Data.MySqlClient;
using System.Data;

namespace JADirect.Data.Infrastructure;

/// <summary>
/// Responsável por gerenciar a criação de conexões com o MySql.
/// </summary>
public class DbConnectionFactory
{
    private readonly string _connectionString;
    
    /// <summary>
    /// Inicializa a factory com a string de conexão vinda do arquivo de configuração.
    /// </summary>
    /// <param name="connectionString"></param>
    public DbConnectionFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    public IDbConnection CreateConnection()
    {
        return new MySqlConnection(_connectionString);
    }

}