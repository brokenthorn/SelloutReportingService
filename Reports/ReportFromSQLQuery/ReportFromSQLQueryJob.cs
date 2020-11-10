using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Quartz;
using Renci.SshNet;
using WrapFTP;

namespace SelloutReportingService.Reports.ReportFromSQLQuery
{
    public class ReportFromSQLQueryJob : IJob
    {
        private static readonly ILogger Log = Logger.Instance;

        public async Task Execute(IJobExecutionContext context)
        {
            var jobId = context.JobDetail.Key.Name;

            Log.LogInformation($"{jobId}: started.");

            #region Prepare task execution

            const string jobDefinitionJobDataMapKey = nameof(ReportFromSQLQueryJobDefinition);
            var jobDefinition =
                (ReportFromSQLQueryJobDefinition) context.JobDetail.JobDataMap.Get(jobDefinitionJobDataMapKey);
            if (jobDefinition == null)
            {
                Log.LogError(
                    $"{jobId}: failed to start. JobDataMap returned null for key = '{jobDefinitionJobDataMapKey}'.");
                return;
            }

            #endregion

            #region Global vars

            var localReportFilePath = Path.GetFullPath(jobDefinition.ReportFilePath);
            var localReportFileName = Path.GetFileName(localReportFilePath);
            var isZipped = jobDefinition.IsZipped;
            var sqlQuery = jobDefinition.SqlQuery;
            var uploadDirectives = jobDefinition.UploadDirectives;
            var reportFileFormat = jobDefinition.ReportFileFormat;
            // TODO: Implement notification via Email
            // var notifyEmailAddress = jobDefinition.NotifyEmailAddress;
            var sqlCommandTimeout = jobDefinition.SqlCommandTimeout;
            var msSqlConnectionString = jobDefinition.MsSqlConnectionString;

            #endregion

            #region Generate Report File

            try
            {
                await using (var sqlConnection = new SqlConnection(msSqlConnectionString))
                {
                    await sqlConnection.OpenAsync();

                    #region SQL Data Reader writing to report output file

                    await using (var cmd = new SqlCommand(sqlQuery, sqlConnection)
                    {
                        CommandTimeout = sqlCommandTimeout,
                        CommandType = CommandType.Text
                    })
                    {
                        var temporaryLocalReportFilePath = localReportFilePath + ".tmp";

                        Log.LogDebug($"{jobId}: Started executing its SQL Query.");
                        await using (var localReportFileStream = File.Create(temporaryLocalReportFilePath))
                        await using (var sqlDataReader = await cmd.ExecuteReaderAsync())
                        {
                            Log.LogDebug($"{jobId}: Finished executing its SQL Query.");

                            var encoder = new UTF8Encoding(true);
                            var newLine = Environment.NewLine;
                            var separator = reportFileFormat switch
                            {
                                ReportFileFormat.Csv => ',',
                                ReportFileFormat.Tsv => '\t',
                                _ => ','
                            };

                            Log.LogDebug(
                                $"{jobId}: Started writing results to temporary file '{temporaryLocalReportFilePath}'.");
                            while (await sqlDataReader.ReadAsync())
                            {
                                var record = new string[sqlDataReader.FieldCount];
                                for (var i = 0; i < sqlDataReader.FieldCount; i++)
                                {
                                    var value = sqlDataReader.IsDBNull(i) ? "" : sqlDataReader.GetString(i);
                                    // Escape double quotes within CSV fields by preceding with another double quote (RFC-4180):
                                    if (value.Contains('"')) value = value.Replace("\"", "\"\"");
                                    record[i] = $"\"{value}\"";
                                }

                                var row = string.Join(separator, record) + newLine;
                                var bytes = encoder.GetBytes(row);
                                await localReportFileStream.WriteAsync(bytes, 0, bytes.Length);
                            }

                            Log.LogDebug(
                                $"{jobId}: Finished writing results to temporary file '{temporaryLocalReportFilePath}'.");
                        }

                        if (isZipped)
                        {
                            Log.LogDebug(
                                $"{jobId}: Started compressing temporary file '{temporaryLocalReportFilePath}'.");
                            await using (var tempFileStream = File.Open(temporaryLocalReportFilePath, FileMode.Open))
                            {
                                if (File.Exists(localReportFilePath)) File.Delete(localReportFilePath);

                                using (var zipArchive = ZipFile.Open(localReportFilePath, ZipArchiveMode.Create))
                                {
                                    var zipArchiveEntry =
                                        zipArchive.CreateEntry(localReportFileName, CompressionLevel.Optimal);
                                    await using (var zipEntryStream = zipArchiveEntry.Open())
                                    {
                                        await tempFileStream.CopyToAsync(zipEntryStream);
                                    }
                                }
                            }

                            File.Delete(temporaryLocalReportFilePath);
                            Log.LogDebug(
                                $"{jobId}: Finished compressing temporary file '{temporaryLocalReportFilePath}' to '{localReportFilePath}'. Temporary file has been deleted.");
                        }
                        else
                        {
                            File.Move(temporaryLocalReportFilePath, localReportFilePath, true);
                            Log.LogDebug(
                                $"{jobId}: Renamed temporary file '{temporaryLocalReportFilePath}' to '{localReportFilePath}'. Any existing file has been overwritten.");
                        }
                    }

                    #endregion
                }
            }
            catch (Exception e)
            {
                Log.LogError(e, $"{jobId}: failed to generate the report file.");
                return;
            }

            #endregion

            #region Upload Report File to FTP and SFTP servers

            foreach (var uploadDirective in uploadDirectives)
            {
                var host = uploadDirective.Host;
                var port = uploadDirective.Port;
                var isSftp = uploadDirective.IsSftp;
                var useSsl = uploadDirective.UseSsl;
                var passiveMode = uploadDirective.PassiveMode;
                var userName = uploadDirective.UserName;
                var password = uploadDirective.Password;
                var folderPath = uploadDirective.FolderPath;
                if (!folderPath.StartsWith('/')) folderPath = '/' + folderPath;
                var filePath = Path.Join(folderPath, localReportFileName);

                if (isSftp)
                    try
                    {
                        Log.LogInformation($"{jobId}: uploading {localReportFilePath} to sftp://{host}{filePath}.");
                        var client = new SftpClient(host, port, userName, password);
                        client.Connect();

                        await using (var fileStream = File.OpenRead(localReportFilePath))
                        await using (var sftStream = client.OpenWrite(filePath))
                        {
                            await fileStream.CopyToAsync(sftStream);
                        }

                        // BUG: Memory Leak. Unfortunately we cannot call client.Disconnect(), or make use of IDispose, because it would freeze: https://github.com/sshnet/SSH.NET/issues/741
                        Log.LogInformation(
                            $"{jobId}: upload of {localReportFilePath} to sftp://{host}{filePath} finished.");
                    }
                    catch (Exception e)
                    {
                        Log.LogError(e,
                            $"{jobId}: failed upload of {localReportFilePath} to sftp://{host}{filePath}.");
                    }
                else // not an SFTP server
                    try
                    {
                        Log.LogInformation($"{jobId}: uploading {localReportFilePath} to ftp://{host}{filePath}.");
                        var client = new FtpClient(host, port, userName, password, -1, passiveMode, useSsl);
                        await using (var fileStream = File.OpenRead(localReportFilePath))
                        {
                            await client.Upload(fileStream, filePath);
                        }

                        Log.LogInformation(
                            $"{jobId}: upload of {localReportFilePath} to ftp://{host}{filePath} finished.");
                    }
                    catch (Exception e)
                    {
                        Log.LogError(e,
                            $"{jobId}: failed upload of {localReportFilePath} to ftp://{host}{filePath}.");
                    }
            }

            #endregion

            Log.LogInformation($"{jobId}: finished.");
        }

        /// <summary>
        /// Replaces "{...}" placeholders in the input string with strings outputted by DateTime.Now.ToString(...).
        /// </summary>
        /// <param name="s">The input string containing DateTime placeholders</param>
        /// <returns>A string with the placeholders replaced by formatted DateTime parts</returns>
        /// <exception cref="FormatException">
        /// thrown when the input string does not contain proper opening and closing curly
        /// bracket pairs.
        /// </exception>
        private static string ReplaceDateTimePlaceholder(string s)
        {
            var currentLeftCurlyIndex = s.IndexOf('{');
            var currentRightCurlyIndex = s.IndexOf('}');

            // no placeholders found so return the same string early:
            if (currentLeftCurlyIndex < 0 && currentRightCurlyIndex < 0) return s;

            var strLength = s.Length;
            var leftCurlyIndexes = new List<int>();
            var rightCurlyIndexes = new List<int>();

            // find and store left curly indexes:
            while (currentLeftCurlyIndex >= 0)
            {
                leftCurlyIndexes.Add(currentLeftCurlyIndex);
                var notAtEnd = currentLeftCurlyIndex + 1 < strLength;
                if (notAtEnd)
                    currentLeftCurlyIndex = s.IndexOf('{', currentLeftCurlyIndex + 1);
                else
                    currentLeftCurlyIndex = -1;
            }

            // find and store right curly indexes:
            while (currentRightCurlyIndex >= 0)
            {
                rightCurlyIndexes.Add(currentRightCurlyIndex);
                var notAtEnd = currentRightCurlyIndex + 1 < strLength;
                if (notAtEnd)
                    currentRightCurlyIndex = s.IndexOf('}', currentRightCurlyIndex + 1);
                else
                    currentRightCurlyIndex = -1;
            }

            if (leftCurlyIndexes.Count != rightCurlyIndexes.Count)
                throw new FormatException("The number of opening and closing curly brackets is not equal.");

            var fmtTuples = new List<(int Left, int Right, string Fmt)>();

            // fill fmtTuples:
            for (var i = 0; i < leftCurlyIndexes.Count; i++)
            {
                // reusing temp vars:
                (currentLeftCurlyIndex, currentRightCurlyIndex) = (leftCurlyIndexes[i], rightCurlyIndexes[i]);

                if (currentLeftCurlyIndex > currentRightCurlyIndex)
                    throw new FormatException(
                        "Opening and closing brackets don't form correct pairs: " +
                        $"right curly bracket at position {currentRightCurlyIndex} is wrongfully preceding " +
                        $"left curly bracket at position {currentLeftCurlyIndex}.");

                var textBetweenCurlyBrackets = s
                    .Substring(currentLeftCurlyIndex + 1, currentRightCurlyIndex - currentLeftCurlyIndex - 1);
                fmtTuples.Add((currentLeftCurlyIndex, currentRightCurlyIndex, textBetweenCurlyBrackets));
            }

            var result = string.Empty;

            // fill result:
            for (var i = 0; i < fmtTuples.Count; i++)
            {
                currentLeftCurlyIndex = fmtTuples[i].Left;
                var formattedString = DateTime.Now.ToString(fmtTuples[i].Fmt);

                var textLeftOfCurrentPlaceholder = string.Empty;

                // fill textLeftOfCurrentPlaceholder:
                if (currentLeftCurlyIndex != 0)
                {
                    if (i == 0)
                    {
                        // the textLeftOfPlaceholder should be the text
                        // between s[0] up to the left curly:
                        textLeftOfCurrentPlaceholder = s.Substring(0, currentLeftCurlyIndex);
                    }
                    else
                    {
                        var previousFmtTuple = fmtTuples[i - 1];
                        // the textLeftOfPlaceholder should be the text
                        // between the previous placeholder and the current one:
                        textLeftOfCurrentPlaceholder =
                            s.Substring(previousFmtTuple.Right + 1,
                                currentLeftCurlyIndex - previousFmtTuple.Right - 1);
                    }
                }

                result += textLeftOfCurrentPlaceholder + formattedString;
            }

            return result;
        }
    }
}