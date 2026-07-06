using Microsoft.Extensions.Logging;

namespace JADirect.Application.Services;


/// <summary>
/// Serviço responsável pelo processamento dos eventos recebidos via webhook da Meta.
/// Executa de forma assíncrona após o controller já ter retornado 200 OK.
/// Futuramente processará sessões de 24h quando motoristas enviarem mensagens.
/// </summary>
public class WhatsAppWebhookService
{
    private readonly ILogger<WhatsAppWebhookService> _logger;

    public WhatsAppWebhookService(ILogger<WhatsAppWebhookService> logger)
    {
        _logger = logger;
    }
    
    /// <summary>
    /// Processa o payload recebido do webhook da Meta.
    /// Por ora registra o evento para inspeção durante os testes.
    /// </summary>
    public Task ProcessAsync(string rawPayload)
    {
        _logger.LogInformation(
            "WhatsAppWebhookService: event received. Payload: {Payload}", rawPayload);

        return Task.CompletedTask;
    }
    
}