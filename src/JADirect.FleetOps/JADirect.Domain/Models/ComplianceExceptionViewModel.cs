namespace JADirect.Domain.Models;


/// <summary>
/// Representa uma falha operacional detectada, como ausência de logs ou inspeções pendentes.
/// Este modelo é consumido pela lista de conformidade no Dashboard do Gerente.
/// </summary>
public class ComplianceExceptionViewModel
{
    public int UserId { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public string DriverName { get; set; } = string.Empty;
    public string RegistrationNo { get; set; } = string.Empty;
    public string Message {  get; set; } =  string.Empty;
    public int VehicleId { get; set; }
    public string Severity { get; set; } = "warning";
}