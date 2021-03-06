﻿using Microsoft.Azure.Batch;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using static PSharpBatchTestCommon.PSharpBatchConfig;

namespace PSharpBatchTestCommon
{

    public class BlobOperations
    {

        // Storage account credentials
        private string StorageAccountName;
        private string StorageAccountKey;

        CloudStorageAccount storageAccount;
        CloudBlobClient blobClient;

        //Resource Files
        List<ResourceFile> nodeFiles;
        List<ResourceFile> jobManagerFiles;
        List<ResourceFile> inputFiles;
        Dictionary<PSharpTestEntities, List<ResourceFile>> inputFilesDict;

        //Other constants
        int ContainerExpiryHours;

        public BlobOperations(string StorageAccountName, string StorageAccountKey, int ContainerExpiryHours = -1)
        {
            this.StorageAccountName = StorageAccountName;
            this.StorageAccountKey = StorageAccountKey;
            this.ContainerExpiryHours = ContainerExpiryHours;
            Connect();
        }

        private void Connect()
        {
            string connectionString = string.Format(Constants.StorageConnectionStringFormat, StorageAccountName, StorageAccountKey);
            storageAccount = CloudStorageAccount.Parse(connectionString);
            blobClient = storageAccount.CreateCloudBlobClient();
        }

        /// <summary>
        /// Creates output container if not exists.
        /// </summary>
        /// <param name="poolId"></param>
        /// <param name="jobId"></param>
        /// <returns></returns>
        public async Task<string> CreateOutputContainer(string poolId, string jobId)
        {
            var outputContainerName = string.Format(Constants.OutputContainerNameFormat, jobId.ToLower());
            await CreateContainerIfNotExistAsync(outputContainerName);
            return outputContainerName;
        }

        public async Task DeleteAllContainers(BatchJob batchJob)
        {
            try
            {
                await DeleteContainerAsync(batchJob.JobManagerContainerID);
                
            }
            catch { return; }
            try
            {
                foreach (var cName in batchJob.InputContainerIDs)
                {
                    await DeleteContainerAsync(cName);
                }
            }
            catch { return; }
            try
            {
                await DeleteContainerAsync(batchJob.OutputContainerID);
            }
            catch { return; }
            
        }

        public async Task DownloadOutputFiles(string directoryPath, string outputContainerName)
        {
            directoryPath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(directoryPath));
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }
            await DownloadBlobsFromContainerAsync(outputContainerName, directoryPath);
        }

        /// <summary>
        /// Uploads the node file and its dependencies to azure storage.
        /// </summary>
        /// <param name="nodeFodlerPath"></param>
        /// <param name="poolId"></param>
        /// <returns></returns>
        public async Task<List<ResourceFile>> UploadNodeFiles(string nodeFodlerPath, string poolId)
        {
            var nodeContainerName = string.Format(Constants.NodeContainerNameFormat, poolId.ToLower());
            await CreateContainerIfNotExistAsync(nodeContainerName);
            var nodeFilePaths = new List<string> { nodeFodlerPath };
            nodeFiles = await UploadFilesAndFoldersToContainerAsync(nodeContainerName, nodeFilePaths, true);
            return nodeFiles;
        }

        /// <summary>
        /// Deletes the node container.
        /// </summary>
        /// <returns></returns>
        public async Task DeleteNodeContainer(string poolId)
        {
            try
            {
                var nodeContainer = string.Format(Constants.NodeContainerNameFormat, poolId.ToLower());
                await DeleteContainerAsync(nodeContainer);
            }
            catch (Exception exp) { return; }
        }


        
        /// <summary>
        /// Upload files for all the Tests to be perfomed
        /// </summary>
        /// <param name="TestEntities">List of Tests mentioned in config file</param>
        /// <param name="poolId"></param>
        /// <param name="jobId"></param>
        /// <returns></returns>
        public async Task<Tuple<Dictionary<PSharpTestEntities, List<ResourceFile>>, List<string>>> UploadInputFilesFromTestEntities(List<PSharpTestEntities> TestEntities, string poolId, string jobId)
        {
            inputFiles = new List<ResourceFile>();
            inputFilesDict = new Dictionary<PSharpTestEntities, List<ResourceFile>>();
            var inputContainers = new List<string>();
            
            //Creating application hashset
            HashSet<string> applicationPaths = new HashSet<string>(TestEntities.Select(ce => Path.GetFullPath(Environment.ExpandEnvironmentVariables(ce.ApplicationPath))));
            int i = 0;
            foreach(var filePath in applicationPaths)
            {
                var containerName = string.Format(Constants.InputContainerNameFormatForTestEntity, jobId.ToLower(), i);
                i++;
                await CreateContainerIfNotExistAsync(containerName);
                inputContainers.Add(containerName);
                //inputFiles.AddRange(await UploadDllsAndDependenciesAsync(inputContainerName, filePath));
                List<ResourceFile> resFiles;
                try
                {
                    resFiles = await UploadDllsAndDependenciesAsync(containerName, filePath, true);
                }
                catch(Exception e)
                {
                    Console.WriteLine(e.Message);
                    resFiles = await UploadFilesAndFoldersToContainerAsync(containerName, new List<string> { Path.GetDirectoryName(filePath) }, true);
                }
                foreach(var tEntities in TestEntities.Where(t=>filePath.Equals(Path.GetFullPath(Environment.ExpandEnvironmentVariables(t.ApplicationPath)))))
                {
                    inputFilesDict.Add(tEntities, resFiles);
                }
            }
            return new Tuple<Dictionary<PSharpTestEntities, List<ResourceFile>>, List<string>>(inputFilesDict, inputContainers);
        }

        /// <summary>
        /// Uploads job manager application and its dependencies
        /// </summary>
        /// <param name="jobManagerFilePath"></param>
        /// <param name="poolId"></param>
        /// <param name="jobId"></param>
        /// <returns></returns>
        public async Task<Tuple<List<ResourceFile>, string>> UploadJobManagerFiles(string jobManagerFilePath, string poolId, string jobId)
        {
            jobManagerFilePath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(jobManagerFilePath));
            var jobManagerContainerName = string.Format(Constants.JobManagerContainerNameFormat, jobId.ToLower());
            await CreateContainerIfNotExistAsync(jobManagerContainerName);
            //jobManagerFiles = await UploadFilesAndFoldersToContainerAsync(jobManagerContainerName, jobManagerFilePaths);
            jobManagerFiles = await UploadDllsAndDependenciesAsync(jobManagerContainerName, jobManagerFilePath, true);
            return new Tuple<List<ResourceFile>, string>(jobManagerFiles, jobManagerContainerName);
        }

        public List<ResourceFile> GetApplicationResourceFiles()
        {
            return nodeFiles;
        }

        public List<ResourceFile> GetInputResourceFiles()
        {
            return inputFiles;
        }

        public List<ResourceFile> GetJobManagerResourceFiles()
        {
            return jobManagerFiles;
        }

        public string GetOutputContainerSasUrl(string outputContainerName)
        {
            return GetContainerSasUrl(outputContainerName, SharedAccessBlobPermissions.Write, true);
        }

        public CloudStorageAccount GetCloudStorageAccount()
        {
            return storageAccount;
        }

        public async Task CreateContainerIfNotExistAsync(string containerName)
        {
            CloudBlobContainer container = blobClient.GetContainerReference(containerName);

            if (await container.CreateIfNotExistsAsync())
            {
                Console.WriteLine("Container [{0}] created.", containerName);
            }
            else
            {
                Console.WriteLine("Container [{0}] exists, skipping creation.", containerName);
            }
        }


        public async Task<List<ResourceFile>> UploadDllsAndDependenciesAsync(string inputContainerName, string applicationPath, bool IsInfinteTimeout = false)
        {
            HashSet<string> allDependencies = new HashSet<string>();
            var fullPath = Path.GetFullPath(applicationPath);
            if (CheckIfDirectory(fullPath))
            {
                //Dont consider
                throw new Exception("Application path cannot be a folder.");
            }
            //Get dpependencies and their file paths
            allDependencies = GetDependenciesRecursive(fullPath);
            allDependencies.Add(fullPath);
            var finalPaths = allDependencies.ToList();
            return await UploadFilesToContainerAsync(inputContainerName, finalPaths, IsInfinteTimeout);
        }

        private HashSet<string> GetDependenciesRecursive(string filePath)
        {
            HashSet<string> allDependencies = new HashSet<string>();
            var resultSet = GetDependenciesRecursive(filePath, allDependencies);
            return resultSet;
        }

        private HashSet<string> GetDependenciesRecursive(string filePath, HashSet<string> allDependencies)
        {
            HashSet<string> dependencies = new HashSet<string>();

            //File Operations
            var fileInfo = new FileInfo(filePath);
            var directoryPath = fileInfo.Directory.FullName;
            //Assembly Operations
            var assembly = Assembly.LoadFile(filePath);
            var references = assembly.GetReferencedAssemblies();
            foreach (var reference in references)
            {
                //Find the file from directory
                if (reference.FullName.Contains("PublicKeyToken=b77a5c561934e089")) { continue; }
                var refFilePath = Directory.GetFiles(directoryPath, string.Format("*{0}.dll", reference.Name)).FirstOrDefault();
                if (null == refFilePath)
                {
                    refFilePath = Directory.GetFiles(directoryPath, string.Format("*{0}.dll", reference.Name)).FirstOrDefault();
                }
                if (refFilePath != null && !allDependencies.Contains(refFilePath) && !dependencies.Contains(refFilePath))
                {
                    dependencies.Add(refFilePath);
                }
            }
            allDependencies.UnionWith(dependencies);
            List<string> subDependencies = new List<string>();
            foreach (var d in dependencies)
            {
                allDependencies.UnionWith(GetDependenciesRecursive(d, allDependencies));
            }
            return allDependencies;
        }

        public async Task<List<ResourceFile>> UploadFilesAndFoldersToContainerAsync(string inputContainerName, List<string> filePaths, bool IsInfiniteTimeout = false)
        {
            List<string> allFilePaths = new List<string>();
            foreach (var path in filePaths)
            {
                if (CheckIfDirectory(path))
                {
                    //currently skipping directories
                    allFilePaths.AddRange(GetAllFilesFromFolder(path).Where(f => !f.EndsWith(".pdb")));
                }
                else
                {
                    allFilePaths.Add(path);
                }
            }

            return await UploadFilesToContainerAsync(inputContainerName, allFilePaths, IsInfiniteTimeout);
        }


        private async Task<List<ResourceFile>> UploadFilesToContainerAsync(string inputContainerName, List<string> filePaths, bool IsInfinteTimeout)
        {
            List<ResourceFile> resourceFiles = new List<ResourceFile>();

            foreach (string filePath in filePaths)
            {
                try
                {
                    resourceFiles.Add(await UploadFileToContainerAsync(inputContainerName, filePath, IsInfinteTimeout));
                }
                catch(Exception exp)
                {
                    //Exception can be thrown if the file is in use, in that case we just skip the file.
                    Console.WriteLine(exp.Message);
                }
            }

            return resourceFiles;
        }

        public async Task<ResourceFile> UploadFileToContainerAsync(string containerName, string filePath, bool IsInfinteTimeout)
        {
            Console.WriteLine("Uploading file {0} to container [{1}]...", filePath, containerName);

            string blobName = Path.GetFileName(filePath);

            CloudBlobContainer container = blobClient.GetContainerReference(containerName);
            CloudBlockBlob blobData = container.GetBlockBlobReference(blobName);
            await blobData.UploadFromFileAsync(filePath);

            // Set the expiry time and permissions for the blob shared access signature. In this case, no start time is specified,
            // so the shared access signature becomes valid immediately
            SharedAccessBlobPolicy sasConstraints = new SharedAccessBlobPolicy
            {
                SharedAccessExpiryTime = (ContainerExpiryHours < 0 || IsInfinteTimeout) ?DateTime.MaxValue.ToUniversalTime():DateTime.UtcNow.AddHours(ContainerExpiryHours),
                Permissions = SharedAccessBlobPermissions.Read
            };

            // Construct the SAS URL for blob
            string sasBlobToken = blobData.GetSharedAccessSignature(sasConstraints);
            string blobSasUri = String.Format("{0}{1}", blobData.Uri, sasBlobToken);

            return new ResourceFile(blobSasUri, blobName);
        }

        private static bool CheckIfDirectory(string path)
        {
            try
            {
                FileAttributes attrb = File.GetAttributes(path);
                if (attrb.HasFlag(FileAttributes.Directory))
                {
                    return true;
                }
            }
            catch (Exception e)
            {

            }
            return false;
        }

        private static List<string> GetAllFilesFromFolder(string path)
        {
            //Doesn't work with directories and files starting with . : hidden files and folder
            List<string> filePaths = new List<string>();
            var files = Directory.EnumerateFileSystemEntries(path);
            foreach (var file in files)
            {
                if (CheckIfDirectory(file))
                {
                    //Directory
                    //skipping for now
                    //filePaths.AddRange(GetAllFilesFromFolder(file));
                }
                else
                {
                    filePaths.Add(file);
                }
            }

            return filePaths;
        }

        private string GetContainerSasUrl(string containerName, SharedAccessBlobPermissions permissions, bool infiniteTimeout = false)
        {
            // Set the expiry time and permissions for the container access signature. In this case, no start time is specified,
            // so the shared access signature becomes valid immediately
            SharedAccessBlobPolicy sasConstraints = new SharedAccessBlobPolicy
            {
                SharedAccessExpiryTime = (ContainerExpiryHours < 0 || infiniteTimeout)?DateTime.MaxValue.ToUniversalTime():DateTime.UtcNow.AddHours(ContainerExpiryHours),
                Permissions = permissions
            };

            // Generate the shared access signature on the container, setting the constraints directly on the signature
            CloudBlobContainer container = blobClient.GetContainerReference(containerName);
            string sasContainerToken = container.GetSharedAccessSignature(sasConstraints);

            // Return the URL string for the container, including the SAS token
            return String.Format("{0}{1}", container.Uri, sasContainerToken);
        }

        public async Task DownloadBlobsFromContainerAsync(string containerName, string directoryPath)
        {
            Console.WriteLine("Downloading all files from container [{0}]...", containerName);

            // Retrieve a reference to a previously created container
            CloudBlobContainer container = blobClient.GetContainerReference(containerName);

            // Get a flat listing of all the block blobs in the specified container
            foreach (IListBlobItem item in container.ListBlobs(prefix: null, useFlatBlobListing: true))
            {
                // Retrieve reference to the current blob
                CloudBlob blob = (CloudBlob)item;
                Console.WriteLine("Dowloading file: [" + blob.Name + "].");
                // Save blob contents to a file in the specified folder
                string localOutputFile = GetPathFromBlobName(blob.Name, directoryPath);
                var localDirectoryPath = Path.GetDirectoryName(localOutputFile);
                if (!Directory.Exists(localDirectoryPath))
                {
                    Directory.CreateDirectory(localDirectoryPath);
                }
                await blob.DownloadToFileAsync(localOutputFile, FileMode.Create);
            }

            Console.WriteLine("All files downloaded to {0}", directoryPath);
        }

        private static string GetPathFromBlobName(string BlobName, string directoryPath)
        {
            string defaultPath = Path.Combine(directoryPath, BlobName);
            try
            {
                var dollarSplit = BlobName.Split('$');
                var splitWords = dollarSplit.First().Split('_');
                var testName = splitWords[1];
                var commandName = splitWords[2];
                var fileName = splitWords[3] + "_" + dollarSplit[1];
                var folderName = string.IsNullOrEmpty(testName) ? commandName : testName + "_" + commandName;
                return Path.Combine(directoryPath, folderName, fileName);
            }
            catch(Exception e)
            {
                return defaultPath;
            }
        }

        private async Task DeleteContainerAsync(string containerName)
        {
            CloudBlobContainer container = blobClient.GetContainerReference(containerName);

            if (await container.DeleteIfExistsAsync())
            {
                Console.WriteLine("Container [{0}] deleted.", containerName);
            }
            else
            {
                Console.WriteLine("Container [{0}] does not exist, skipping deletion.", containerName);
            }
        }

        public void UploadFileToContainerUsingSas(string filePath, string containerSas)
        {
            string blobName = Path.GetFileName(filePath);

            // Obtain a reference to the container using the SAS URI.
            CloudBlobContainer container = new CloudBlobContainer(new Uri(containerSas));

            // Upload the file (as a new blob) to the container
            try
            {
                CloudBlockBlob blob = container.GetBlockBlobReference(blobName);
                blob.UploadFromFile(filePath);

                Console.WriteLine("Write operation succeeded for SAS URL " + containerSas);
                Console.WriteLine();
            }
            catch (StorageException e)
            {

                Console.WriteLine("Write operation failed for SAS URL " + containerSas);
                Console.WriteLine("Additional error information: " + e.Message);
                Console.WriteLine();

                // Indicate that a failure has occurred so that when the Batch service sets the
                // CloudTask.ExecutionInformation.ExitCode for the task that executed this application,
                // it properly indicates that there was a problem with the task.
                Environment.ExitCode = -1;
            }
        }
    }
}
