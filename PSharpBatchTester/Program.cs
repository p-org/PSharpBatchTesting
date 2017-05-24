using Microsoft.Azure.Batch;
using Microsoft.Azure.Batch.Auth;
using Microsoft.Azure.Batch.Common;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;
using PSharpBatchTestCommon;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PSharpBatchTester
{
    class Program
    {

        private static string JobId;

        //Config
        private static PSharpBatchConfig config;
        private static PSharpBatchAuthConfig authConfig;

        private static string LogSession;

        static void Main(string[] args)
        {
            if (args.Count() < 2)
            {
                if (args.Count() < 1)
                {
                    Console.WriteLine("No Config file path given");
                }
                Console.WriteLine("No auth config file path given.");
                return;
            }

            LogSession = Guid.NewGuid().ToString();

            try
            {
                //Get args
                string configFilePath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(args[0]));
                config = PSharpBatchConfig.LoadFromXML(configFilePath);
                //PSharpOperations.ParseConfig(config);

                if (config.RunLocally)
                {
                    LocalMain();
                    return;
                }

                string authConfigFilePath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(args[1]));
                authConfig = PSharpBatchAuthConfig.LoadFromXML(authConfigFilePath);

                //If it contains 3 args, then get the location of the test application
                if(args.Count() >= 3)
                {
                    for(int i = 2; i < args.Count(); i++)
                    {
                        if (args[i].StartsWith("/output:"))
                        {
                            config.OutputFolderPath = args[i].Substring("/output:".Length);
                        }
                        else if (args[i].StartsWith("/binaries:"))
                        {
                            config.PSharpBinariesFolderPath = args[i].Substring("/binaries:".Length);
                        }
                    }
                }

                //We call the async main so we can await on many async calls
                MainAsync().Wait();
            }
            catch(PSharpConfigValidateException psharpConfigException)
            {
                Console.WriteLine("Error parsing config values : "+ psharpConfigException.Message);
            }
            catch (AggregateException ae)
            {
                #region ExceptionLog
                Dictionary<string, string> errorProp = new Dictionary<string, string>();
                errorProp.Add("StackTrace", ae.StackTrace);
                errorProp.Add("Message", ae.Message);
                errorProp.Add("InnerStackTrace", ae.InnerException.StackTrace);
                errorProp.Add("InnerMessage", ae.InnerException.Message);
                Logger.LogEvents("Exception", errorProp); 
                #endregion

                Console.WriteLine();
                Console.WriteLine(ae.StackTrace);
                Console.WriteLine(ae.Message);
                if(ae.InnerException != null)
                {
                    Console.WriteLine(ae.InnerException.StackTrace);
                    Console.WriteLine(ae.InnerException.Message);
                }
                Console.WriteLine();
            }
            Console.WriteLine();
        }

        private static void LocalMain()
        {
            List<Process> ProcessList = new List<Process>();
            
            foreach (var tEntity in config.TestEntities)
            {
                foreach (var cEntity in tEntity.CommandEntities)
                {
                    for (int i = 0; i < cEntity.NumberOfParallelTasks; i++)
                    {
                        string FlagString;

                        if (string.IsNullOrEmpty(cEntity.SchedulingStratergy))
                        {
                            FlagString = String.Format("/test:{0} /i:{1} {2}", tEntity.ApplicationPath, cEntity.IterationsPerTask, cEntity.CommandFlags);
                        }
                        else
                        {
                            FlagString = String.Format("/test:{0} /i:{1} {2} /sch:{3}", tEntity.ApplicationPath, cEntity.IterationsPerTask, cEntity.CommandFlags, cEntity.SchedulingStratergy);
                        }
                        Console.WriteLine(FlagString);
                        ProcessStartInfo startInfo = new ProcessStartInfo(
                                config.PSharpBinariesFolderPath + "\\PSharpTester.exe", FlagString);
                        startInfo.UseShellExecute = false;

                        Process process = new Process();
                        process.StartInfo = startInfo;
                        process.StartInfo.RedirectStandardError = true;
                        ProcessList.Add(process);
                        process.Start();
                    }
                }
            }

            foreach (var p in ProcessList)
            {
                try
                {
                    p.WaitForExit();
                    string sr = p.StandardError.ReadToEnd();
                    var outputFolderPath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(config.OutputFolderPath));
                    File.WriteAllText(outputFolderPath +"\\out_" + p.Id + ".txt", sr);
                }
                catch (Exception ex) { }
            }
        }

        private static async Task MainAsync()
        {

            //Creating BatchOperations
            BatchOperations batchOperations = new BatchOperations(authConfig.BatchAccountName, authConfig.BatchAccountKey, authConfig.BatchAccountUrl);

            //Creating BlobOperations
            BlobOperations blobOperations = new BlobOperations(authConfig.StorageAccountName, authConfig.StorageAccountKey, config.BlobContainerExpiryHours);


            //Pool operations
            if (!(await batchOperations.CheckIfPoolExists(config.PoolId)))
            {

                //Upload the application and the dependencies to azure storage and get the resource objects.
                var nodeFiles = await blobOperations.UploadNodeFiles(config.PSharpBinariesFolderPath, config.PoolId);

                //Creating the pool
                await batchOperations.CreatePoolIfNotExistAsync
                    (
                       poolId: config.PoolId,
                       resourceFiles: nodeFiles,
                       numberOfNodes: config.NumberOfNodesInPool,
                       OSFamily: config.NodeOsFamily,
                       VirtualMachineSize: config.NodeVirtualMachineSize,
                       NodeStartCommand: PSharpBatchTestCommon.Constants.PSharpDefaultNodeStartCommand
                    );
            }

            string executingDirectory = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

            //Job Details
            string jobManagerFilePath = /*typeof(PSharpBatchJobManager.Program).Assembly.Location;*/ Path.Combine(executingDirectory, @".\PSharpBatchJobManager\PSharpBatchJobManager.exe");   // Data files for Job Manager Task
            string jobTimeStamp = PSharpBatchTestCommon.Constants.GetTimeStamp();
            JobId = config.JobDefaultId + jobTimeStamp;

            //Uploading the data files to azure storage and get the resource objects.
            var inputFilesDict = await blobOperations.UploadInputFilesFromTestEntities(config.TestEntities, config.PoolId, JobId);

            //Uploading JobManager Files
            var jobManagerFiles = await blobOperations.UploadJobManagerFiles(jobManagerFilePath, config.PoolId, JobId);

            await blobOperations.CreateOutputContainer(config.PoolId, JobId);
            var outputContainerSasUrl = blobOperations.GetOutputContainerSasUrl();

            //Creating the job
            await batchOperations.CreateJobAsync
                (
                    jobId: JobId,
                    poolId: config.PoolId,
                    resourceFiles: jobManagerFiles,
                    outputContainerSasUrl: outputContainerSasUrl
                );

            //Adding tasks
            await batchOperations.AddTasksFromTestEntities
                (
                    jobId: JobId,
                    taskIDPrefix: config.TaskDefaultId,
                    inputFilesDict: inputFilesDict,
                    TestEntities: config.TestEntities
                );


            //Monitor tasks
            var taskResult = await batchOperations.MonitorTasks
                (
                    jobId: JobId,
                    timeout: TimeSpan.FromHours(config.TaskWaitHours)
                );

            //Flush Log
            Logger.FlushLogs();

            var outputFolderPath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(config.OutputFolderPath));

            await blobOperations.DownloadOutputFiles(outputFolderPath);

            try
            {
                PSharpOperations.MergeOutputCoverageReport(outputFolderPath, Path.GetFullPath(Environment.ExpandEnvironmentVariables(config.PSharpBinariesFolderPath)));
            }
            catch(Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
            }

            //All task completed
            Console.WriteLine();
            //Console.Write("Delete job? [yes] no: ");
            //string response = Console.ReadLine().ToLower();
            if (/*response == "y" || response == "yes"*/ config.DeleteJobAfterDone)
            {
                await batchOperations.DeleteJobAsync(JobId);
            }
            Console.WriteLine();
            //Console.Write("Delete Containers? [yes] no: ");
            //response = Console.ReadLine().ToLower();
            if (/*response == "y" || response == "yes"*/config.DeleteContainerAfterDone)
            {
                await blobOperations.DeleteInputContainer();
                await blobOperations.DeleteJobManagerContainer();
                await blobOperations.DeleteOutputContainer();
            }
        }
    }
}
