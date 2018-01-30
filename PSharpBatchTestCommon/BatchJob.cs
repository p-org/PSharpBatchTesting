using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PSharpBatchTestCommon
{
	public class BatchJob
	{
		public string PoolID { get; set; }

		public string JobID { get; set; }

		public List<string> InputContainerIDs { get; set; }

		public string  JobManagerContainerID { get; set; }

		public string OutputContainerID { get; set; }

		public void SaveAsXML(string path)
		{
			using (FileStream fileStream = new FileStream(path, FileMode.Create))
			{
				this.XMLSerialize(fileStream);
				fileStream.Close();
			}
		}

		public static BatchJob LoadFromXML(string path)
		{
			BatchJob config = null;
			using (FileStream fileStream = new FileStream(path, FileMode.Open))
			{
				config = XMLDeserialize(fileStream);
				fileStream.Close();
			}
			//config.ValidateAndParse();
			return config;
		}

		public void XMLSerialize(Stream writeStream)
		{
			System.Xml.Serialization.XmlSerializer xmlSerializer = new System.Xml.Serialization.XmlSerializer(typeof(BatchJob));
			xmlSerializer.Serialize(writeStream, this);
		}

		public static BatchJob XMLDeserialize(Stream readStream)
		{
			System.Xml.Serialization.XmlSerializer xmlSerializer = new System.Xml.Serialization.XmlSerializer(typeof(BatchJob));
			return xmlSerializer.Deserialize(readStream) as BatchJob;
		}
	}
}
