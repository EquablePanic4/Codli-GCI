using System;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Codli_GCI
{
    class Program
    {
        private static string dataDirectory;

        static void Main(string[] args)
        {
            Console.Title = $"Codli GCI [v{Assembly.GetEntryAssembly().GetName().Version}]";
            Console.WriteLine("Welcome in Codli GCI!");
        }

        private static bool CheckPassword(string password)
        {
            //We need to store user's Github credintials, so it must be protected by password
            //Password is right when first line of codli-data.shd equals "#Codli!"
            
        }
    }
}
