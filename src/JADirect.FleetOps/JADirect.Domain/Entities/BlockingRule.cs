using JADirect.Domain.Enums;

namespace JADirect.Domain.Entities;

/// <summary>
/// Representa uma regra de bloqueio de veículo configurada por tenant.
/// O WalkaroundService carrega as regras do tenant e as aplica contra
/// os itens submetidos pelo motorista para calcular o status do veículo.
/// </summary>
public class BlockingRule
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public string ItemState { get; set; } =  string.Empty;
    public string ActionTaken { get; set; } = string.Empty;
    public bool BlocksVehicle {  get; set; }
}