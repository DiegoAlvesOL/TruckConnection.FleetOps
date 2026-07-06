using JADirect.Domain.Entities;
using JADirect.Data.Repositories;

namespace JADirect.Application.Services;

public class AssignmentService
{
    private readonly AssignmentRepository _assignmentRepository;

    public AssignmentService(AssignmentRepository assignmentRepository)
    {
        _assignmentRepository = assignmentRepository;
    }

    public (bool Success, string ErrorMessage) AssignVehicleToDriver(int driverId, int vehicleId)
    {
        DriverAssignment? existingDriverAssignment = _assignmentRepository.GetTodayAssignmentByDriver(driverId);

        if (existingDriverAssignment != null)
        {
            return (false, "You have already assumed a vehicle today. Only one vehicle per driver per day is allowed.");
            
        }
        
        bool vehicleAlreadyAssigned = _assignmentRepository.ExistsActiveAssignmentForVehicle(vehicleId);

        if (vehicleAlreadyAssigned)
        {
            return (false, "This vehicle has already been assigned to another driver today.");
        }


        var assignment = new DriverAssignment
        {
            DriverId = driverId,
            VehicleId = vehicleId,
            AssignmentDate = DateOnly.FromDateTime(DateTime.Today)
        };
        
        _assignmentRepository.CreateAssignment(assignment);
        
        return (true, string.Empty);
    }



    public DriverAssignment? GetTodayJourneyState(int driverId)
    {
        return _assignmentRepository.GetTodayAssignmentByDriver(driverId);
    }
    
    
    /// <summary>
    /// Remove o assignment ativo do motorista no dia atual, devolvendo o veículo
    /// ao pool disponível para que outro motorista possa assumí-lo.
    /// A devolução é permitida a qualquer momento do dia, independentemente
    /// de o motorista ter realizado o walkaround check. O walkaround pertence
    /// ao veículo e permanece válido para quem assumir a seguir.
    /// </summary>
    /// <param name="driverId">ID do motorista que está devolvendo o veículo.</param>
    /// <returns>
    /// Tupla onde Success indica se a devolução foi processada,
    /// e ErrorMessage contém a razão da falha quando Success for false.
    /// </returns>
    public (bool Success, string ErrorMessage) UnassignVehicle(int driverId)
    {
        // VERIFICAÇÃO: confirma que há um assignment ativo antes de tentar remover.
        DriverAssignment? activeAssignment = _assignmentRepository.GetTodayAssignmentByDriver(driverId);

        if (activeAssignment == null)
        {
            return (false, "You do not have an active vehicle assignment to return today.");
        }

        bool removed = _assignmentRepository.DeleteTodayAssignment(driverId);

        if (!removed)
        {
            return (false, "Unable to return the vehicle. Please try again.");
        }

        return (true, string.Empty);
    }
    
}