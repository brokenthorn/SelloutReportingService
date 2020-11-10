# SelloutReportingService
A Windows service that generates and sends custom reports using SQL Server and Cron Trigger expressions for scheduling.

## Reasoning and use case
I needed a way to periodically generate custom reports by querying a SQL Server and upload the data to a file server.

## How to Build, Install and Use

1. Clone this repository using Git or download a zipped version from GitHub.
1. Install the [.Net Core 3.1 SDK](https://dotnet.microsoft.com/download/dotnet-core/3.1) if you don't have it already.
1. Open a command line and navigate to the source code folder and run:

   ```sh
   $dotnet publish -r win-x64 -c Release
   ```

   If you don't like publishing the app with so many DLLs and other files in the
   same folder, you can try publishing as a single file, which will zip all the
   dependencies together in the EXE and unzip it to a temp folder at runtime
   (initial startup time will be slower):

   ```sh
   $dotnet publish -r win-x64 -c Release /p:PublishSingleFile=true
   ```

1. Move the generated EXE (and any other generated files) to your folder of
   preference. There is no installer.
1. Make sure you create the configuration file `config/JobDefinitions.json`.
   See [Configuration](#Configuration) below.
1. Install and start the service by running:

   ```sh
   $ SelloutReportingService.exe install
   $ SelloutReportingService.exe start
   ```

1. Check the Windows Event Viewer for log messages from the service and any errors.
   You can also run the service executable from the command line and see log
   messages outputted directly to the console by just calling the executable
   with no arguments.

## Configuration
Create a `config/JobDefinitions.json` file. Follow the example `config/JobDefinitions.example.json` file:

```json
[
  {
    "JobId": "Example Job running every 1 minute",
    "CronTrigger": "0 * * ? * * *",
    "MsSqlConnectionString": "Server=localhost,1433;User Id=USENAME;Password=PASSWORD;Connection Timeout=10",
    "SqlQuery": "EXEC DATABASE.dbo.SomeProcedureThatReturnsOneResultSet;",
    "SqlCommandTimeout": 900,
    "AlertEmailAddress": "YOU@YOURDOMAIN.COM",
    "ReportFileFormat": "CSV",
    "ReportFilePath": "OUTPUT_FILE_PATH.csv.zip",
    "IsZipped": true,
    "UploadDirectives": [
      {
        "Host": "FTP.YOURSITE.COM",
        "Port": 21,
        "IsSftp": false,
        "PassiveMode": true,
        "UseSsl": false,
        "UserName": "FTP_USERNAME",
        "Password": "FTP_PASSWORD",
        "FolderPath": "/in/"
      },
      {
        "Host": "SFTP.YOURSITE.COM",
        "Port": 22,
        "IsSftp": true,
        "UserName": "SFTP_USERNAME",
        "Password": "SFTP_PASSWORD",
        "FolderPath": "/in/"
      }
    ]
  }
]
```

The example configuration file defines one job called `Example Job running every 1 minute` that runs once every minute,
as defined by the `CronTrigger` expression `0 * * ? * * *`.
See [Qartz's Cron Trigger Tutorial](http://www.quartz-scheduler.org/documentation/quartz-2.3.0/tutorials/crontrigger.html)
for more information on how to define custom schedules.

The job connects to a SQL Server using the specified connection string, runs the specified query, saves the resultset to a CSV file,
which can optionally be zipped, and finally uploads it to one or more FTP and/or SFTP servers.

## License
Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
