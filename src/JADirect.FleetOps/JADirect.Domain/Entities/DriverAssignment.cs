namespace JADirect.Domain.Entities;

/// <summary>
/// Representa a alocação de um motorista a um veículo em um dia específico de operação.
/// O encerramento do assignment é implícito pela data: ao virar a meia-noite,
/// o assignment do dia anterior deixa de ser o ativo sem necessidade de coluna de status.
/// </summary>
public sealed class DriverAssignment
{
    public int Id { get; set; }
    public int DriverId { get; set; }
    public int VehicleId { get; set; }
    public DateOnly AssignmentDate { get; set; }
    public DateTime CreatedAt { get; set; }
}