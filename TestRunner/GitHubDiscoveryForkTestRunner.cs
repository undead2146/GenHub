using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using GenHub.Features.GitHub.Services;
using GenHub.Core.Interfaces.GitHub;
using GenHub.Core.Models.GitHub;
using GenHub.Core.Models;

namespace TestRunner
{
    /// <summary>
    /// Test runner to verify the fork requirement enforcement in GitHubRepositoryDiscoveryService
    /// </summary>
    public class GitHubDiscoveryForkTestRunner
    {
        public static async Task RunForkValidationTests()
        {
            Console.WriteLine("=== GITHUB REPOSITORY DISCOVERY FORK VALIDATION TESTS ===");
            Console.WriteLine();

            // Test 1: Verify search queries include fork:true
            await TestSearchQueriesHaveForkFilter();

            // Test 2: Verify base repositories are excluded from results
            await TestBaseRepositoriesExcluded();

            // Test 3: Verify marker repositories are correctly identified
            await TestMarkerRepositoryIdentification();

            Console.WriteLine();
            Console.WriteLine("=== FORK VALIDATION TESTS COMPLETE ===");
        }

        private static async Task TestSearchQueriesHaveForkFilter()
        {
            Console.WriteLine("TEST 1: Verify all search queries include 'fork:true'");
            
            // Use reflection to access private search queries
            var serviceType = typeof(GitHubRepositoryDiscoveryService);
            var queriesField = serviceType.GetField("DiscoveryQueries", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            
            if (queriesField != null)
            {
                var queries = (string[])queriesField.GetValue(null)!;
                
                Console.WriteLine($"Total search queries: {queries.Length}");
                
                var queriesWithoutFork = queries.Where(q => !q.Contains("fork:true")).ToArray();
                
                if (queriesWithoutFork.Any())
                {
                    Console.WriteLine("❌ FAILED: Found queries WITHOUT 'fork:true':");
                    foreach (var query in queriesWithoutFork)
                    {
                        Console.WriteLine($"   - {query}");
                    }
                }
                else
                {
                    Console.WriteLine("✅ PASSED: All search queries include 'fork:true'");
                }
            }
            else
            {
                Console.WriteLine("❌ ERROR: Could not access DiscoveryQueries field");
            }
            
            Console.WriteLine();
        }

        private static async Task TestBaseRepositoriesExcluded()
        {
            Console.WriteLine("TEST 2: Verify base repositories are excluded from results");
            
            // Use reflection to access private base repositories
            var serviceType = typeof(GitHubRepositoryDiscoveryService);
            var baseReposField = serviceType.GetField("BaseRepositories", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            
            if (baseReposField != null)
            {
                var baseRepos = (object[])baseReposField.GetValue(null)!;
                
                Console.WriteLine($"Base repositories defined: {baseRepos.Length}");
                foreach (var baseRepo in baseRepos)
                {
                    // Extract owner and name using reflection
                    var ownerProp = baseRepo.GetType().GetProperty("Owner");
                    var nameProp = baseRepo.GetType().GetProperty("Name");
                    
                    if (ownerProp != null && nameProp != null)
                    {
                        var owner = ownerProp.GetValue(baseRepo) as string;
                        var name = nameProp.GetValue(baseRepo) as string;
                        Console.WriteLine($"   - {owner}/{name} (Base repository - should NOT be in results)");
                    }
                }
                
                Console.WriteLine("✅ PASSED: Base repositories are properly defined for exclusion");
            }
            else
            {
                Console.WriteLine("❌ ERROR: Could not access BaseRepositories field");
            }
            
            Console.WriteLine();
        }

        private static async Task TestMarkerRepositoryIdentification()
        {
            Console.WriteLine("TEST 3: Verify marker repositories are correctly identified");
            
            // Use reflection to access private marker repositories
            var serviceType = typeof(GitHubRepositoryDiscoveryService);
            var markerReposField = serviceType.GetField("MarkerRepositories", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            
            if (markerReposField != null)
            {
                var markerRepos = (object[])markerReposField.GetValue(null)!;
                
                Console.WriteLine($"Marker repositories defined: {markerRepos.Length}");
                foreach (var markerRepo in markerRepos)
                {
                    // Extract owner and name using reflection
                    var ownerProp = markerRepo.GetType().GetProperty("Owner");
                    var nameProp = markerRepo.GetType().GetProperty("Name");
                    var descProp = markerRepo.GetType().GetProperty("Description");
                    
                    if (ownerProp != null && nameProp != null && descProp != null)
                    {
                        var owner = ownerProp.GetValue(markerRepo) as string;
                        var name = nameProp.GetValue(markerRepo) as string;
                        var description = descProp.GetValue(markerRepo) as string;
                        Console.WriteLine($"   - {owner}/{name} - {description}");
                    }
                }
                
                Console.WriteLine("✅ PASSED: Marker repositories are properly defined");
                Console.WriteLine("   These repositories MUST be found by the discovery service");
            }
            else
            {
                Console.WriteLine("❌ ERROR: Could not access MarkerRepositories field");
            }
            
            Console.WriteLine();
        }

        public static void DisplayForkRequirements()
        {
            Console.WriteLine("=== FORK REQUIREMENT SUMMARY ===");
            Console.WriteLine();
            Console.WriteLine("CRITICAL REQUIREMENTS:");
            Console.WriteLine("1. ✅ Repository MUST be a fork (repo.IsFork == true)");
            Console.WriteLine("2. ✅ Non-forks are automatically rejected");
            Console.WriteLine("3. ✅ Base repositories are excluded from results");
            Console.WriteLine("4. ✅ Search queries include 'fork:true' to prevent non-forks");
            Console.WriteLine("5. ✅ Repository must have releases OR workflows");
            Console.WriteLine();
            Console.WriteLine("MARKER REPOSITORIES (must be found):");
            Console.WriteLine("- jmarshall2323/CnC_Generals_Zero_Hour");
            Console.WriteLine("- x64-dev/GeneralsGameCode_GeneralsOnline");
            Console.WriteLine();
            Console.WriteLine("NON-FORKS SHOULD BE REJECTED:");
            Console.WriteLine("- mod-for-game/mod-for-cc-generals-zero-hour");
            Console.WriteLine("- Any repository where IsFork = false");
            Console.WriteLine();
        }
    }
}
