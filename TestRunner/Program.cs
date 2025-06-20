using System;
using System.Threading.Tasks;

namespace TestRunner
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("GenHub Test Runner");
            Console.WriteLine("==================");
            Console.WriteLine();

            // Display fork requirements
            GitHubDiscoveryForkTestRunner.DisplayForkRequirements();

            // Run fork validation tests
            await GitHubDiscoveryForkTestRunner.RunForkValidationTests();

            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }
}
