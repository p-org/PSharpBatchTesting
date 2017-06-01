using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PSharpBatchTestCommon
{
    public class Constants
    {

        //Storage Constants
        public static string StorageConnectionStringFormat = "DefaultEndpointsProtocol=https;AccountName={0};AccountKey={1}";
        public static string NodeContainerNameFormat = "application{0}"; //{0}:PoolID.
        public static string InputContainerNameFormat = "input{0}{1}"; //{0}:PoolID. {1}:JobID
        public static string InputContainerNameFormatForTestEntity = "input{0}{1}path{2}"; //{0}:PoolID. {1}:JobID {2}:Path index in the Hash
        public static string OutputContainerNameFormat = "output{0}{1}"; //{0}:PoolID. {1}:JobID
        public static string JobManagerContainerNameFormat = "jobmanager{0}{1}"; //{0}:PoolID. {1}:JobID
        //public static int BlobContainerSasExpiryHours = 10;


        //Batch Constats : includes Pools, Nodes, Jobs and Tasks
        //public static int MaxIterationPerTask = 1000;

        //Command Constants
        public const string PSharpDefaultNodeStartCommand = "cmd /c (robocopy %AZ_BATCH_TASK_WORKING_DIR% %AZ_BATCH_NODE_SHARED_DIR%) ^& IF %ERRORLEVEL% LEQ 1 exit 0";
        public const string PSharpDefaultTaskCommandLine = "cmd /c %AZ_BATCH_NODE_SHARED_DIR%\\PSharpTester.exe /test:RaceTest.exe 1>out.txt 2>&1";
        public const string PSharpTaskCommandFormat = "cmd /c %AZ_BATCH_NODE_SHARED_DIR%\\PSharpTester.exe /test:{0} /i:{1} 1>psharpbatchout.txt 2>&1";
        
        //{0}: Test application, {1}: number of iterations, {2}: Flags
        public const string PSharpTaskCommandFormatWithFlags = "cmd /c %AZ_BATCH_NODE_SHARED_DIR%\\PSharpTester.exe /test:.\\{0} /i:{1} {2} 1>psharpbatchout.txt 2>&1"; 
        //{0}: Test application, {1}: number of iterations, {2}: Flags, {3}: Scheduler (and testing-process-id)
        public const string PSharpTaskCommandFormatWithSchFlags = "cmd /c %AZ_BATCH_NODE_SHARED_DIR%\\PSharpTester.exe /test:.\\{0} /i:{1} {2} {3} 1>psharpbatchout.txt 2>&1";


        //Coverage Report command template
        // {0} directory in which we have the .sci files, {1} : Location of the CoverageReportMerger exe
        // {2} paths to all the Sci files space seperated.
        public const string PSharpCoverageReportMergerCommandTemplate = "/c cd {0} & {1} {2} /output:Merged_Report";

        //Local command constants
        //{0} : PSharpTester Paht | {1} : Test application path | {2} : Number of iterations 
        //{3} : Number of parallel tasks | {4} : other flags | {5} : Output Directory | {6} : Scheduler
        public const string PSharpTestLocalArgsTemplate = "/c {0} /test:{1} {2} /o:{3} 1>psharpbatchout.txt 2>&1";
        //public const string PSharpTestLocalArgsWithSchFlagTemplate = "/c {0} /test:{1} /i:{2} /parallel:{3} {4} /o:{5} /sch:{6} 1>psharpbatchout.txt 2>&1";

        //Exception Messages
        public const string ExceptionPoolIdMessage = "Incorrect value for Pool Id.";
        public const string ExceptionJobIdMessage = "Incorrect value for Job Id.";
        public const string ExceptionTaskIdMessage = "Incorrect value for Task Id.";
        public const string ExceptionBlobExpiryMessage = "Incorrect value for Blob expiry.";
        public const string ExceptionNumNodesMessage = "Incorrect value for Number of nodes. Number of nodes should be greater than 2.";
        public const string ExceptionNodeOsFamilyMessage = "Incorrect value for Node OsFamily.";
        public const string ExceptionNodeVirtualMachineSizeMessage = "Incorrect value for Node Virtual machine size.";
        public const string ExceptionPSharpBinariesPathMessage = "Incorrect value for PSharp Binaries folder path.";
        public const string ExceptionOutputFolderPathMessage = "Incorrect value for Task Output folder path.";
        public const string ExceptionTaskWaitHoursMessage = "Incorrect value for Task Wait hours.";
        public const string ExceptionDeleteJobMessage = "Incorrect value for delete job after done.";
        public const string ExceptionDeleteContainerMessage = "Incorrect value for delete container after done.";
        public const string ExceptionApplicationPathMessage = "Incorrect value for application path in test case {0}.";
        public const string ExceptionParallelTaskMessage = "Incorrect value for Number of parallel task in command {0} of test case {1}. The value should be atleast 1.";
        public const string ExceptionIterationsMessage = "Incorrect value for number of iterations in command {0} of test case {1}. The value should be atlease 1.";
        public const string ExceptionCommandFlagsMessage = "Incorrect value for Command flags in command {0} of test case {1}.";
        public const string ExceptionCommandNameMessage = "Incorrect value for Command Name in command {0} of test case {1}.";
        public const string ExceptionSchedulingStatergyMessage = "Incorrect value for Scheduling Stratergy in command {0} of test case {1}.";
        public const string ExceptionNoTestEntityMessage = "No test cases provided in the config file.";
        public const string ExceptionTestEntityNullMessage = "One of the Test case is empty or incorrect. Please check the config file.";
        public const string ExceptionNoCommandEntityMessage = "No Command provided for the test case {0} in the config file.";
        public const string ExceptionCommandEntityNullMessage = "One of the commands in a test case {0} is empty or incorrect. Please check the config file.";
        public const string ExceptionBatchAccountNameMessage = "Incorrect value for Batch Account Name in Auth Config file.";
        public const string ExceptionBatchAccountUrlMessage = "Incorrect value for Batch Account URL in Auth Config file.";
        public const string ExceptionBatchAccountKeyMessage = "Incorrect value for Batch Account Key in Auth Config file.";
        public const string ExceptionStorageAccountNameMessage = "Incorrect value for Storage Account Name in Auth Config file.";
        public const string ExceptionStorageAccountKeyMessage = "Incorrect value for Storage Account Key in Auth Config file.";


        //Util Methods
        public static string GetTimeStamp()
        {
            //All time will be in UTC
            return DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
        }
    }
}
