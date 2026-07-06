using JADirect.Domain.Models;
using JADirect.Domain.Enums;

namespace JADirect.Application.Services;

/// <summary>
/// Orquestrador da frota. Centraliza a inteligência de conformidade, aplicando regras
/// diferenciadas de expiração de inspeção (Walkaround) baseadas no tipo de veículo.
/// </summary>
public class FleetService
{
    /// <summary>
    /// Inicializa uma nova instância do serviço de frota.
    /// </summary>
    public FleetService(){}

    /// <summary>
    /// Avalia a conformidade de um veículo específico e gera o modelo de status para o Dashboard.
    /// </summary>
    /// <param name="vehicleId">ID primário do veículo no banco de dados.</param>
    /// <param name="registrationNo">Placa do veículo.</param>
    /// <param name="brand">Fabricante/Marca.</param>
    /// <param name="model">Modelo do veículo.</param>
    /// <param name="currentKm">Quilometragem atual registrada.</param>
    /// <param name="lastCheck">Data e hora da última inspeção realizada com sucesso.</param>
    /// <param name="vehicleType">Tipo do veículo (Van ou Truck) para aplicação da regra de validade.</param>
    /// <returns>Objeto contendo a cor do semáforo, mensagem de status e permissão para Daily Log.</returns>
    public VehicleStatusViewModel GetVehicleCompliance(int vehicleId, string registrationNo, string brand, string model,
        int currentKm, DateTime? lastCheck, VehicleType vehicleType, int statusId)
    {
        var viewModel = new VehicleStatusViewModel
        {
            Id = vehicleId,
            RegistrationNo = registrationNo,
            Manufacturer = brand,
            Model = model,
            CurrentKm = currentKm,
            LastInspectionDate = lastCheck,
        };

        // Valida se o veículo está com status 4, garante e nenhum veiculo com esse status possa ser usado.
        if (statusId == 4)
        {
            return SetStatus(viewModel, "Red", false, "Vehicle Blocked: Maintenance Required");
        }

        // Bloqueio imediato se nunca houve uma inspeção registrada
        if (!lastCheck.HasValue)
        {
            return SetStatus(viewModel, "Red", false, "First walkaround check Required");
        }
        
        int daysSince = (DateTime.Now.Date - lastCheck.Value.Date).Days;

        // Regra para VANS: Ciclo de renovação de 7 dias
        if (vehicleType == VehicleType.Van)
        {
            if (daysSince < 6)
            {
                return SetStatus(viewModel, "Green", true, "Walkaround check compliant");
            }

            if (daysSince == 6)
            {
                return SetStatus(viewModel, "Yellow", true, "walkaround check expires tomorrow");
            }
            
            // Vermelho: 7 dias ou mais (Expirado)
            return SetStatus(viewModel, "Red", false, "Walkaround check expired");
        }
        // Regra para CAMINHÕES: Validade apenas para o dia corrente
        else if (vehicleType == VehicleType.RigidTruck || vehicleType == VehicleType.AticulatedTruck)
        {
            if (daysSince == 0) // Verde: Feito hoje
            {
                return SetStatus(viewModel, "Green", true, "Walkaround check compliant");
            }
            
            // Vermelho: Qualquer data anterior a hoje
            return SetStatus(viewModel, "Red", false, "Walkaround check required");
        }
        else
        {
            return SetStatus(viewModel, "Red", false, "Unknown Vehicle Type");
        }
    }

    /// <summary>
    /// Padroniza a resposta de status para o Dashboard.
    /// </summary>
    /// <param name="model">Instância do ViewModel a ser populada.</param>
    /// <param name="color">Cor resultante (Green, Yellow, Red).</param>
    /// <param name="allowed">Define se o botão de Daily Log será habilitado.</param>
    /// <param name="msg">Mensagem descritiva do estado atual.</param>
    /// <returns>O ViewModel devidamente configurado.</returns>
    private VehicleStatusViewModel SetStatus(VehicleStatusViewModel model, string color, bool allowed, string msg)
    {
        model.StatusColor = color;
        model.IsDailyLogAllowed = allowed;
        model.StatusMessage = msg;
        return model;
    }
}