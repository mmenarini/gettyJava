using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace gettyJava
{
    class Program
    {
        private static string exeName;
        private static string dirName;
        static Program() {
            var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
            exeName = currentProcess.MainModule.FileName;
            dirName = Path.GetDirectoryName(exeName);
        }
        private static string getConfigFile()
        {
            //var exeName = System.Reflection.Assembly.GetAssembly(typeof(Program)).CodeBase; //ExecutingAssembly().Location;
            //var exeName = System.Reflection.Assembly.GetExecutingAssembly().Location;
            return Path.GetFullPath("gettyJava.config", dirName);
        }
        private static string getEmbeddedConfigFile()
        {
            //var exeName = System.Reflection.Assembly.GetAssembly(typeof(Program)).CodeBase; //ExecutingAssembly().Location;
            //var exeName = System.Reflection.Assembly.GetExecutingAssembly().Location;
            return $"{exeName}:gettyJava.config";
        }

        private static string getConfigValueFor(IEnumerable<string> lines, string key)
        {
            return lines
                .Where((s, i) => s.StartsWith(key))
                .First().Split("=").Last().Trim();
        }

        private static string[] FixStrings(IEnumerable<string> args)
        {
            var ret = args.Select(s => s.Contains(" ") ? $"\"{s.Replace("\"", "\"\"")}\"" : s).ToArray() ;
            return ret == null ? new string[0] : ret;
        }

        private static int runJava(IEnumerable<string> args)
        {
            using (var process = new Process())
            {
                process.StartInfo.FileName = @"java.exe";
                var cmdStr = $"{string.Join(" ", args)}";
                process.StartInfo.Arguments = cmdStr;
                
                Console.Out.WriteLine($"Executing command:\n{process.StartInfo.FileName} {process.StartInfo.Arguments}");

                //process.StartInfo.Arguments = @"/c dir";      // print the current working directory information
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.RedirectStandardInput= true;

                process.OutputDataReceived += (sender, data) => Console.Out.WriteLine(data.Data);
                process.ErrorDataReceived += (sender, data) => Console.Error.WriteLine(data.Data);
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                var buffer = new char[8000];
                var InThread = new Thread(() =>
                {
                    Thread.CurrentThread.IsBackground = true;
                    /* run your code here */
                    while (true)
                    {
                        try
                        {
                            var buffLen = Console.In.ReadBlock(buffer, 0, 8000);
                            process.StandardInput.Write(buffer, 0, buffLen);
                        }
                        catch (Exception e)
                        {

                        }
                    }
                });
                InThread.Start();
                process.WaitForExit();     // (optional) wait up to 10 seconds
                return process.ExitCode;
            }
        }

        private static string ProcessInfo()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
            sb.AppendLine("Process information");
            sb.AppendLine("-------------------");
            sb.AppendLine("CPU time");
            sb.AppendLine(string.Format("\tTotal       {0}",
                currentProcess.TotalProcessorTime));
            sb.AppendLine(string.Format("\tUser        {0}",
                currentProcess.UserProcessorTime));
            sb.AppendLine(string.Format("\tPrivileged  {0}",
                currentProcess.PrivilegedProcessorTime));
            sb.AppendLine("Memory usage");
            sb.AppendLine(string.Format("\tCurrent     {0:N0} B", currentProcess.WorkingSet64));
            sb.AppendLine(string.Format("\tPeak        {0:N0} B", currentProcess.PeakWorkingSet64));
            sb.AppendLine(string.Format("Active threads      {0:N0}", currentProcess.Threads.Count));
            sb.AppendLine("-------------------");
            sb.AppendLine(string.Format("Process Name  {0}", currentProcess.ProcessName));
            sb.AppendLine(string.Format("File Name     {0}", currentProcess.MainModule.FileName));

            return sb.ToString();
        }
        static int Main(string[] args)
        {
            Console.Out.WriteLine($"gettyJava called with args {string.Join(" ", args)}");
            //if (args.Length == 1 && args[0].Equals("@CONFIG@"))
            //{
            //    Console.WriteLine(ProcessInfo());
            //    var configFileLocation = getConfigFile();
            //    Console.Out.WriteLine($"gettyJava.config located at {configFileLocation}");
            //    using (var output = new StreamWriter(File.Open(
            //        getEmbeddedConfigFile(),FileMode.Create,FileAccess.Write)))
            //    {
            //        var configFileStr = File.OpenText(configFileLocation).ReadToEnd();
            //        output.Write(configFileStr);
            //    }
            //    return 0;
            //}
            Console.Out.WriteLine($"gettyJava.config located at {getConfigFile()}");
            var config = File.OpenText(getConfigFile()).ReadToEnd();
            var configLines = config.Split(
                new[] { Environment.NewLine },
                StringSplitOptions.None);
            //var configLinesTrimmed = configLines.Select(s => s.Trim());
            var OutputFile = getConfigValueFor(configLines, "OUTPUT_FILE");
            var DaikonJar = getConfigValueFor(configLines, "DAIKON_JAR");
            var Pattern = getConfigValueFor(configLines, "PATTERN");
            Console.WriteLine($"OutputFile {OutputFile}");
            Console.WriteLine($"DaikonJar {DaikonJar}");
            Console.WriteLine($"Pattern {Pattern}");
            if (args.Length>0 && "-version".Equals(args[0]))
                return runJava(args);
            var pre = new LinkedList<string>();
            var post = new LinkedList<string>();

            var isPost = true;
            var last = "";

            foreach (var s in args)
            {
                if (isPost)
                {
                    if (!s.StartsWith('-'))
                    {
                        if (!last.Equals("-cp"))
                        {
                            isPost = false;
                            post.AddLast(s);
                        }
                        else
                        {
                            pre.AddLast($"{DaikonJar};{s}");
                        }
                        last = s;
                        continue;
                    }
                    pre.AddLast(s);
                    last = s;
                }
                else
                {
                    post.AddLast(s);
                }
            }

            var preComputed = FixStrings(pre);
            var postComputed = FixStrings(post);
            Console.Out.WriteLine($"PRE: {string.Join(" ", preComputed)}");
            Console.Out.WriteLine($"POST: {string.Join(" ", postComputed)}");

            var newArgs = new List<string>();
            newArgs.AddRange(preComputed);
            newArgs.Add($"daikon.Chicory --daikon-online --daikon-args=\"--no_text_output -o {OutputFile}\" --ppt-select-pattern=\"{Pattern}\"");
            newArgs.AddRange(postComputed);
            return runJava(newArgs);
        }
    }
}
