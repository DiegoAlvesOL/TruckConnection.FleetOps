using System.Data;
using System.Data.Common;
using JADirect.Data.Infrastructure;
using JADirect.Domain.Entities;

namespace JADirect.Data.Repositories;

/// <summary>
/// Repositório responsável pela persistência de metadados de fotografias
/// registradas durante os walkaround checks.
/// </summary>
public class PhotoRepository
{
    private readonly DbConnectionFactory _connectionFactory;

    public PhotoRepository(DbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    /// <summary>
    /// Insere o registro de metadados de uma foto na tabela walkaround_photos.
    /// O arquivo físico já deve ter sido enviado ao Railway Bucket antes desta chamada.
    /// O campo created_at é preenchido automaticamente pelo banco via DEFAULT CURRENT_TIMESTAMP.
    /// </summary>
    /// <param name="photo">Entidade com todos os metadados da foto já preenchidos.</param>
    public void InsertPhoto(WalkaroundPhoto photo)
    {
        using (var connection = _connectionFactory.CreateConnection())
        {
            const string sql = @"
                INSERT INTO walkaround_photos
                    (walkaround_id, checklist_item_id, driver_id, vehicle_id,
                     storage_key, file_size_kb, taken_at)
                VALUES
                    (@WalkaroundId, @ChecklistItemId, @DriverId, @VehicleId,
                     @StorageKey, @FileSizeKb, @TakenAt)";

            using (var command = connection.CreateCommand())
            {
                command.CommandText = sql;
                AddParameter(command, "@WalkaroundId", photo.WalkaroundId);
                AddParameter(command, "@ChecklistItemId", photo.ChecklistItemId);
                AddParameter(command, "@DriverId", photo.DriverId);
                AddParameter(command, "@VehicleId", photo.VehicleId);
                AddParameter(command, "@StorageKey", photo.StorageKey);
                AddParameter(command, "@FileSizeKb", photo.FileSizeKb);
                AddParameter(command, "@TakenAt", photo.TakenAt);

                connection.Open();
                command.ExecuteNonQuery();
            }
        }
    }



    /// <summary>
    /// Retorna todas as fotos vinculadas a um walkaround check específico.
    /// Usado pelo PhotoService para servir as imagens na tela de histórico e detalhe.
    /// </summary>
    /// <param name="walkaroundId">ID do walkaround check.</param>
    /// <returns>Lista de WalkaroundPhoto com os metadados de cada foto.</returns>
    public List<WalkaroundPhoto> GetPhotosByWalkaroundId(int walkaroundId)
    {
        var photos = new List<WalkaroundPhoto>();

        using (var connection = _connectionFactory.CreateConnection())
        {
            const string sql = @"
                SELECT id, walkaround_id, checklist_item_id, driver_id, vehicle_id,
                       storage_key, file_size_kb, taken_at, created_at
                FROM walkaround_photos
                WHERE walkaround_id = @WalkaroundId
                ORDER BY taken_at ASC";

            using (var command = connection.CreateCommand())
            {
                command.CommandText = sql;
                AddParameter(command, "@WalkaroundId", walkaroundId);
                
                connection.Open();
                using (var reader = (DbDataReader)command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        photos.Add(MapPhotoFromReader(reader));
                    }
                }
            }
        }
        return photos;
    }

    public Dictionary<int, Dictionary<int, string>> GetPhotosByWalkaroundIds(
        List<int> walkaroundIds)
    {
        var result = new Dictionary<int, Dictionary<int, string>>();

        if (walkaroundIds.Count == 0)
        {
            return result;
        }

        using (var connection = _connectionFactory.CreateConnection())
        {
            var parameterNames = walkaroundIds
                .Select((id, index) => $"@id{index}")
                .ToList();
            
            string sql = string.Format(
                @"SELECT walkaround_id, checklist_item_id, storage_key
              FROM walkaround_photos
              WHERE walkaround_id IN ({0})",
                string.Join(", ", parameterNames));

            using (var command = connection.CreateCommand())
            {
                command.CommandText = sql;
                for (int index = 0; index < walkaroundIds.Count; index++)
                {
                    AddParameter(command, $"@id{index}", walkaroundIds[index]);
                }
                
                connection.Open();
                using (var reader = (DbDataReader)command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        int walkaroundId = Convert.ToInt32(reader["walkaround_id"]);
                        int checklistItemId = Convert.ToInt32(reader["checklist_item_id"]);
                        string storageKey = reader["storage_key"].ToString() ?? string.Empty;

                        if (!result.ContainsKey(walkaroundId))
                        {
                            result[walkaroundId] = new Dictionary<int, string>();
                        }

                        result[walkaroundId][checklistItemId] = storageKey;
                    }
                }
            }
        }
        return result;
    }
    
    
    
    /// <summary>
    /// Verifica se existe registro de foto para um storage_key específico.
    /// Usado pelo PhotoService antes de acessar o bucket, evitando chamadas
    /// desnecessárias ao Railway Bucket para chaves inexistentes.
    /// </summary>
    /// <param name="storageKey">Chave do arquivo no Railway Bucket.</param>
    /// <returns>True se o registro existe na tabela. False caso contrário.</returns>
    public bool ExistsByStorageKey(string storageKey)
    {
        using (var connection = _connectionFactory.CreateConnection())
        {
            const string sql = @"
                SELECT COUNT(1)
                FROM walkaround_photos
                WHERE storage_key = @StorageKey";

            using (var command = connection.CreateCommand())
            {
                command.CommandText = sql;
                AddParameter (command, "@StorageKey", storageKey);
                
                connection.Open();
                return Convert.ToInt32(command.ExecuteScalar()) > 0;
            }
        }
    }

    /// <summary>
    /// Helper privado para adicionar parâmetros de forma segura,
    /// evitando SQL Injection e mantendo independência do provider MySQL.
    /// </summary>
    /// <param name="command">Comando ao qual o parâmetro será adicionado.</param>
    /// <param name="name">Nome do parâmetro com o prefixo @.</param>
    /// <param name="value">Valor do parâmetro. Null é convertido para DBNull.</param>
    private void AddParameter(IDbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    /// <summary>
    /// Helper privado que mapeia uma linha do banco de dados para a entidade WalkaroundPhoto.
    /// Centraliza o mapeamento para evitar duplicação entre os métodos de leitura.
    /// </summary>
    /// <param name="reader">Reader posicionado em um registro válido.</param>
    /// <returns>WalkaroundPhoto com todos os campos preenchidos.</returns>
    private WalkaroundPhoto MapPhotoFromReader(DbDataReader reader)
    {
        return new WalkaroundPhoto
        {
            Id = Convert.ToInt32(reader["id"]),
            WalkaroundId = Convert.ToInt32(reader["walkaround_id"]),
            ChecklistItemId = Convert.ToInt32(reader["checklist_item_id"]),
            DriverId = Convert.ToInt32(reader["driver_id"]),
            VehicleId = Convert.ToInt32(reader["vehicle_id"]),
            StorageKey = reader["storage_key"].ToString() ?? string.Empty,
            FileSizeKb = Convert.ToInt32(reader["file_size_kb"]),
            TakenAt = Convert.ToDateTime(reader["taken_at"]),
            CreatedAt = Convert.ToDateTime(reader["created_at"])
        };
    }
}