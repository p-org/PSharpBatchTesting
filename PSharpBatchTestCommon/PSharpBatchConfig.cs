﻿using System;
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

        [XmlArray("Tests")]
        [XmlArrayItem("Test")]
        public List<PSharpTestEntities> TestEntities;

        public class PSharpTestEntities
        {
            public string ApplicationPath;

            [XmlElement("Command")]
            public PSharpCommandEntities[] CommandEntities;
        }

        public class PSharpCommandEntities
        {

            public int NumberOfParallelTasks;
            public int IterationsPerTask;
            public string CommandFlags;
            public string CommandName;
            public string SchedulingStratergy;

            public PSharpCommandEntities()
            {
                NumberOfParallelTasks = 1;
                IterationsPerTask = 1;
            }

            public override string ToString()
            {
                string format = "NumberOfParallelTasks:{0}\nIterations:{1}\nCommandFlags:{3}";
                return string.Format(format, NumberOfParallelTasks, IterationsPerTask, CommandFlags);
            }
        }

        public PSharpBatchConfig()
        {
            //Default Values
            this.NodeOsFamily = "5";
            this.NodeVirtualMachineSize = "small";
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
            config.Validate();
            return config;
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

        public void Validate()
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
            if (string.IsNullOrEmpty(TaskDefaultId))
            {
                throw new PSharpConfigValidateException(Constants.ExceptionTaskIdMessage);
            }
            if (!(BlobContainerExpiryHours>0) && BlobContainerExpiryHours != -1)
            {
                throw new PSharpConfigValidateException(Constants.ExceptionBlobExpiryMessage);
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

            if(null == TestEntities || TestEntities.Count == 0)
            {
                throw new PSharpConfigValidateException(Constants.ExceptionNoTestEntityMessage);
            }

            foreach(var tEntity in TestEntities)
            {
                if(null == tEntity)
                {
                    throw new PSharpConfigValidateException(Constants.ExceptionTestEntityNullMessage);
                }
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

                    if(null == cEntity)
                    {
                        throw new PSharpConfigValidateException(Constants.ExceptionCommandEntityNullMessage);
                    }

                    if (cEntity.NumberOfParallelTasks < 1)
                    {
                        throw new PSharpConfigValidateException(string.Format(Constants.ExceptionParallelTaskMessage, i, TestEntities.IndexOf(tEntity)));
                    }
                    if (cEntity.IterationsPerTask < 1)
                    {
                        throw new PSharpConfigValidateException(string.Format(Constants.ExceptionIterationsMessage, i, TestEntities.IndexOf(tEntity)));
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
