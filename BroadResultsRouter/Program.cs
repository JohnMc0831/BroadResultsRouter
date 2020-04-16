using System;
using System.IO;
using Microsoft.Extensions.Configuration;
using Google.Cloud.Storage.V1;
using Google.Apis.Auth.OAuth2;
using Serilog;

namespace BroadResultsRouter
{
    class Program
    {
        static void Main(string[] args)
        {
            SetupStaticLogger();
        }
        private static void SetupStaticLogger()
        {
            var configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .Build();

            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(configuration)
                .CreateLogger();

            CheckForResults();
        }

        static void CheckForResults()
        {
            string msg = string.Empty;
            const string dropBoxPath = @"\\fahc.fletcherallen.org\shared\Apps\Epic Build\Broad";
            msg = $"New results files will be copied to {dropBoxPath}";
            Log.Warning(msg);
            //pull each results csv file and check if it is in the results table in the db.  If not, add it.
            var downloadBucket = "vt-reports-prod";
            string objectName = string.Empty;
            var cred = GoogleCredential.FromFile(@".\broadorders-7736fb458349.json");
            var storage = StorageClient.Create(cred);
            foreach (var storageObject in storage.ListObjects(downloadBucket, ""))
            {
                //pull it...
                if (!storageObject.Name.Contains("pdfs", StringComparison.OrdinalIgnoreCase)) //we just want the csv files.
                {
                    var appRoot = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                    var saveFilePath = Path.Combine(appRoot, "priorResults", storageObject.Name);
                    Log.Warning($"Found csv results file {storageObject.Name}");
                    if(!File.Exists(saveFilePath)) //not already downloaded....
                    {
                        using (var outputFile = System.IO.File.OpenWrite(saveFilePath))
                        {
                            try
                            {
                                storage.DownloadObject(downloadBucket, storageObject.Name, outputFile);
                                msg = @"Downloaded csv results file {storageObject.Name}";
                                Log.Warning(msg);
                            }
                            catch (Exception wtf)
                            {
                                Log.Error(wtf.Message);
                            }
                            
                        }
                        //copy the new results to the share location for GA to pickup.
                        try
                        {
                            File.Copy(saveFilePath, Path.Combine(dropBoxPath, Path.GetFileName(saveFilePath)));
                             msg = $"Successfully copied csv results file to {dropBoxPath}";
                            Log.Warning(msg);
                        }
                        catch (Exception wtf)
                        {
                            Log.Error(wtf.Message);
                        }
                        
                    } else
                    {
                        var fname = Path.GetFileName(saveFilePath);
                        Log.Warning($"Skipping already downloaded file {fname}");
                    }
                }
            }
            Log.Warning("BroadResultsRouter has completed!");
        }
    }
}
