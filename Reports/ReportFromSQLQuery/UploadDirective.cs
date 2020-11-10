using System;

namespace SelloutReportingService.Reports.ReportFromSQLQuery
{
    /// <summary>
    /// A directive containing information needed to connect to a FTP or SFTP server
    /// and a root folder where files should be uploaded to.
    /// </summary>
    [Serializable]
    public class UploadDirective
    {
        /// <summary>
        /// S/FTP server hostname or IP address.
        /// </summary>
        public string Host { get; set; }

        /// <summary>
        /// S/FTP server port number.
        /// </summary>
        /// <remarks>
        /// FTP servers usually listens on port 21,
        /// while SFTP servers usually listens on port 22, but some SFTP servers also listen on port 21.
        /// </remarks>
        public int Port { get; set; }

        /// <summary>
        /// Is the server an SFTP server (or just a normal FTP server)? Defaults to <c>false</c>.
        /// </summary>
        public bool IsSftp { get; set; } = false;

        /// <summary>
        /// Use SSL when connecting to the FTP server? Defaults to <c>false</c>.
        /// </summary>
        public bool UseSsl { get; set; } = false;

        /// <summary>
        /// Use Passive Mode when connecting to the FTP server? Defaults to <c>true</c>.
        /// </summary>
        public bool PassiveMode { get; set; } = true;

        /// <summary>
        /// User name to use for authentication.
        /// </summary>
        public string UserName { get; set; }

        /// <summary>
        /// Password to use for authentication.
        /// </summary>
        public string Password { get; set; }

        /// <summary>
        /// The remote folder path to upload files to. Defaults to "/".
        /// </summary>
        public string FolderPath { get; set; } = "/";
    }
}