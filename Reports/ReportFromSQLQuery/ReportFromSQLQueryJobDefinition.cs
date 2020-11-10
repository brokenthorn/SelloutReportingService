using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SelloutReportingService.Reports.ReportFromSQLQuery
{
    /// <summary>
    /// A Job Definition for creating a report by running a SQL Query.
    /// </summary>
    [Serializable]
    public class ReportFromSQLQueryJobDefinition
    {
        /// <summary>
        /// A unique Job ID or Name.
        /// </summary>
        public string JobId { get; set; }

        /// <summary>
        /// A Quartz Scheduler Cron Expression representing the frequency with which to run the job.
        /// </summary>
        /// <remarks>
        /// See
        /// <a href="http://www.quartz-scheduler.org/documentation/quartz-2.3.0/tutorials/crontrigger.html">Cron Triggers</a>.
        /// </remarks>
        public string CronTrigger { get; set; }

        /// <summary>
        /// Microsoft SQL Server connection string.
        /// </summary>
        /// <remarks>
        /// See
        /// <a href="https://docs.microsoft.com/en-us/sql/odbc/reference/develop-app/connection-strings">Connection Strings</a>.
        /// </remarks>
        public string MsSqlConnectionString { get; set; }

        /// <summary>
        /// SQL query string to execute in order to produce the report data.
        /// </summary>
        public string SqlQuery { get; set; }

        /// <summary>
        /// The maximum client timeout in seconds to wait for the <see cref="SqlQuery" /> to finish executing.
        /// Use this to prevent long running queries from locking up the server. The default is 60 seconds.
        /// </summary>
        /// <remarks>
        /// Also notice that setting this too low can cause the query to timeout before it can complete
        /// and no results will be returned if this happens.
        /// </remarks>
        public int SqlCommandTimeout { get; set; } = 60;

        /// <summary>
        /// Send notifications and alerts to this email address.
        /// </summary>
        public string NotifyEmailAddress { get; set; }

        /// <summary>
        /// The local file path where the report should be saved.
        /// </summary>
        public string ReportFilePath { get; set; }

        /// <summary>
        /// Is the <see cref="ReportFilePath" /> file zipped?
        /// </summary>
        /// <remarks>
        /// Set this to <c>true</c> and add a .zip suffix to <see cref="ReportFilePath" />, if you want the report to be
        /// compressed with ZIP.
        /// </remarks>
        public bool IsZipped { get; set; }

        /// <summary>
        /// The file format to use for the uncompressed (see <see cref="IsZipped" />) report file.
        /// </summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public ReportFileFormat ReportFileFormat { get; set; }

        /// <summary>
        /// A list of FTP and SFTP upload directives to upload the <see cref="ReportFilePath" /> file to.
        /// </summary>
        public List<UploadDirective> UploadDirectives { get; set; }
    }
}