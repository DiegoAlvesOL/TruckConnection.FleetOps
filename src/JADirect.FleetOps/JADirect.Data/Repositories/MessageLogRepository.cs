using System.Data;
using JADirect.Data.Infrastructure;
using JADirect.Domain.Entities;
using MySql.Data.MySqlClient;

namespace JADirect.Data.Repositories;


/// <summary>
/// Repositório responsável por gravar cada tentativa de envio de mensagem WhatsApp.
/// Registra sucesso e falha sem distinção — toda tentativa gera um log.
/// </summary>
public class MessageLogRepository
{
    private readonly DbConnectionFactory _connectionFactory;

    public MessageLogRepository(DbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }


    /// <summary>
    /// Insere um registro de log de envio no banco de dados.
    /// </summary>
    /// <param name="log">Entidade de log preenchida pelo serviço de alerta.</param>
    public void Insert(WhatsappMessageLog log)
    {
        using var connection = (MySqlConnection)_connectionFactory.CreateConnection();
        
        const string sql = @"
            INSERT INTO whatsapp_message_logs
                (tenant_id, user_id, message_type, phone_number,
                 status, meta_message_id, error_message, sent_at)
            VALUES
                (@TenantId, @UserId, @MessageType, @PhoneNumber,
                 @Status, @MetaMessageId, @ErrorMessage, @SentAt)";
        
        using var command = new MySqlCommand(sql, connection);
        AddParameter(command, "@TenantId", DbType.Int32, log.TenantId);
        AddNullableParameter(command, "@UserId", DbType.Int32, log.UserId);
        AddParameter(command, "@MessageType", DbType.String, log.MessageType);
        AddParameter(command, "@PhoneNumber", DbType.String, log.PhoneNumber);
        AddParameter(command, "@Status", DbType.String, log.Status);
        AddNullableParameter(command, "@MetaMessageId", DbType.String, log.MetaMessageId);
        AddNullableParameter(command, "@ErrorMessage",  DbType.String, log.ErrorMessage);
        AddParameter(command, "@SentAt", DbType.DateTime, log.SentAt);
        connection.Open();
        command.ExecuteNonQuery();
    }


    private static void AddParameter(MySqlCommand command, string name, DbType dbType, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = dbType;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
    
    
    private static void AddNullableParameter(MySqlCommand command, string name, DbType dbType, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = dbType;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }
    
}