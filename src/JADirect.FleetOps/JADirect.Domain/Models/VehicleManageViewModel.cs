using JADirect.Domain.Entities;

namespace JADirect.Domain.Models;

public class VehicleManageViewModel
{
    public Vehicle Vehicle { get; set; } = new Vehicle();

    public List<WalkaroundHistoryViewModel> WalkaroundHistory { get; set; } = new();
}