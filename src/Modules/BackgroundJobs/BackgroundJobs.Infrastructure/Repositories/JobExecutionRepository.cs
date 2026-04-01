using BackgroundJobs.Domain;
using BackgroundJobs.Infrastructure.Persistence;
using SharedKernel.Infrastructure.Repositories;

namespace BackgroundJobs.Infrastructure.Repositories;

public sealed class JobExecutionRepository : RepositoryBase<JobExecution>, IJobExecutionRepository
{
    public JobExecutionRepository(BackgroundJobsDbContext dbContext)
        : base(dbContext) { }
}
