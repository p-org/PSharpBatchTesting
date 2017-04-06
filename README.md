# PSharp Batch Testing
PSharp batch testing is an Addon for [PSharp](https://github.com/p-org/PSharp) language. 

## Overview
PSharp batch tester uses [Azure Batch Service](https://azure.microsoft.com/en-in/services/batch/) to run multiple PSharp tests parallely on the cloud and fetch the result. Using this tool you can run heavy testing in the cloud.

## Running PSharp Batch Tests
To run Psharp batch test, you need an Azure subcription and an Azure batch service account. To learn more about creating an Azure batch service account, view the sub-heading Azure Batch Service. Once you have that, follow the steps below:

1. Open the solution containing the PSharp project you need to test.
2. Install the Microsoft.PSharp.BatchTesting nuget from here: [Microsoft.PSharp.Nuget]().
3. Installing the Nuget will add two config files to the project.
    * PSharpBatch.config : Contains configuration for uploading necessasary files, testing in the cloud and downloading the results.
    * PSharpBatchAuth.config : authentication credentials for Azure batch account and storage account.  

    (For more detials about configuration go to: [Configuring Batch Test](#Configuring-Batch-Test)).

4. Edit the configuration files to need and Open Nuget Package Manager Console.
5. Run the command BatchTest in Nuget Package Manager Console to start the Batch Testing.

## Configuring Batch Test

### Configuring PSharpBatch.config

PSharpBatch.config has the following parameters:  

* **PoolId** : Id of the virtual machine pool in Azure batch service. If it doesn't exists already, it will be created.
* **NumberOfNodesInPool** : Number of VMs to be added in the pool, if it is created.
* **NodeOsFamily** : The OS version of the VMs. Should contain .Net Framework 4.6 and above (OsFamily: >=5). For more details visit [Azure Guest OS Update Matix](https://docs.microsoft.com/en-us/azure/cloud-services/cloud-services-guestos-update-matrix).
* **JobDefaultId** : Prefix for the Job to be created for the given tests. This will be appended by timestamp.
* **TaskDefaultId** : Prefix for the tasks created under the Job. Will be appended by JobId and timestamp.
* **BlobContainerSasExpiryHours** : Exipry time in hours for the files uploaded to storage account. After this time period, the Batch service will not be able to use the files. -1 for no expiry.
* **PSharpBinariesFolderPath** : Path to folder containing PSharp Binaries.
* **OutputFolderPath** : Path to folder where the output should be downloaded.
* **TaskWaitHours** : Waiting time in hours for the test to complete. Post this time, the unfinished tasks will be terminated.
* **DeleteJobAfterDone** : (true/false) Whether to delete the Batch-service job after it is complete.
* **DeleteContainerAfterDone** : (true/false) Whether to delete the storage account containers after the tests are complete.
* **Commands** : PSharp test commands to run in cloud. You cna specify multiple commands via &lt;command&gt; tag.
    * **NumberOfParallelTasks** : Number of parallel tasks to run. Equivalent to /parallel: flag in PSharp test command.
    * **IterationsPerTask** : Number of iterations per task. Equivalent to /i: flag in PSharp test command.
    * **TestApplicaionPath** : Path to the application to be tested (.dll/.exe).
    * **CommandFlags** : Other flags to mention in the PSharp test command.
    * **CommandName** : A name to command to distinguish between different tasks.
    * **SchedulingStratergy** : Scheduling statergy to run the test. Equivalent to /sch: flag in Psharp test command.


### Configuring PSharpBatch.Auth.config

PSharpBatchAuth.config has the following parameters:

* **BatchAccountName** : Name of the Azure Batch Service Account.
* **BatchAccountKey** : Account Key for the Azure Batch Service Account.
* **BatchAccountUrl** : URL of the Azure Batch Service Account.
* **StorageAccountName** : Name of the Storage Account linked with the Azure Batch Service Account.
* **StorageAccountKey** : Account Key (Primary/Secondary) of the above mentioned Storage Account.