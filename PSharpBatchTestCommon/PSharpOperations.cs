using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static PSharpBatchTestCommon.PSharpBatchConfig;

namespace PSharpBatchTestCommon
{
    public static class PSharpOperations
    {
        public static void ParseCommandEntities(Dictionary<string, string> DeclareDictionary, ref PSharpCommandEntities entity)
        {
            var flags = new StringBuilder();
            List<string> wordlist = entity.CommandFlags.Split(' ').ToList();

            List<string> removeWords = new List<string>();
            List<string> addWords = new List<string>();
            foreach(var word in wordlist)
            {
                if (word.Contains("%"))
                {
                    int StartIndex = word.IndexOf("%");
                    int LastIndex = word.LastIndexOf("%");
                    string toReplaceKey = word.Substring(StartIndex + 1, (LastIndex - 1 - StartIndex));
                    string toReplace = word.Substring(StartIndex, (LastIndex - StartIndex + 1));
                    string value = null; 
                    if (DeclareDictionary.ContainsKey(toReplaceKey))
                    {
                        value = DeclareDictionary[toReplaceKey];
                    }
                    else
                    {
                        value = Environment.ExpandEnvironmentVariables(toReplace);
                    }
                    string newWord = word.Replace(toReplace, value);

                    addWords.Add(newWord);
                    removeWords.Add(word);
                }
            }

            foreach(var word in removeWords)
            {
                wordlist.Remove(word);
            }

            foreach(var word in addWords)
            {
                foreach(var w in word.Split(' '))
                {
                    wordlist.Add(w);
                }
            }
            
            foreach (var word in wordlist)
            {
                if (word.Length == 0)
                {
                    continue;
                }

                else if (word.StartsWith("/parallel:"))
                {
                    //contains parallel
                    entity.NumberOfParallelTasks = int.Parse(word.Substring("/parallel:".Length));
                }
                else if (word.StartsWith("/sch:"))
                {
                    entity.SchedulingStratergy = word.Substring("/sch:".Length);
                }
                else if (word.StartsWith("/test:"))
                {
                    //Ignore this flag
                }
                else if (word.Contains("PSharpTester.exe"))
                {
                    //Do nothing
                }
                else
                {
                    //Just directly add to the flag string
                    flags.Append(word);
                    flags.Append(" ");
                }
            }

            entity.CommandFlags = flags.ToString();
        }

        public static void MergeOutputCoverageReport(string outputDirectory, string BinaresPath)
        {
            if (!Directory.Exists(outputDirectory))
            {
                return;
            }

            var directories = Directory.EnumerateDirectories(outputDirectory);
            var files = Directory.EnumerateFiles(outputDirectory);
            if (files.Count() > 0 && files.Where(f => f.EndsWith(".sci")).Count() > 0)
            {
                MergeSciFiles(files.Where(f => f.EndsWith(".sci")).ToList(), outputDirectory, BinaresPath);
            }

            foreach (var directory in directories)
            {
                MergeOutputCoverageReport(directory, BinaresPath);
            }
        }

        private static void MergeSciFiles(List<string> SciFiles, string DirectoryPath, string BinariesPath)
        {
            var mergerPath = Path.Combine(BinariesPath, "PSharpCoverageReportMerger.exe");
            string command = string.Format(Constants.PSharpCoverageReportMergerCommandTemplate, DirectoryPath, mergerPath, string.Join(" ", SciFiles));
            var mergeProcess = Process.Start("cmd", command);
            mergeProcess.WaitForExit();
        }

        /// <summary>
        /// Splitting a list into smaller chunks
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="me"></param>
        /// <param name="size"></param>
        /// <returns></returns>
        public static List<List<T>> SplitList<T>(this List<T> me, int size)
        {
            var list = new List<List<T>>();
            for (int i = 0; i < me.Count; i += size)
                list.Add(me.GetRange(i, Math.Min(size, me.Count - i)));
            return list;
        }

    }
}
