using System.Net.Http.Json;
using System.Text.Json;
using JADirect.Data.Repositories;
using JADirect.Domain.Entities;
using JADirect.Domain.Enums;
using JADirect.Domain.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace JADirect.Application.Services;

/// <summary>
/// Serviço responsável por enviar todas as mensagens WhatsApp do sistema.
/// Recebe os dados prontos, monta o payload da Meta API e grava o log de cada envio.
/// Não consulta banco de dados. Não conhece regras de conformidade.
/// </summary>
public class WhatsAppAlertService
{
    private readonly MessageLogRepository _messageLogRepository;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<WhatsAppAlertService> _logger;

    public WhatsAppAlertService(
        MessageLogRepository messageLogRepository,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<WhatsAppAlertService> logger)
    {
        _messageLogRepository = messageLogRepository;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Envia um alerta individual para cada motorista da lista.
    /// Usa texto simples quando há sessão ativa (gratuito) ou template aprovado (cobrado).
    /// </summary>
    /// <param name="tenantId">Identificador do tenant.</param>
    /// <param name="drivers">Lista de motoristas pendentes.</param>
    public async Task SendDriverAlertsAsync(int tenantId, List<PendingDriverAlert> drivers)
    {
        var httpClient = BuildHttpClient();
        string phoneNumberId = ReadRequiredConfig("MetaWhatsApp:PhoneNumberId", "META_WHATSAPP_PHONE_NUMBER_ID");
        string apiVersion = _configuration["MetaWhatsApp:ApiVersion"] ?? "v19.0";

        foreach (var driver in drivers)
        {
            object payload = BuildDriverPayload(driver);

            await SendAndLogAsync(
                httpClient, payload, phoneNumberId, apiVersion,
                tenantId, driver.UserId, driver.PhoneNumber,
                WhatsappMessageType.DriverAlert);
        }
    }

    /// <summary>
    /// Envia o resumo de conformidade ao gestor do tenant com a lista de motoristas pendentes.
    /// </summary>
    /// <param name="tenantId">Identificador do tenant.</param>
    /// <param name="tenant">Dados do tenant incluindo o telefone do gestor.</param>
    /// <param name="pendingDrivers">Lista de motoristas que não preencheram o Daily Log.</param>
    public async Task SendManagerSummaryAsync(
        int tenantId,
        Tenant tenant,
        List<PendingDriverAlert> pendingDrivers)
    {
        var httpClient = BuildHttpClient();
        string phoneNumberId = ReadRequiredConfig("MetaWhatsApp:PhoneNumberId", "META_WHATSAPP_PHONE_NUMBER_ID");
        string apiVersion = _configuration["MetaWhatsApp:ApiVersion"] ?? "v19.0";

        object payload = BuildManagerPayload(tenant, pendingDrivers);

        await SendAndLogAsync(
            httpClient, payload, phoneNumberId, apiVersion,
            tenantId, userId: null, tenant.WhatsappManagerPhone,
            WhatsappMessageType.ManagerSummary);
    }

    /// <summary>
    /// Monta o payload para o alerta ao motorista.
    /// Sessão ativa: texto simples gratuito.
    /// Sem sessão: template aprovado pela Meta.
    /// </summary>
    private static object BuildDriverPayload(PendingDriverAlert driver)
    {
        if (driver.HasActiveSession)
        {
            return new
            {
                messaging_product = "whatsapp",
                to = driver.PhoneNumber,
                type = "text",
                text = new
                {
                    body = string.Format(
                        "Hi {0}, we haven't received your daily log yet for today. " +
                        "Please take a moment to submit your deliveries, collections and returns " +
                        "so your records stay up to date. " +
                        "You can access the system here: https://jadirectfleetops-production.up.railway.app/",
                        driver.FirstName)
                }
            };
        }

        return new
        {
            messaging_product = "whatsapp",
            to = driver.PhoneNumber,
            type = "template",
            template = new
            {
                name = "driver_daily_log_reminder",
                language = new { code = "en" },
                components = new[]
                {
                    new
                    {
                        type = "body",
                        parameters = new[] { new { type = "text", text = driver.FirstName } }
                    }
                }
            }
        };
    }

    /// <summary>
    /// Monta o payload para o resumo ao gestor com a lista de motoristas pendentes.
    /// </summary>
    private static object BuildManagerPayload(Tenant tenant, List<PendingDriverAlert> pendingDrivers)
    {
        string driverNameList = string.Join(", ",
            pendingDrivers.Select(d => string.Format("{0} {1}", d.FirstName, d.Surname)));

        return new
        {
            messaging_product = "whatsapp",
            to = tenant.WhatsappManagerPhone,
            type = "template",
            template = new
            {
                name = "manager_compliance_summary",
                language = new { code = "en" },
                components = new[]
                {
                    new
                    {
                        type = "body",
                        parameters = new[]
                        {
                            new { type = "text", text = tenant.Name },
                            new { type = "text", text = driverNameList }
                        }
                    }
                }
            }
        };
    }

    /// <summary>
    /// Executa o envio HTTP e persiste o resultado em log.
    /// Captura exceções sem relançar para que falha de um envio não interrompa os demais.
    /// </summary>
    private async Task SendAndLogAsync(
        HttpClient httpClient,
        object payload,
        string phoneNumberId,
        string apiVersion,
        int tenantId,
        int? userId,
        string phoneNumber,
        WhatsappMessageType messageType)
    {
        var log = new WhatsappMessageLog
        {
            TenantId = tenantId,
            UserId = userId,
            MessageType = messageType,
            PhoneNumber = phoneNumber,
            SentAt = DateTime.UtcNow
        };

        try
        {
            string url = string.Format("{0}/{1}/messages", apiVersion, phoneNumberId);
            var response = await httpClient.PostAsJsonAsync(url, payload);
            string body = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                using var document = JsonDocument.Parse(body);
                string? metaMessageId = document.RootElement
                    .GetProperty("messages")[0]
                    .GetProperty("id")
                    .GetString();

                log.Status = WhatsappMessageStatus.Sent;
                log.MetaMessageId = metaMessageId;

                _logger.LogInformation(
                    "WhatsAppAlertService: sent to {Phone}. MetaId: {MetaId}.",
                    phoneNumber, metaMessageId);
            }
            else
            {
                log.Status = WhatsappMessageStatus.Failed;
                log.ErrorMessage = body;

                _logger.LogWarning(
                    "WhatsAppAlertService: failed to send to {Phone}. Response: {Body}.",
                    phoneNumber, body);
            }
        }
        catch (Exception ex)
        {
            log.Status = WhatsappMessageStatus.Failed;
            log.ErrorMessage = ex.Message;

            _logger.LogError(ex,
                "WhatsAppAlertService: exception sending to {Phone}.", phoneNumber);
        }
        finally
        {
            _messageLogRepository.Insert(log);
        }
    }

    private HttpClient BuildHttpClient()
    {
        string accessToken = ReadRequiredConfig("MetaWhatsApp:AccessToken", "META_WHATSAPP_ACCESS_TOKEN");
        var httpClient = _httpClientFactory.CreateClient();
        httpClient.BaseAddress = new Uri("https://graph.facebook.com/");
        httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        return httpClient;
    }

    private string ReadRequiredConfig(string configKey, string envVarKey)
    {
        return _configuration[configKey]
               ?? Environment.GetEnvironmentVariable(envVarKey)
               ?? throw new InvalidOperationException(
                   string.Format("Configuração '{0}' não encontrada.", configKey));
    }
}