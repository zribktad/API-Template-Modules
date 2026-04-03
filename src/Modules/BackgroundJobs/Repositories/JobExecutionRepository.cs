using BackgroundJobs.Shared;
using BackgroundJobs.Persistence;
using SharedKernel.Infrastructure.Repositories;

namespace BackgroundJobs.Repositories;

public sealed class JobExecutionRepository : RepositoryBase<JobExecution>, IJobExecutionRepository
{
    public JobExecutionRepository(BackgroundJobsDbContext dbContext)
        : base(dbContext) { }
}



