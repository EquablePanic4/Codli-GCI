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

        static void Main(string[] args)
        {
            /** Parms:
             * -l [github-login]
             * -p [github-password]
             * -r [github-repository]
             * -o [github-organisation]
             * -b [github-branch]
             * --runtime [repository-platform] (dotnet-core) //includes building repo
             * --build-configuration [.NET Core configuration]
             * --upddate [database ...]
             * --secrets [path_to_file] - one line, one secret
             * --destination [build-destination]
             * --off [service-name] (service which sould be disabled while CI)
             * --command [command] (additional command after all)
             * --config [path] (load config file with commands)
             */
            Console.Title = $"Codli GCI [v{Assembly.GetEntryAssembly().GetName().Version}]";
            Console.WriteLine("Welcome in Codli GCI!");
            
            if (args.Length == 0 || args.Length % 2 == 1)
            {
                Console.WriteLine("An error occured with number of parms");
                return;
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

            //Get temp directory
            if (Directory.Exists(dataDirectory))
                Directory.Delete(dataDirectory);

            Directory.CreateDirectory(dataDirectory);

            //Now we can create git command
            return $"git -C {dataDirectory} clone{branchCmd}{repoUri} ."; // <-- . means that we won't clone root directory
        }

        private static bool IntegrateGitProject(Dictionary<string, string> dict)
        {
            //If we need to update EFCore DB, simply we do it
            if (dict.ContainsKey("--upddate"))
            {
                switch (dict["--upddate"])
                {
                    case "database":
                        return UpdateEFCoreDb(dict);

                    default:
                        return false;
                }
            }

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

                return $"dotnet build {dataDirectory}{buildConfig} -o {dict["--destination"]}";
            }

            throw new NotImplementedException();
        }

        private static bool SetDotnetSecrets(Dictionary<string, string> dict)
        {

        }

        private static bool UpdateEFCoreDb(Dictionary<string, string> dict)
        {
            Console.WriteLine("Updating EF Core database...");
            RunCommand("dotnet ef database update");
            return true;
        }

        private static void PrepareBuildDestination(Dictionary<string, string> dict)
        {

        }

        private static string ChangeServiceState(string service, bool state)
        {
            if (state)
                return RunCommand($"sudo systemctl start {service}");

            return RunCommand($"sudo systemctl stop {service}");
        }

        #endregion

        #region Helpers

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
