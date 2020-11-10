using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Quartz;
using Quartz.Impl;
using SelloutReportingService.Reports.ReportFromSQLQuery;
using Topshelf;

namespace SelloutReportingService.Services
{
    public class ReportingServiceControl : ServiceControl
    {
        private static readonly string JobDefinitionsFilePath =
            Path.Combine(Configuration.BasePath, "config", "JobDefinitions.json");

        private static readonly ILogger Log = Logger.Instance;

        private readonly List<ReportFromSQLQueryJobDefinition> _jobDefinitions;
        private IScheduler _scheduler;

        public ReportingServiceControl()
        {
            _jobDefinitions = new List<ReportFromSQLQueryJobDefinition>();
        }

        /// <summary>This method is called when the Windows service is started.</summary>
        /// <remarks>It needs to return true in less than 30 seconds to successfully start the service.</remarks>
        /// <param name="hostControl">Allows the service to control the host while running.</param>
        /// <returns>true if the service started successfully.</returns>
        public bool Start(HostControl hostControl)
        {
            Task.Run(() => StartScheduler(hostControl));
            return true;
        }

        /// <summary>This method is called when the Windows service is stopped.</summary>
        /// <remarks>It needs to return true in less than 30 seconds to successfully stop the service.</remarks>
        /// <param name="hostControl">Allows the service to control the host while running.</param>
        /// <returns>true if the service stopped successfully.</returns>
        public bool Stop(HostControl hostControl)
        {
            _scheduler.Shutdown().GetAwaiter().GetResult();
            return true;
        }

        /// <summary>Starts up the task scheduler.</summary>
        /// <remarks>
        /// Schedule this Task from <see cref="ServiceControl.Start" /> to run on the thread pool, because
        /// <see cref="ServiceControl.Start" /> has to finish executing in under 30 seconds for the Windows service
        /// to start up successfully.
        /// </remarks>
        /// <param name="hostControl">Allows the service to control the host while running.</param>
        private async Task StartScheduler(HostControl hostControl)
        {
            _scheduler ??= await new StdSchedulerFactory().GetScheduler();

            try
            {
                Log.LogInformation("Loading job definitions from '{}'.", JobDefinitionsFilePath);

                await LoadJobDefinitions(JobDefinitionsFilePath);

                Log.LogInformation("Job definitions have been loaded.");
            }
            catch (Exception e)
            {
                Log.LogError(e, "Failed to load job definitions from '{}'.", JobDefinitionsFilePath);
                Log.LogError("Cannot start service.");
                hostControl.Stop();
                return;
            }

            var jobsScheduled = 0;

            foreach (var jobDefinition in _jobDefinitions)
                try
                {
                    var jobDetail = JobBuilder
                        .Create<ReportFromSQLQueryJob>()
                        .WithIdentity(jobDefinition.JobId)
                        .UsingJobData(new JobDataMap {{nameof(ReportFromSQLQueryJobDefinition), jobDefinition}})
                        .Build();
                    var trigger = TriggerBuilder
                        .Create()
                        .WithIdentity(jobDefinition.JobId)
                        .WithCronSchedule(jobDefinition.CronTrigger)
                        .StartNow()
                        .Build();

                    await _scheduler.ScheduleJob(jobDetail, trigger);
                    jobsScheduled += 1;

                    Log.LogInformation("Job '{}' has been scheduled. First fire at '{}'.",
                        jobDefinition.JobId,
                        trigger.GetNextFireTimeUtc()?.ToLocalTime());
                }
                catch (Exception e)
                {
                    Log.LogError(e, "Failed to schedule job '{}'.", jobDefinition.JobId);
                }

            if (jobsScheduled == 0)
            {
                Log.LogError("No jobs scheduled.");
                Log.LogError("Cannot start service.");
                hostControl.Stop();
            }
            else
            {
                Log.LogInformation("{} jobs in total have been scheduled.", jobsScheduled);
                Log.LogInformation("Starting task scheduler '{}'.", _scheduler.SchedulerName);

                await _scheduler.Start();

                Log.LogInformation("Task scheduler '{}' is now running.", _scheduler.SchedulerName);
            }
        }

        private async Task LoadJobDefinitions(string filePath)
        {
            if (_jobDefinitions.Count > 0) throw new Exception("Job definitions have already been loaded.");

            using (var stream = File.OpenRead(filePath))
            {
                var definitions = await JsonSerializer.DeserializeAsync<ReportFromSQLQueryJobDefinition[]>(stream);
                _jobDefinitions.AddRange(definitions);
            }
        }
    }
}