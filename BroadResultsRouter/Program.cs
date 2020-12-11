using System;
using System.IO;
using Microsoft.Extensions.Configuration;
using Google.Cloud.Storage.V1;
using Google.Apis.Auth.OAuth2;
using Serilog;
using CsvHelper;
using System.Globalization;
using System.Collections.Generic;

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
                    //Log.Warning($"Found csv results file {storageObject.Name}");
                    if(!File.Exists(saveFilePath)) //not already downloaded....
                    {
                        using (var outputFile = File.OpenWrite(saveFilePath))
                        {
                            try
                            {
                                storage.DownloadObject(downloadBucket, storageObject.Name, outputFile);
                                msg = $"Downloaded csv results file: {storageObject.Name}";
                                Console.WriteLine(""); 
                                Log.Warning(msg);
                            }
                            catch (Exception wtf)
                            {
                                Log.Error(wtf.Message);
                            }
                            
                        }

                        //Check the file for formatting errors...most common ones are times included with dates and no leading zeroes on MRNs. 
                        Log.Information("Commencing error checking routines...");
                        bool clean = CheckForErrors(saveFilePath);

                        //copy the new results to the share location for GA to pickup.
                        try
                        {
                            File.Copy(saveFilePath, Path.Combine(dropBoxPath, Path.GetFileName(saveFilePath)));
                            Log.Warning($"Successfully copied csv results file to {dropBoxPath}");
                        }
                        catch (Exception wtf)
                        {
                            Log.Error(wtf.Message);
                        }
                        
                    } else
                    {
                        Console.Write('.');
                    }
                }
            }
            Console.WriteLine("");
            Log.Information("BroadResultsRouter has completed!");
        }

        static bool CheckForErrors(string inputFile)
        {
            bool clean = true;
            var records = new List<ResultRow>();
            Log.Information($"Consuming csv results file: {inputFile}");
            using (var reader = new StreamReader(inputFile))
            {
                using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
                {
                    records.AddRange(csv.GetRecords<ResultRow>());
                }
            }
            Log.Information($"I just ate {records.Count} results! YUM!");
            foreach(var row in records)
            {
                if(row.patient_id.Length < 10)
                {
                    row.patient_id = row.patient_id.PadLeft(10, '0');
                    Log.Warning($"Found an incorrect MRN and reformatted it: {row.patient_id}");
                    clean = false;
                }

                if (row.time_collected.Contains(":"))
                {
                    string badDate = row.time_collected;
                    row.time_collected = Convert.ToDateTime(row.time_collected).ToShortDateString();
                    Log.Warning($"Found an incorrect Date {badDate} and reformatted it: {row.time_collected}");
                    clean = false;
                }

                //Ensure date/time format for time_completed is something the IEngine can swallow.  MUST BE THIS FORMAT:  MM/dd/yyyy HH:mm
                DateTime newDate;

                if (!DateTime.TryParseExact(row.time_completed, "MM/dd/yyyy HH:mm", CultureInfo.CurrentCulture, DateTimeStyles.None, out newDate))
                {
                    var origTC = row.time_completed;
                    row.time_completed = Convert.ToDateTime(row.time_completed).ToString("MM/dd/yyyy HH:mm");
                    Log.Information($"Time Completed CONVERTED from {origTC} to {row.time_completed}");
                }
            }

            Log.Warning($"Rewriting results file {inputFile} with corrected values!");
            using (var writer = new StreamWriter(inputFile))
            {
                using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
                {
                    csv.WriteRecords(records);
                }
            }
            Log.Information("Rewrite complete!");
            return clean;
        }
    }
}
