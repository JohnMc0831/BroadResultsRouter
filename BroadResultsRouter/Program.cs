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

            CheckForResults(@"D:\BroadResultsRouter\test.csv");
        }

        static void CheckForResults(string path = "")
        {
            //const string dropBoxPath = @"\\fahc.fletcherallen.org\shared\Apps\Epic Build\Broad";
            const string dropBoxPath = @"D:\Broad";
            string msg = string.Empty;

            if (!string.IsNullOrEmpty(path))
            {
                //Just for testing; Don't pull from google but take hardcoded path/file instead.
                //copy the new results to the share location for GA to pickup.
                try
                {

                    //TODO:  Rewrite names
                    string saveFilePath = path;
                    string tmpFile = Path.Combine(Path.GetTempPath(), "test.csv");
                    File.Move(saveFilePath, tmpFile, true);
                    FixMrns(tmpFile, saveFilePath);
                    File.Copy(saveFilePath, Path.Combine(dropBoxPath, Path.GetFileName(saveFilePath)));
                    msg = $"Successfully copied csv results file to {dropBoxPath}";
                    Log.Warning(msg);
                }
                catch (Exception wtf)
                {
                    Log.Error(wtf.Message);
                }
                return;
            }

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
                            //TODO:  Rewrite names
                            string tmpFile = Path.Combine(Path.GetDirectoryName(saveFilePath), "TEMP.CSV");
                            File.Move(saveFilePath, tmpFile);
                            FixMrns(tmpFile, saveFilePath);
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
            Log.Information("BroadResultsRouter has completed!");
        }

        public static void FixMrns(string inputFile, string outputFile)
        {
            Log.Information("MRN Checker has initiated a scan!");
            int counter = 0;
            string line;
            bool badMrnFormatFound = false;
            // create text file for edited output
            using (System.IO.StreamWriter finalfile = new System.IO.StreamWriter(outputFile))
            {
                // Read the file and display it line by line.  
                System.IO.StreamReader file = new System.IO.StreamReader(inputFile);
                while ((line = file.ReadLine()) != null)
                {
                    //System.Console.WriteLine(line);
                    Log.Information($"READ: {line}");
                    var parsedLine = line.Split(",");
                    badMrnFormatFound = parsedLine[1].Length < 10;
                    var fixedMrn = parsedLine[1].Length < 10 ? parsedLine[1].PadLeft(10, '0') : parsedLine[1];
                    string finalLine = $"{parsedLine[0]},{fixedMrn},{parsedLine[2]},{parsedLine[3]},{parsedLine[4]},{parsedLine[5]},{parsedLine[6]},{parsedLine[7]},{parsedLine[8]},{parsedLine[9]},{parsedLine[10]}";
                    finalfile.WriteLine(finalLine);
                    Log.Information($"WROTE: {finalLine}");
                    counter++;                   
                }
                Log.Warning("PLEASE NOTE:  It appears that the patient IDs (MRNs) are not correctly formatted!");
                Log.Information($"Wrote {counter} lines to new file {inputFile}.");
                file.Close();
            }
        }
    }
}
