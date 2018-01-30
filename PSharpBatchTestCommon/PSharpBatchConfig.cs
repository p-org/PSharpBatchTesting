using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using static PSharpBatchTestCommon.PSharpOperations;

namespace PSharpBatchTestCommon
{
    public class PSharpBatchConfig
    {

        //Job and pool details
        public string PoolId;
        public string JobDefaultId;

        //Task Details
        public string TaskDefaultId;

        //Storage Constants
        public int BlobContainerExpiryHours;

        //Node Details
        public int NumberOfNodesInPool;
        public string NodeOsFamily; //Default Value : 5
        public string NodeVirtualMachineSize; //Default value : small
        public int NodeMaxConcurrentTasks; //Default value : 1

        //File Details
        public string PSharpBinariesFolderPath;

        //Output
        public string OutputFolderPath;

        //Task Wait Time
        public int TaskWaitHours;

        //Delete job
        public bool DeleteJobAfterDone;

        //Delete containers
        public bool DeleteContainerAfterDone;

        //Delete pool
        public bool DeletePoolAfterDone;

        //Run PSharpTester locally
        public bool RunLocally;

        //Run and monitor
        [XmlIgnore]
        public bool MonitorBatch;
        [XmlIgnore]
        public string BatchFilePath;
        

        public static Dictionary<string, string> DeclareDictionary = new Dictionary<string, string>();

        [XmlArray("Declarations")]
        [XmlArrayItem("Declare")]
        public List<DeclareVariables> Variables;

        public class DeclareVariables
        {
            [XmlAttribute("Name")]
            public string Name;
            [XmlAttribute("Value")]
            public string Value;
        }

        [XmlArray("Tests")]
        [XmlArrayItem("Test")]
        public List<PSharpTestEntities> TestEntities;

        public class PSharpTestEntities
        {
            [XmlAttribute("Name")]
            public string TestName;

            public string ApplicationPath;

            [XmlElement("Command")]
            public PSharpCommandEntities[] CommandEntities;

            public int NumberOfTasks()
            {
                int numberOfTasks = 0;

                foreach(var cEntity in CommandEntities)
                {
                    numberOfTasks += cEntity.NumberOfParallelTasks;
                }

                return numberOfTasks;
            }
        }

        public class PSharpCommandEntities
        {
            [XmlIgnore]
            public int NumberOfParallelTasks;
            [XmlIgnore]
            public string SchedulingStratergy;
            [XmlAttribute("Flags")]
            public string CommandFlags;
            [XmlAttribute("Name")]
            public string CommandName;
            

            public PSharpCommandEntities()
            {
                NumberOfParallelTasks = 1;
            }

            public override string ToString()
            {
                string format = "NumberOfParallelTasks:{0}\nCommandFlags:{1}";
                return string.Format(format, NumberOfParallelTasks);
            }
        }

        public PSharpBatchConfig()
        {
            //Default Values
            this.NodeOsFamily = "5";
            this.NodeVirtualMachineSize = "small";
            this.DeletePoolAfterDone = false;
            this.NodeMaxConcurrentTasks = 1;
        }

        public void SaveAsXML(string path)
        {
            using(FileStream fileStream = new FileStream(path, FileMode.Create))
            {
                this.XMLSerialize(fileStream);
                fileStream.Close();
            }
        }

        public static PSharpBatchConfig LoadFromXML(string path)
        {
            PSharpBatchConfig config = null;
            using(FileStream fileStream = new FileStream(path, FileMode.Open))
            {
                config = XMLDeserialize(fileStream);
                fileStream.Close();
            }
            //config.ValidateAndParse();
            return config;
        }

        public static void SetVariables()
        {

        }

        public void XMLSerialize(Stream writeStream)
        {
            System.Xml.Serialization.XmlSerializer xmlSerializer = new System.Xml.Serialization.XmlSerializer(typeof(PSharpBatchConfig));
            xmlSerializer.Serialize(writeStream, this);
        }

        public static PSharpBatchConfig XMLDeserialize(Stream readStream)
        {
            System.Xml.Serialization.XmlSerializer xmlSerializer = new System.Xml.Serialization.XmlSerializer(typeof(PSharpBatchConfig));
            return xmlSerializer.Deserialize(readStream) as PSharpBatchConfig;
        }

        public void ValidateAndParse()
        {
            //Validate all the properties

            if (string.IsNullOrEmpty(PoolId))
            {
                throw new PSharpConfigValidateException(Constants.ExceptionPoolIdMessage);
            }
            if (string.IsNullOrEmpty(JobDefaultId))
            {
                throw new PSharpConfigValidateException(Constants.ExceptionJobIdMessage);
            }
            if(JobDefaultId.Length > 20)
            {
                throw new PSharpConfigValidateException(Constants.ExceptionJobIdLengthMessage);
            }
            if (string.IsNullOrEmpty(TaskDefaultId))
            {
                throw new PSharpConfigValidateException(Constants.ExceptionTaskIdMessage);
            }
            if (NumberOfNodesInPool<2)
            {
                throw new PSharpConfigValidateException(Constants.ExceptionNumNodesMessage);
            }

            int tempOsVal = 0;
            if (string.IsNullOrEmpty(NodeOsFamily) || !int.TryParse(NodeOsFamily, out tempOsVal))
            {
                throw new PSharpConfigValidateException(Constants.ExceptionNodeOsFamilyMessage);
            }

            if (string.IsNullOrEmpty(NodeVirtualMachineSize))
            {
                throw new PSharpConfigValidateException(Constants.ExceptionNodeVirtualMachineSizeMessage);
            }
            PSharpBinariesFolderPath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(PSharpBinariesFolderPath));
            if (string.IsNullOrEmpty(PSharpBinariesFolderPath) || !Directory.Exists(PSharpBinariesFolderPath))
            {
                throw new PSharpConfigValidateException(Constants.ExceptionPSharpBinariesPathMessage);
            }

            if (string.IsNullOrEmpty(OutputFolderPath))
            {
                throw new PSharpConfigValidateException(Constants.ExceptionOutputFolderPathMessage);
            }

            if (TaskWaitHours < 1)
            {
                throw new PSharpConfigValidateException(Constants.ExceptionTaskWaitHoursMessage);
            }

            if (MonitorBatch)
            {
                if(!string.IsNullOrEmpty(BatchFilePath) && !File.Exists(BatchFilePath))
                {
                    throw new PSharpConfigValidateException(Constants.ExceptionBatchFileNotFoundMessage);
                }
            }

            if(null == TestEntities || TestEntities.Count == 0)
            {
                throw new PSharpConfigValidateException(Constants.ExceptionNoTestEntityMessage);
            }

            foreach(var variable in Variables)
            {
                if(!DeclareDictionary.ContainsKey(variable.Name))
                    DeclareDictionary.Add(variable.Name, variable.Value);
            }

            foreach(var tEntity in TestEntities)
            {
                if(null == tEntity)
                {
                    throw new PSharpConfigValidateException(Constants.ExceptionTestEntityNullMessage);
                }
                if(null == tEntity.TestName)
                {
                    tEntity.TestName = string.Empty;
                }
                tEntity.ApplicationPath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(tEntity.ApplicationPath));
                if (string.IsNullOrEmpty(tEntity.ApplicationPath) || !File.Exists(tEntity.ApplicationPath))
                {
                    throw new PSharpConfigValidateException(string.Format(Constants.ExceptionApplicationPathMessage, TestEntities.IndexOf(tEntity)));
                }

                if(null == tEntity.CommandEntities || tEntity.CommandEntities.Count() == 0)
                {
                    throw new PSharpConfigValidateException(string.Format(Constants.ExceptionNoCommandEntityMessage, TestEntities.IndexOf(tEntity)));
                }

                for(int i = 0; i < tEntity.CommandEntities.Count(); i++)
                {
                    var cEntity = tEntity.CommandEntities[i];

                    PSharpOperations.ParseCommandEntities(DeclareDictionary, ref cEntity);

                    if(null == cEntity)
                    {
                        throw new PSharpConfigValidateException(Constants.ExceptionCommandEntityNullMessage);
                    }

                    if (cEntity.NumberOfParallelTasks < 1)
                    {
                        throw new PSharpConfigValidateException(string.Format(Constants.ExceptionParallelTaskMessage, i, TestEntities.IndexOf(tEntity)));
                    }
                    if (string.IsNullOrEmpty(cEntity.CommandName))
                    {
                        throw new PSharpConfigValidateException(string.Format(Constants.ExceptionCommandNameMessage, i, TestEntities.IndexOf(tEntity)));
                    }

                    //Todo : check list of supported schedulers
                    //if (!string.IsNullOrEmpty(cEntity.SchedulingStratergy) && !cEntity.SchedulingStratergy.StartsWith("/sch:"))
                    //{
                    //    throw new PSharpConfigValidateException(string.Format(Constants.ExceptionSchedulingStatergyMessage, i, TestEntities.IndexOf(tEntity)));
                    //}
                }
            }
        }
    }
}
