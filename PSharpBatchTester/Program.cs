﻿using Microsoft.Azure.Batch;
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
            LogSession = Guid.NewGuid().ToString();

            try
            {

                ParseArgs(args);

                config.ValidateAndParse();
                if (config.RunLocally)
                {
                    LocalMain();
                    return;
                }

                if (!args.Any(v => v.StartsWith("/auth:")))
                {
                    Console.WriteLine("No auth config file path given.");
                    return;
                }

                if (!config.MonitorBatch || (config.MonitorBatch && string.IsNullOrEmpty(config.BatchFilePath)))
                {
                    //We call the async main so we can await on many async calls
                    MainAsync().Wait();
                    return;
                }

                if (config.MonitorBatch)
                {
                    if(string.IsNullOrEmpty(config.BatchFilePath))
                    {
                        Console.WriteLine("Please provide a psbatch file to monitor.");
                        return;
                    }

                    var batchJob = BatchJob.LoadFromXML(config.BatchFilePath);
                    MonitorAsync(batchJob).Wait();
                }
            }
            catch(PSharpConfigValidateException psharpConfigException)
            {
                Console.WriteLine("Error parsing config values : "+ psharpConfigException.Message);
            }
            catch (PSharpException psharpException)
            {
                Console.WriteLine(psharpException.Message);
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

        private static void ParseArgs(string[] args)
        {
            // Throw error if config files are not found
            if(!args.Any(v => v.StartsWith("/config:")))
            {
                throw new PSharpException("No Config file path provided");
            }
            if(!args.Any(v => v.StartsWith("/auth:")))
            {
                throw new PSharpException("No Auth Config file path provided");
            }

            // Parsing the BatchConfig file first
            var configArg = args.Where(arg => arg.StartsWith("/config:")).First();
            string configFilePath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(configArg.Substring("/config:".Length)));
            config = PSharpBatchConfig.LoadFromXML(configFilePath);

            // Parsing the other arguments.
            for (int i = 1; i < args.Count(); i++)
            {
                if (args[i].StartsWith("/auth:"))
                {
                    string authConfigFilePath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(args[1].Substring(6)));
                    authConfig = PSharpBatchAuthConfig.LoadFromXML(authConfigFilePath);
                }
                else if (args[i].StartsWith("/output:"))
                {
                    config.OutputFolderPath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(args[i].Substring("/output:".Length)));
                }
                else if (args[i].StartsWith("/binaries:"))
                {
                    config.PSharpBinariesFolderPath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(args[i].Substring("/binaries:".Length)));
                }
                else if (args[i].StartsWith("/local"))
                {
                    config.RunLocally = true;
                }
                else if (args[i].StartsWith("/declare:"))
                {
                    var words = args[i].Substring("/declare:".Length).Split('=');
                    if (!PSharpBatchConfig.DeclareDictionary.ContainsKey(words[0]))
                        PSharpBatchConfig.DeclareDictionary.Add(words[0], words[1]);
                    else
                        PSharpBatchConfig.DeclareDictionary[words[0]] = words[1];
                }
                else if (args[i].StartsWith("/monitor"))
                {
                    config.MonitorBatch = true;
                }
                else if (args[i].StartsWith("/psbatch:"))
                {
                    config.BatchFilePath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(args[i].Substring("/psbatch:".Length)));
                }
            }
        }

        private static void LocalMain()
        {
            int NumPassedTests = 0;
            int NumFailedTests = 0;
            int NumTotalTests = 0;
            int NumCrashedTests = 0;
            double ElapsedTime = 0;

            var outputFolderPath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(config.OutputFolderPath));
            if (!Directory.Exists(outputFolderPath))
            {
                Console.WriteLine("Output directory doesn't exists. Creating directory : "+outputFolderPath);
                Directory.CreateDirectory(outputFolderPath);
            }

            foreach (var tEntity in config.TestEntities)
            {
                foreach (var cEntity in tEntity.CommandEntities)
                {
                    NumTotalTests++;
                    var directoryName = (string.IsNullOrEmpty(tEntity.TestName)) ? cEntity.CommandName : tEntity.TestName + "_" + cEntity.CommandName;
                    string commandOutputDirectory = Path.Combine(outputFolderPath, directoryName);

                    if (!Directory.Exists(commandOutputDirectory))
                    {
                        Directory.CreateDirectory(commandOutputDirectory);
                    }

                    var PSharpTesterLocation = Path.Combine(config.PSharpBinariesFolderPath, "PSharpTester.exe");

                    string CommandString;

                    string parallelAndSchedulerFlags = string.Empty;
                    if(cEntity.NumberOfParallelTasks > 1)
                    {
                        parallelAndSchedulerFlags += $"/parallel:{cEntity.NumberOfParallelTasks} ";
                    }
                    if (!string.IsNullOrEmpty(cEntity.SchedulingStratergy))
                    {
                        parallelAndSchedulerFlags += $"/sch:{cEntity.SchedulingStratergy} ";
                    }

                    CommandString = String.Format(PSharpBatchTestCommon.Constants.PSharpTestLocalArgsTemplate, PSharpTesterLocation, tEntity.ApplicationPath,
                            cEntity.CommandFlags, parallelAndSchedulerFlags, commandOutputDirectory);

                    Console.WriteLine("Starting command [" + cEntity.CommandName + "]");
                    ProcessStartInfo startInfo = new ProcessStartInfo("cmd", CommandString);
                    startInfo.UseShellExecute = false;
                    startInfo.WorkingDirectory = commandOutputDirectory;
                    Process process = new Process();
                    process.StartInfo = startInfo;
                    process.Start();
                    try
                    {
                        process.WaitForExit();
                        if (File.ReadAllText(commandOutputDirectory + "\\psharpbatchout.txt").Contains("Found 0 bugs"))
                        {
                            NumPassedTests++;
                        }
                        else
                        {
                            Console.WriteLine("... Failed: " + tEntity.TestName + " " + cEntity.CommandName);
                            NumFailedTests++;
                        }
                    }
                    catch (Exception)
                    {
                        Console.WriteLine("... Crashed: " + tEntity.TestName + " " + cEntity.CommandName);
                        NumCrashedTests++;
                    }

                    ElapsedTime += (process.ExitTime - process.StartTime).TotalMilliseconds;
                }
            }
            Console.WriteLine("All tasks complete.");
            Console.WriteLine("Your output is stored in the following location : " + outputFolderPath);
            Console.WriteLine("... Testing statistics:");
            Console.WriteLine("..... Total number of tests: " + NumTotalTests);
            Console.WriteLine("..... Number of passed tests: " + NumPassedTests);
            Console.WriteLine("..... Number of failed tests: " + NumFailedTests);
            Console.WriteLine("..... Elapsed time: " + (ElapsedTime / 1000) + "s");
        }

        private static async Task MainAsync()
        {

            //Creating BatchOperations
            BatchOperations batchOperations = new BatchOperations(authConfig.BatchAccountName, authConfig.BatchAccountKey, authConfig.BatchAccountUrl);

            //Creating BlobOperations
            BlobOperations blobOperations = new BlobOperations(authConfig.StorageAccountName, authConfig.StorageAccountKey);
            
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
                       NodeStartCommand: PSharpBatchTestCommon.Constants.PSharpDefaultNodeStartCommand,
                       NodeMaxConcurrentTasks: config.NodeMaxConcurrentTasks
                    );
            }

            string executingDirectory = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

            //Job Details
            string jobManagerFilePath = /*typeof(PSharpBatchJobManager.Program).Assembly.Location;*/ Path.Combine(executingDirectory, @".\PSharpBatchJobManager\PSharpBatchJobManager.exe");   // Data files for Job Manager Task
            string jobTimeStamp = PSharpBatchTestCommon.Constants.GetTimeStamp();
            JobId = config.JobDefaultId + jobTimeStamp;

            //Creating BatchJob object
            var batchJob = new BatchJob();
            batchJob.PoolID = config.PoolId;
            batchJob.JobID = JobId;


            //Uploading the data files to azure storage and get the resource objects.
            var inputTupleRes = await blobOperations.UploadInputFilesFromTestEntities(config.TestEntities, config.PoolId, JobId);

            var inputFilesDict = inputTupleRes.Item1;
            batchJob.InputContainerIDs = inputTupleRes.Item2;

            //Uploading JobManager Files
            var jobTupleRes = await blobOperations.UploadJobManagerFiles(jobManagerFilePath, config.PoolId, JobId);

            var jobManagerFiles = jobTupleRes.Item1;
            batchJob.JobManagerContainerID = jobTupleRes.Item2;


            batchJob.OutputContainerID = await blobOperations.CreateOutputContainer(config.PoolId, JobId);
            var outputContainerSasUrl = blobOperations.GetOutputContainerSasUrl(batchJob.OutputContainerID);

            var numberOfTasks = config.TestEntities.Select(t => t.NumberOfTasks()).Sum();

            //Creating the job
            await batchOperations.CreateJobAsync
                (
                    jobId: JobId,
                    poolId: config.PoolId,
                    resourceFiles: jobManagerFiles,
                    outputContainerSasUrl: outputContainerSasUrl,
                    numberOfTasks: numberOfTasks,
                    timeoutInHours: config.TaskWaitHours
                );

            //Adding tasks
            await batchOperations.AddTasksFromTestEntities
                (
                    jobId: JobId,
                    taskIDPrefix: config.TaskDefaultId,
                    inputFilesDict: inputFilesDict,
                    TestEntities: config.TestEntities
                );

            var outputFolderPath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(config.OutputFolderPath));

            //Write BatchJob to file
            Directory.CreateDirectory(outputFolderPath);
            var batchJobPath = Path.Combine(outputFolderPath, "batchjob.psbatch");
            batchJob.SaveAsXML(batchJobPath);

            Logger.FlushLogs();

            if (config.MonitorBatch)
            {
                await MonitorAsync(batchJob);
            }
        }

        private static async Task MonitorAsync(BatchJob batchJob)
        {
            //Creating BatchOperations
            BatchOperations batchOperations = new BatchOperations(authConfig.BatchAccountName, authConfig.BatchAccountKey, authConfig.BatchAccountUrl);

            //Creating BlobOperations
            BlobOperations blobOperations = new BlobOperations(authConfig.StorageAccountName, authConfig.StorageAccountKey);

            //Monitor tasks
            var taskResult = await batchOperations.MonitorTasks
                (
                    jobId: JobId,
                    timeout: TimeSpan.FromHours(config.TaskWaitHours)
                );

            var outputFolderPath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(config.OutputFolderPath));

            await blobOperations.DownloadOutputFiles(outputFolderPath, batchJob.OutputContainerID);

            try
            {
                PSharpOperations.MergeOutputCoverageReport(outputFolderPath, Path.GetFullPath(Environment.ExpandEnvironmentVariables(config.PSharpBinariesFolderPath)));
            }
            catch (Exception e)
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
                await blobOperations.DeleteAllContainers(batchJob);
            }

            if (config.DeletePoolAfterDone)
            {
                await blobOperations.DeleteNodeContainer(config.PoolId);
                await batchOperations.DeletePoolAsync(config.PoolId);
            }
        }

    }
}
