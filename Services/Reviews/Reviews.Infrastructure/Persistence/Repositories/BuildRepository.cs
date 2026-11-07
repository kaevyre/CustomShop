using Reviews.Domain.Entities;
using Reviews.Domain.Interfaces;

namespace Reviews.Infrastructure.Persistence.Repositories;

public sealed class BuildRepository(MongoDbContext context)
    : MongoRepositoryBase<Build>(context.Builds), IBuildRepository;
