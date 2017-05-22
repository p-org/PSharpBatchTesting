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
    public class PSharpOperations
    {

        public static void ParseCommandEntities(ref PSharpCommandEntities entity)
        {
            var flags = new StringBuilder();
            var wordlist = entity.CommandFlags.Split(' ');
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
                else if (word.StartsWith("/i:"))
                {
                    //contains parallel
                    entity.IterationsPerTask = int.Parse(word.Substring("/i:".Length));
                }
                else if (word.StartsWith("/sch:"))
                {
                    //contains parallel
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


    }
}
