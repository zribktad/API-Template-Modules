using BackgroundJobs.Persistence;
using BuildingBlocks.Infrastructure.EFCore.Repositories;

namespace BackgroundJobs.Repositories;

public sealed class JobExecutionRepository : RepositoryBase<JobExecution>, IJobExecutionRepository
{
    public JobExecutionRepository(BackgroundJobsDbContext dbContext)
        : base(dbContext) { }
}
