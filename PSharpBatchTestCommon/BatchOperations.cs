﻿using Microsoft.Azure.Batch;
using Microsoft.Azure.Batch.Auth;
using Microsoft.Azure.Batch.Common;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static PSharpBatchTestCommon.PSharpBatchConfig;

namespace PSharpBatchTestCommon
{
    public class BatchOperations
    {

        // Batch account credentials
        private string BatchAccountName;
        private string BatchAccountKey;
        private string BatchAccountUrl;

        BatchSharedKeyCredentials credentials;
        BatchClient batchClient;

        public BatchOperations(string BatchAccountName, string BatchAccountKey, string BatchAccountUrl)
        {
            this.BatchAccountName = BatchAccountName;
            this.BatchAccountKey = BatchAccountKey;
            this.BatchAccountUrl = BatchAccountUrl;

            Connect();
        }

        /// <summary>
        /// Creates and Connects to the Azure Batch Client
        /// </summary>
        private void Connect()
        {
            //Creating the Batch client
            credentials = new BatchSharedKeyCredentials(BatchAccountUrl, BatchAccountName, BatchAccountKey);
            batchClient = BatchClient.Open(credentials);
        }

        /// <summary>
        /// Checks if Pool exists in the batch account for the given pool id.
        /// </summary>
        /// <param name="poolId"></param>
        /// <returns></returns>
        public async Task<bool> CheckIfPoolExists(string poolId)
        {
            //Check if pool exists
            try
            {
                var pool = await batchClient.PoolOperations.GetPoolAsync(poolId);
                if (null != pool)
                {
                    return true;
                }
            }
            catch (BatchException be)
            {
                // Swallow the specific error code PoolExists since that is expected if the pool already exists
                if (be.RequestInformation?.BatchError != null && be.RequestInformation.BatchError.Code == BatchErrorCodeStrings.PoolNotFound)
                {
                    //Pool exists
                    return false;
                }
                else
                {
                    throw; // Any other exception is unexpected
                }
            }
            return false;
        }

        /// <summary>
        /// Creates Pool if it doesn't exists already
        /// </summary>
        /// <param name="poolId">Identifier for the pool</param>
        /// <param name="resourceFiles">Resources files to be copied to the pool nodes when created</param>
        /// <param name="numberOfNodes">Number of nodes to be crearted</param>
        /// <param name="OSFamily">OSFamily for the pool nodes</param>
        /// <param name="VirtualMachineSize">Size of the node virtual machines</param>
        /// <param name="NodeStartCommand">Node start command</param>
        /// <returns></returns>
        public async Task CreatePoolIfNotExistAsync(string poolId,
            IList<ResourceFile> resourceFiles, int numberOfNodes = 1, string OSFamily = "5", string VirtualMachineSize = "small", string NodeStartCommand = Constants.PSharpDefaultNodeStartCommand, int NodeMaxConcurrentTasks = 1)
        {
            CloudPool pool = null;
            try
            {
                Console.WriteLine("Creating pool [{0}]...", poolId);

                // Create the unbound pool. Until we call CloudPool.Commit() or CommitAsync(), no pool is actually created in the
                // Batch service. This CloudPool instance is therefore considered "unbound," and we can modify its properties.
                pool = batchClient.PoolOperations.CreatePool(
                    poolId: poolId,
                    targetDedicatedComputeNodes: numberOfNodes,                                                     // Default : 1
                    virtualMachineSize: VirtualMachineSize,                                              // Defualt : small -> single-core, 1.75 GB memory, 225 GB disk
                    cloudServiceConfiguration: new CloudServiceConfiguration(osFamily: OSFamily));      // Default : 5 -> Windows Server 2012 R2 with .Net Framework 4.6.2 support


                //Seeting node max concurrent tasks
                pool.MaxTasksPerComputeNode = NodeMaxConcurrentTasks;

                // Create and assign the StartTask that will be executed when compute nodes join the pool.
                // In this case, we copy the StartTask's resource files (that will be automatically downloaded
                // to the node by the StartTask) into the shared directory that all tasks will have access to.
                pool.StartTask = new StartTask
                {
                    // Default Command : 
                    // Specify a command line for the StartTask that copies the task application files to the
                    // node's shared directory. Every compute node in a Batch pool is configured with a number
                    // of pre-defined environment variables that can be referenced by commands or applications
                    // run by tasks.
                    // Since a successful execution of robocopy can return a non-zero exit code (e.g. 1 when one or
                    // more files were successfully copied) we need to manually exit with a 0 for Batch to recognize
                    // StartTask execution success.
                    CommandLine = NodeStartCommand,
                    ResourceFiles = resourceFiles,
                    WaitForSuccess = true
                };

                await pool.CommitAsync();
            }
            catch (BatchException be)
            {
                // Swallow the specific error code PoolExists since that is expected if the pool already exists
                if (be.RequestInformation?.BatchError != null && be.RequestInformation.BatchError.Code == BatchErrorCodeStrings.PoolExists)
                {
                    Console.WriteLine("The pool {0} already existed when we tried to create it", poolId);
                }
                else
                {
                    throw; // Any other exception is unexpected
                }
            }
        }

        /// <summary>
        /// Creates and adds a Job to the mentioned Pool.
        /// </summary>
        /// <param name="jobId">Identifier for the Job</param>
        /// <param name="poolId">Identifier of the pool to which the Job should be added</param>
        /// <returns></returns>
        public async Task CreateJobAsync(string jobId, string poolId, IList<ResourceFile> resourceFiles, string outputContainerSasUrl, int numberOfTasks, int timeoutInHours)
        {
            Console.WriteLine("Creating job [{0}]...", jobId);

            CloudJob job = batchClient.JobOperations.CreateJob();           // Job is not created until it is commited. Therefore we can modify its properties.
            job.Id = jobId;
            job.PoolInformation = new PoolInformation { PoolId = poolId };  // Mentions the pool to which the job is tied.

            string jobManagerID = jobId + "ManagerTask";

            string jobManagerTaskCommandLine = $"cmd /c PSharpBatchJobManager.exe \"{BatchAccountName}\" \"{BatchAccountKey}\" \"{BatchAccountUrl}\" \"{jobId}\" \"{jobManagerID}\" \"{outputContainerSasUrl}\" \"{numberOfTasks}\" \"{timeoutInHours}\" ";

            //string jobManagerTaskCommandLine = string.Format("cmd /c PSharpBatchJobManager.exe \"{0}\" \"{1}\" \"{2}\" \"{3}\" \"{4}\" \"{5}\" \"{6}\" ", BatchAccountName, BatchAccountKey, BatchAccountUrl, jobId, jobManagerID, outputContainerSasUrl);

            job.JobManagerTask = new JobManagerTask
                (
                    id: jobManagerID,
                    commandLine: jobManagerTaskCommandLine
                );
            job.JobManagerTask.ResourceFiles = resourceFiles;

            await job.CommitAsync();
        }

        /// <summary>
        /// Creates and adds a task to the mentioned job.
        /// </summary>
        /// <param name="jobId">Identifier of the Job to which the task shoud be added</param>
        /// <param name="taskId">Identifier for the task</param>
        /// <param name="inputFiles">Input files for the task</param>
        /// <param name="taskCommand">Command of the task</param>
        /// <returns></returns>
        public async Task<CloudTask> AddTaskAsync(string jobId, string taskId, IList<ResourceFile> inputFiles, string taskCommand = Constants.PSharpDefaultTaskCommandLine)
        {
            //Prefer to add tasks in bulk, which is more efficient.

            Console.WriteLine("Adding task to job [{0}]...", jobId);

            // Only Applicable for the default command:
            // Because we copied the task application to the node's shared directory 
            // with the pool's StartTask, we can access it via
            // the shared directory on whichever node each task will run.

            // Creating the task.
            string taskCommandLine = String.Format(taskCommand);
            CloudTask task = new CloudTask(taskId, taskCommandLine);
            task.ResourceFiles = inputFiles;

            // Adding the task to Job
            await batchClient.JobOperations.AddTaskAsync(jobId, task);

            return task;
        }

        /// <summary>
        /// Adds tasks from Test entities containing multiple test commands
        /// </summary>
        /// <param name="jobId"></param>
        /// <param name="taskIDPrefix"></param>
        /// <param name="inputFilesDict"></param>
        /// <param name="CommandEntities"></param>
        /// <returns></returns>
        public async Task<List<CloudTask>> AddTasksFromTestEntities(string jobId, string taskIDPrefix, Dictionary<PSharpTestEntities, List<ResourceFile>> inputFilesDict, List<PSharpTestEntities> TestEntities)
        {
            List<CloudTask> tasks = new List<CloudTask>();
            //Creating tasks with iterations
            List<string> taskCommands = new List<string>();

            foreach(var tEntity in TestEntities)
            {
                foreach(var cEntity in tEntity.CommandEntities)
                {
                    for (int i = 0; i < cEntity.NumberOfParallelTasks; i++)
                    {
                        //Todo: Incase of Portfolio : give additional flags /testing-process-id: along with /sch:portfolio

                        string command = string.Empty;
                        if (string.IsNullOrEmpty(cEntity.SchedulingStratergy))
                        {
                            //No shceduling statergy provided
                            command = string.Format(Constants.PSharpTaskCommandFormatWithFlags, Path.GetFileName(tEntity.ApplicationPath), cEntity.CommandFlags);
                        }
                        else
                        {
                            //Schduling stratergy provided. In case of portfolio testing-process-id is added.
                            string schFlag = string.Empty;
                            if (cEntity.SchedulingStratergy.ToLower().Equals("portfolio"))
                            {
                                schFlag = "/sch:" + cEntity.SchedulingStratergy + " /testing-process-id:" + i;
                            }
                            else
                            {
                                schFlag = "/sch:" + cEntity.SchedulingStratergy;
                            }
                            command = string.Format(Constants.PSharpTaskCommandFormatWithSchFlags, Path.GetFileName(tEntity.ApplicationPath), cEntity.CommandFlags, schFlag);
                        }
                        taskCommands.Add(command);
                        string taskId = taskIDPrefix + "_" + tEntity.TestName + "_" + cEntity.CommandName + "_" + i;
                        string taskCommandLine = command;
                        CloudTask task = new CloudTask(taskId, taskCommandLine);
                        task.ResourceFiles = inputFilesDict[tEntity];
                        tasks.Add(task);
                    }
                }
            }

            Console.WriteLine("Adding {0} tasks to job [{1}]...", tasks.Count, jobId);

            //Add tasks in chunks
            var TaskChunks = PSharpOperations.SplitList(tasks, Constants.BatchTestChunkSize);

            foreach(List<CloudTask> subTaskList in TaskChunks)
            {
                await batchClient.JobOperations.AddTaskAsync(jobId, subTaskList, timeout: TimeSpan.FromMinutes(30));
            }

            //await batchClient.JobOperations.AddTaskAsync(jobId, tasks, timeout: TimeSpan.FromMinutes(30));

            return tasks;
        }

        /// <summary>
        /// Creates and adds multiple tasks to the mentioned job. 
        /// Number of tasks created will ne the number of commands. This method assumes all tasks have same input file.
        /// </summary>
        /// <param name="jobId"></param>
        /// <param name="taskIDPrefix"></param>
        /// <param name="inputFiles"></param>
        /// <param name="taskCommands">List of commands for the tasks.</param>
        /// <returns></returns>
        public async Task<List<CloudTask>> AddTasksAsync(string jobId, string taskIDPrefix, List<ResourceFile> inputFiles, List<string> taskCommands)
        {
            Console.WriteLine("Adding {0} tasks to job [{1}]...", taskCommands.Count, jobId);

            // Create a collection to hold the tasks that we'll be adding to the job
            List<CloudTask> tasks = new List<CloudTask>();

            //Resource files to be uploaded
            List<ResourceFile> resourceFiles = new List<ResourceFile>();
            resourceFiles.AddRange(inputFiles);

            // Create each of the tasks. Because we copied the task application to the
            // node's shared directory with the pool's StartTask, we can access it via
            // the shared directory on whichever node each task will run.

            for (int i = 0; i < taskCommands.Count; i++)
            {
                //currently adding one task at a time
                string taskId = taskIDPrefix + i;
                string taskCommandLine = taskCommands.ElementAt(i);
                CloudTask task = new CloudTask(taskId, taskCommandLine);
                task.ResourceFiles = resourceFiles;
                tasks.Add(task);
            }

            // Add the tasks as a collection opposed to a separate AddTask call for each. Bulk task submission
            // helps to ensure efficient underlying API calls to the Batch service.
            await batchClient.JobOperations.AddTaskAsync(jobId, tasks);

            return tasks;
        }

        /// <summary>
        /// Monitoring task complete for a particular job.
        /// </summary>
        /// <param name="jobId">Identifier of the Job to monitor</param>
        /// <param name="timeout">Timeout for monitor operation</param>
        /// <returns></returns>
        public async Task<bool> MonitorTasks(string jobId, TimeSpan timeout)
        {
            bool allTasksSuccessful = true;
            const string successMessage = "All tasks reached state Completed.";
            const string failureMessage = "One or more tasks failed to reach the Completed state within the timeout period.";

            // Obtain the collection of tasks currently managed by the job. Note that we use a detail level to
            // specify that only the "id" property of each task should be populated. Using a detail level for
            // all list operations helps to lower response time from the Batch service.
            ODATADetailLevel detail = new ODATADetailLevel(selectClause: "id");
            List<CloudTask> tasks = await batchClient.JobOperations.ListTasks(jobId, detail).ToListAsync();

            Console.WriteLine("Awaiting task completion, timeout in {0}...", timeout.ToString());

            // We use a TaskStateMonitor to monitor the state of our tasks. In this case, we will wait for all tasks to
            // reach the Completed state.
            TaskStateMonitor taskStateMonitor = batchClient.Utilities.CreateTaskStateMonitor();
            try
            {
                CancellationTokenSource tokenSource = new CancellationTokenSource();
                var cancellationToken = tokenSource.Token;

                //Progress monitoring task
                var monitorTask = Task.Run(() =>
                {
                    Console.WriteLine();
                    Console.WriteLine();
                    Console.Write($"\rProgress: [----------] (0%)");

                    do
                    {
                        Thread.Sleep(4000);

                        int completedTasks = 0;
                        int activeTasks = 0;
                        int runningTasks = 0;
                        int preparingTasks = 0;
                        foreach (var t in tasks)
                        {
                            t.Refresh();
                            if (t.State == null) { continue; }
                            switch (t.State)
                            {
                                case TaskState.Completed:
                                    completedTasks++;
                                    break;
                                case TaskState.Active:
                                    activeTasks++;
                                    break;
                                case TaskState.Preparing:
                                    preparingTasks++;
                                    break;
                                case TaskState.Running:
                                    runningTasks++;
                                    break;
                            }
                        }

                        //Console.Write($"\rCompleted tasks: {completedTasks} | Running tasks: {runningTasks} | Active tasks: {activeTasks} | Preparing tasks: {preparingTasks}");
                        float progress = ((float)(completedTasks * 100)) / (float)tasks.Count;
                        int progChar = (int)(progress / 10);
                        Console.Write($"\rProgress: [{new string('#', progChar)}{new string('-', 10 - progChar)}] ({(int)progress}%)");


                        if (completedTasks == tasks.Count) { break; }

                    } while (!cancellationToken.IsCancellationRequested);
                    Console.WriteLine();
                }, cancellationToken);

                await taskStateMonitor.WhenAll(tasks, TaskState.Completed, timeout);
                tokenSource.Cancel();
                monitorTask.Wait();
            }
            catch (TimeoutException)
            {
                await batchClient.JobOperations.TerminateJobAsync(jobId, failureMessage);
                Console.WriteLine(failureMessage);
                return false;
            }

            #region Terminate taks Not required
            //try
            //{
            //    await batchClient.JobOperations.TerminateJobAsync(jobId, successMessage);
            //}
            //catch(Exception exp)
            //{
            //    Console.WriteLine(exp.StackTrace);
            //} 
            #endregion

            // All tasks have reached the "Completed" state, however, this does not guarantee all tasks completed successfully.
            // Here we further check each task's ExecutionInfo property to ensure that it did not encounter a scheduling error
            // or return a non-zero exit code.

            // Update the detail level to populate only the task id and executionInfo properties.
            // We refresh the tasks below, and need only this information for each task.
            detail.SelectClause = "id, executionInfo";

            foreach (CloudTask task in tasks)
            {
                // Populate the task's properties with the latest info from the Batch service
                await task.RefreshAsync(detail);

                //if (task.ExecutionInformation.SchedulingError != null)
                //{
                //    // A scheduling error indicates a problem starting the task on the node. It is important to note that
                //    // the task's state can be "Completed," yet still have encountered a scheduling error.

                //    allTasksSuccessful = false;

                //    Console.WriteLine("WARNING: Task [{0}] encountered a scheduling error: {1}", task.Id, task.ExecutionInformation.SchedulingError.Message);
                //}
                //else 
                if (task.ExecutionInformation.ExitCode != 0)
                {
                    // A non-zero exit code may indicate that the application executed by the task encountered an error
                    // during execution. As not every application returns non-zero on failure by default (e.g. robocopy),
                    // your implementation of error checking may differ from this example.

                    allTasksSuccessful = false;

                    Console.WriteLine("WARNING: Task [{0}] returned a non-zero exit code - this may indicate task execution or completion failure.", task.Id);
                }
            }

            if (allTasksSuccessful)
            {
                Console.WriteLine("Success! All tasks completed successfully within the specified timeout period.");
            }

            return allTasksSuccessful;
        }

        /// <summary>
        /// Delete Job.
        /// </summary>
        /// <param name="JobId">Identifer of the Job</param>
        /// <returns></returns>
        public async Task DeleteJobAsync(string JobId)
        {
            try
            {
                Console.WriteLine("Deleting Job [{0}] from Azure", JobId);
                await batchClient.JobOperations.DeleteJobAsync(JobId);
            }
            catch(Exception e)
            {
                Console.WriteLine("Unable to delete Job. Please delete it manually");
            }
        }

        /// <summary>
        /// Delete Pool
        /// </summary>
        /// <param name="PoolId">Identifier of the pool</param>
        /// <returns></returns>
        public async Task DeletePoolAsync(string PoolId)
        {
            await batchClient.PoolOperations.DeletePoolAsync(PoolId);
        }

        ~BatchOperations()
        {
            batchClient.Dispose();
        }
    }
}
