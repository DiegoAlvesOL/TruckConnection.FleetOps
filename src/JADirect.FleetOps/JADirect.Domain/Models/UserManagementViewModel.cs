using JADirect.Domain.Entities;
using JADirect.Domain.Enums;

namespace JADirect.Domain.Models;

/// <summary>
/// Modelo para a tela de gerenciamento de usuário.
/// 
/// CARD 7.2: Adicionar lista de períodos ATIVOS para mostrar na interface
/// (férias/licença agendadas ou em andamento).
/// </summary>
public class UserManagementViewModel
{
    public int UserId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string Surname { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public UserRoles Role { get; set; }
    public UserStatus Status { get; set; }
    
    // Helpers para a View (Mantendo a lógica fora do HTML)
    public string DisplayName => $"{FirstName} {Surname}";
    public bool IsActive => Status == UserStatus.Active;
    public string StatusLabel => IsActive ? "Active" : "Deactiveted";
    public string StatusClasses => IsActive ?
        "bg-green-100 text-green-800 border-green-200"
        : "bg-red-100 text-red-800 border-red-800";

    // Campos de indisponibilidade ATUAL (usado no formulário)
    public int AvailabilityPeriodId { get; set; }
    public DateTime? AvailabilityFromDate { get; set; }
    public DateTime? AvailabilityToDate { get; set; }
    public string AvailabilityReason { get; set; } = string.Empty;

    /// <summary>
    /// Helper que retorna true se o usuário está em OnLeave ou Sick.
    /// Facilita renderização condicional na view (mostrar/esconder painel de disponibilidade).
    /// </summary>
    public bool IsOnLeaveOrSick => Status == UserStatus.OnLeave || Status == UserStatus.Sick;

    /// <summary>
    /// NOVO CARD 7.2: Lista de períodos ATIVOS (vigentes ou agendados).
    /// Usado para exibir na tabela de histórico de férias/licenças.
    /// Apenas períodos com status 'active' aparecem aqui.
    /// </summary>
    public List<AvailabilityPeriod> ActivePeriods { get; set; } = new();

    /// <summary>
    /// NOVO CARD 7.2: Helper para decidir se a tabela de períodos deve ser exibida.
    /// </summary>
    public bool HasActivePeriods => ActivePeriods.Count > 0;
}