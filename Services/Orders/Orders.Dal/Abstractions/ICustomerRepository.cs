using Orders.Domain.Models;

namespace Orders.Dal.Abstractions;

public interface ICustomerRepository : IGenericRepository<Customer>
{
    Task<Customer?> GetByEmailAsync(string email, CancellationToken ct = default);
}
