using AutoMapper;
using Orders.Bll.Dtos;
using Orders.Bll.Validation;
using Orders.Dal.Abstractions;
using Orders.Domain.Exceptions;
using Orders.Domain.Models;

namespace Orders.Bll.Services;

public sealed class CustomerService : ICustomerService
{
    private const string Actor = "orders-api";

    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;

    public CustomerService(IUnitOfWork uow, IMapper mapper)
    {
        _uow = uow;
        _mapper = mapper;
    }

    public async Task<CustomerDto> GetAsync(long id, CancellationToken ct = default)
    {
        var customer = await _uow.Customers.GetByIdAsync(id, ct)
            ?? throw new NotFoundException(nameof(Customer), id);
        return _mapper.Map<CustomerDto>(customer);
    }

    public async Task<PagedResultDto<CustomerDto>> ListAsync(int page, int pageSize, CancellationToken ct = default)
    {
        RequestRules.EnsurePaging(page, pageSize);

        var result = await _uow.Customers.GetPagedAsync(page, pageSize, ct);
        return new PagedResultDto<CustomerDto>
        {
            Items = _mapper.Map<List<CustomerDto>>(result.Items),
            TotalCount = result.TotalCount,
            Page = result.Page,
            PageSize = result.PageSize
        };
    }

    public async Task<CustomerDto> CreateAsync(CreateCustomerDto dto, CancellationToken ct = default)
    {
        var customer = _mapper.Map<Customer>(dto);
        Normalize(customer);

        if (await _uow.Customers.GetByEmailAsync(customer.Email, ct) is not null)
            throw new BusinessConflictException($"Customer with email '{customer.Email}' already exists.");

        customer.CreatedBy = Actor;
        var id = await _uow.Customers.AddAsync(customer, ct);

        return await GetAsync(id, ct);
    }

    public async Task<CustomerDto> UpdateAsync(long id, UpdateCustomerDto dto, CancellationToken ct = default)
    {
        var customer = await _uow.Customers.GetByIdAsync(id, ct)
            ?? throw new NotFoundException(nameof(Customer), id);

        var expectedRowVersion = RequestRules.ParseRowVersion(dto.RowVersion);

        _mapper.Map(dto, customer);
        Normalize(customer);

        var sameEmail = await _uow.Customers.GetByEmailAsync(customer.Email, ct);
        if (sameEmail is not null && sameEmail.Id != id)
            throw new BusinessConflictException($"Customer with email '{customer.Email}' already exists.");

        if (expectedRowVersion is not null)
            customer.RowVersion = expectedRowVersion;
        customer.UpdatedBy = Actor;

        await _uow.Customers.UpdateAsync(customer, ct);

        return await GetAsync(id, ct);
    }

    public async Task DeleteAsync(long id, CancellationToken ct = default)
    {
        await _uow.Customers.DeleteAsync(id, ct);
    }

    private static void Normalize(Customer customer)
    {
        customer.Email = customer.Email.Trim().ToLowerInvariant();
        customer.FullName = customer.FullName.Trim();
        customer.Phone = string.IsNullOrWhiteSpace(customer.Phone) ? null : customer.Phone.Trim();
    }
}
