using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PSharpBatchTestCommon
{
    public class PSharpBatchAuthConfig
    {
        // Batch account credentials
        public string BatchAccountName;
        public string BatchAccountKey;
        public string BatchAccountUrl;

        // Storage account credentials
        public string StorageAccountName;
        public string StorageAccountKey;

        public void SaveAsXML(string path)
        {
            using (FileStream fileStream = new FileStream(path, FileMode.Create))
            {
                this.XMLSerialize(fileStream);
                fileStream.Close();
            }
        }

        public static PSharpBatchAuthConfig LoadFromXML(string path)
        {
            PSharpBatchAuthConfig config = null;
            using (FileStream fileStream = new FileStream(path, FileMode.Open))
            {
                config = XMLDeserialize(fileStream);
                fileStream.Close();
            }
            config.Validate();
            return config;
        }

        public void XMLSerialize(Stream writeStream)
        {
            System.Xml.Serialization.XmlSerializer xmlSerializer = new System.Xml.Serialization.XmlSerializer(typeof(PSharpBatchAuthConfig));
            xmlSerializer.Serialize(writeStream, this);
        }

        public static PSharpBatchAuthConfig XMLDeserialize(Stream readStream)
        {
            System.Xml.Serialization.XmlSerializer xmlSerializer = new System.Xml.Serialization.XmlSerializer(typeof(PSharpBatchAuthConfig));
            return xmlSerializer.Deserialize(readStream) as PSharpBatchAuthConfig;
        }

        public void Validate()
        {
            if (string.IsNullOrEmpty(BatchAccountName))
            {
                throw new PSharpConfigValidateException(Constants.ExceptionBatchAccountNameMessage);
            }
            if (string.IsNullOrEmpty(BatchAccountKey))
            {
                throw new PSharpConfigValidateException(Constants.ExceptionBatchAccountKeyMessage);
            }
            if (string.IsNullOrEmpty(BatchAccountUrl))
            {
                throw new PSharpConfigValidateException(Constants.ExceptionBatchAccountUrlMessage);
            }
            if (string.IsNullOrEmpty(StorageAccountName))
            {
                throw new PSharpConfigValidateException(Constants.ExceptionStorageAccountNameMessage);
            }
            if (string.IsNullOrEmpty(StorageAccountKey))
            {
                throw new PSharpConfigValidateException(Constants.ExceptionStorageAccountKeyMessage);
            }
        }
    }
}
