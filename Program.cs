using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Codli_GCI
{
    class Program
    {
        //Codli GCI - Github Continuous Integration
        private static string dataDirectory = "/tmp/codli-gci";
        private static string logsFile = String.Empty;

        static void Main(string[] args)
        {
            /** Parms:
             * -l [github-login]
             * -p [github-password]
             * -r [github-repository]
             * -o [github-organisation]
             * -b [github-branch]
             * --logs [path_to_logs_file] <-- only if we want logs
             * --runtime [repository-platform] (dotnet-core) //includes building repo
             * --build-configuration [.NET Core configuration]
             * --upddate [database ...]
             * --secrets [path_to_file] - one line, one secret
             * --destination [build-destination]
             * --off [service-name] (service which sould be disabled while CI)
             * --config [path] (load config file with commands)
             */
            Console.Title = $"Codli GCI [v{Assembly.GetEntryAssembly().GetName().Version}]";
            Console.WriteLine("Welcome in Codli GCI!");
            
            if (args.Length == 0 || args.Length % 2 == 1)
            {
                Console.WriteLine("An error occured with number of parms");
                return;
            }

            //Cleaning temp dir...
            if (!Directory.Exists(dataDirectory))
                Directory.CreateDirectory(dataDirectory);
            else
            {
                Directory.Delete(dataDirectory, true);
                Directory.CreateDirectory(dataDirectory);
            }

            //We need to change args array to dictionary
            var dict = new Dictionary<string, string>();
            for (var i = 0; i < args.Length; i += 2)
                dict.Add(args[i], args[i + 1]);

            //if dict contains --config, we need change our dict
            if (dict.ContainsKey("--config"))
            {
                var config = File.ReadAllText(dict["--config"]).Split(' ');
                for (var i = 0; i < config.Length; i += 2)
                {
                    if (dict.ContainsKey(config[i]))
                        dict[config[i]] = config[i + 1];
                    else
                        dict.Add(config[i], config[i + 1]);
                }
            }

            //If we want logs, we must clean it out
            if (dict.ContainsKey("--logs"))
            {
                logsFile = dict["--logs"];

                if (File.Exists(logsFile))
                    File.Delete(logsFile);
            }

            //Now we can interpretate parameters
            CodliInterpreter(dict);
        }

        private static void CodliInterpreter(Dictionary<string, string> dictionary)
        {
            var executeResult = RunCommand(GetGitCommand(dictionary));
            if (executeResult.Contains("fatal"))
            {
                Console.WriteLine("ERROR: An error occured at clonning repository!");
                return;
            }

            Console.WriteLine("Successfully cloned git repository into temporary directory!");

            if (dictionary.ContainsKey("--runtime")) //We need to build this application
            {
                Console.WriteLine("Preparing to build project...");
                var integrationResult = IntegrateGitProject(dictionary);
            }
        }

        #region Subengines

        private static string GetGitCommand(Dictionary<string, string> dict)
        {
            //Firstival we check if there's login and password
            var credintialsPrefix = String.Empty;
            if (dict.ContainsKey("-l")) //It means that we have both - login and password
                credintialsPrefix = $"{dict["-l"]}:{dict["-p"]}@";

            var repoUri = $"https://{credintialsPrefix}github.com/{dict["-o"]}/{dict["-r"]}.git";

            //If we want to download specified branch, we need to use -b param, otherwise we're going to download default branch
            var branchCmd = " ";
            if (dict.ContainsKey("-b"))
                branchCmd = $" --branch {dict["-b"]} ";

            //Now we can create git command
            return $"git -C {dataDirectory} clone{branchCmd}{repoUri} ."; // <-- . means that we won't clone root directory
        }

        private static bool IntegrateGitProject(Dictionary<string, string> dict)
        {
            //If we need to update EFCore DB, simply we do it
            if (dict.ContainsKey("--update"))
            {
                switch (dict["--upddate"])
                {
                    case "database":
                        return UpdateEFCoreDb();

                    default:
                        return false;
                }
            }

            //If project contains secret keys, we must apply them
            if (dict.ContainsKey("--secrets"))
                SetDotnetSecrets(dict);

            //If we need to turn of service, we must do it
            if (dict.ContainsKey("--off"))
                ChangeServiceState(dict["--off"], false);

            //If we want to build app, we must do it
            if (dict.ContainsKey("--destination"))
            {
                CleanBuildDestination(dict);
                Console.WriteLine("Building project...");
                RunCommand(GetBuildCommand(dict));
                Console.WriteLine("Building project complete");
            }

            //If we turned off any service, we need to turn it on again.
            if (dict.ContainsKey("--off"))
                ChangeServiceState(dict["--off"], true);

            return false;
        }

        private static string GetBuildCommand(Dictionary<string, string> dict)
        {
            if (dict["--runtime"] == "dotnet-core")
            {
                //Build configuration
                var buildConfig = String.Empty;
                if (dict.ContainsKey("--build-configuration"))
                    buildConfig = $" --configuration {dict["--build-configuration"]}";

                Console.WriteLine("Preparing build destination...");

                if (Directory.Exists(dict["--destination"]))
                    Directory.Delete(dict["--destination"], true);

                Directory.CreateDirectory(dict["--destination"]);

                return $"dotnet publish {dataDirectory}{buildConfig} -o {dict["--destination"]}";
            }

            throw new NotImplementedException();
        }

        private static void CleanBuildDestination(Dictionary<string, string> dict)
        {
            if (Directory.Exists(dict["--destination"]))
                Directory.Delete(dict["--destination"], true);

            Directory.CreateDirectory(dict["--destination"]);
        }

        private static void SetDotnetSecrets(Dictionary<string, string> dict)
        {
            Console.WriteLine("Preparing to set user secrets...");
            var secrets = File.ReadAllLines(dict["--secrets"]);
            foreach (var secret in secrets)
            {
                //We need to file with secrets like "[...]":"[...]" <-- no spaces, no comas
                var arr = SplitSecretLine(secret);
                Console.WriteLine($"Setting secret value for {arr[0]}");
                RunCommand($"dotnet user-secrets set {arr[0]} {arr[1]}", dataDirectory);
            }
        }

        private static bool UpdateEFCoreDb()
        {
            Console.WriteLine("Updating EF Core database...");
            RunCommand("dotnet ef database update", dataDirectory);
            return true;
        }

        private static string ChangeServiceState(string service, bool state)
        {
            if (state)
            {
                Console.WriteLine($"Starting service {service}");
                return RunCommand($"sudo systemctl start {service}");
            }

            Console.WriteLine($"Stopping service {service}");
            return RunCommand($"sudo systemctl stop {service}");
        }

        #endregion

        #region Helpers

        private static string[] SplitSecretLine(string line)
        {
            var arr = line.Split('"');
            var list = new List<string>();

            foreach (var e in arr)
                if (!String.IsNullOrEmpty(e) && !String.IsNullOrWhiteSpace(e))
                    if (e.Replace(" ", null) != ":" && e.Replace(" ", null) != ",")
                        list.Add($"\"{e}\"");

            if (list.Count != 2)
            {
                Console.WriteLine($"Line with secret {line} is bad, there are {list.Count} arguments, but should be exactly 2");
                throw new ArgumentException();
            }

            return list.ToArray();
        }

        private static void LogOutput(string output)
        {
            if (!String.IsNullOrEmpty(logsFile))
                File.AppendAllText(logsFile, $"{output}\r\n");
        }

        private static string RunCommand(string command)
        {
            //Now only Linux is supported
            string result = String.Empty;
            using (System.Diagnostics.Process proc = new System.Diagnostics.Process())
            {
                proc.StartInfo.FileName = "/bin/bash";
                proc.StartInfo.Arguments = "-c \" " + command + " \"";
                proc.StartInfo.UseShellExecute = false;
                proc.StartInfo.RedirectStandardOutput = true;
                proc.StartInfo.RedirectStandardError = true;
                proc.Start();

                result += proc.StandardOutput.ReadToEnd();
                result += proc.StandardError.ReadToEnd();

                proc.WaitForExit();
            }

            LogOutput(result);

            return result;
        }

        private static string RunCommand(string command, string directory)
        {
            char q = '"';
            return RunCommand($"cd {q}{directory}{q} && {command}");
        }
        #endregion
    }
}
