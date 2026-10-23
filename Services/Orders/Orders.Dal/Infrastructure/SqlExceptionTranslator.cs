using System.Diagnostics.CodeAnalysis;
using Microsoft.Data.SqlClient;
using Orders.Domain.Exceptions;

namespace Orders.Dal.Infrastructure;

public static class SqlExceptionTranslator
{
    public static bool TryTranslate(SqlException ex, [NotNullWhen(true)] out DomainException? domain)
    {
        domain = ex.Number switch
        {
            50001 or 50006 or 50010 => new NotFoundException(ex.Message, ex),

            50002 or 50003 or 50004 or 50005 or 50007 or 50008 or 50011
                => new BusinessConflictException(ex.Message, ex),

            50009 or 50012 => new DomainValidationException(ex.Message, ex),

            2601 or 2627 => new BusinessConflictException("A record with the same unique value already exists.", ex),

            547 => new BusinessConflictException("The operation violates a database constraint.", ex),

            _ => null
        };

        return domain is not null;
    }
}
