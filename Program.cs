using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Codli_GCI
{
    class Program
    {
        //Codli GCI - Github Continuous Integration
        private static string dataDirectory = Path.GetFullPath(Directory.GetCurrentDirectory()); // <-- exe's directory
        private static string shadowFile = Path.Combine(dataDirectory, "shadow.cdl");

        static void Main(string[] args)
        {
            /** Parms:
             * -l [github-login]
             * -p [github-password]
             * -r [github-repository]
             * -p [repository-platform] (dotnet-core) //includes building repo
             * -d [build-destination]
             * -off [service-name] (service which sould be disabled while CI)
             * -c [command] (additional command after all)
             */
            Console.Title = $"Codli GCI [v{Assembly.GetEntryAssembly().GetName().Version}]";
            Console.WriteLine("Welcome in Codli GCI!");                
        }

        private static string GetGithubLogin()
        {
            Console.WriteLine("Enter your Github login:");
            return Console.ReadLine();
        }
    }
}
