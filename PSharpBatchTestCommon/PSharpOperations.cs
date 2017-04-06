﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static PSharpBatchTestCommon.PSharpBatchConfig;

namespace PSharpBatchTestCommon
{
    public class PSharpOperations
    {

        public PSharpCommandEntities GetCommandEntities(string PSharpTestCommand)
        {
            PSharpCommandEntities entities = new PSharpCommandEntities();
            var flags = new StringBuilder();
            var wordlist = PSharpTestCommand.Split(' ');
            foreach (var word in wordlist)
            {
                if (word.Length == 0)
                {
                    continue;
                }
                else if (word.StartsWith("/parallel:"))
                {
                    //contains parallel
                    entities.NumberOfParallelTasks = int.Parse(word.Substring("/parallel:".Length));
                }
                else if (word.StartsWith("/i:"))
                {
                    //contains parallel
                    entities.IterationsPerTask = int.Parse(word.Substring("/i:".Length));
                }
                else if (word.StartsWith("/test:"))
                {
                    entities.TestApplicationPath = word.Substring("/test:".Length);
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

            entities.CommandFlags = flags.ToString();

            return entities;
        }
    }
}
