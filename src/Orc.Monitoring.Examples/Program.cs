namespace Orc.Monitoring.Examples;

using System;
using System.Threading.Tasks;

class Program
{
    static async Task Main(string[] args)
    {
        bool exit = false;
        while (!exit)
        {
            Console.Clear();
            Console.WriteLine("Orc.Monitoring Integration Examples");
            Console.WriteLine("====================================");
            Console.WriteLine("1. PerformanceMonitor Integration");
            Console.WriteLine("2. CallStack Integration");
            Console.WriteLine("3. Complex ClassMonitor Integration");
            Console.WriteLine("4. Exit");
            Console.Write("\nChoose an example to run (1-4): ");

            var choice = Console.ReadLine();
            Console.WriteLine();

            switch (choice)
            {
                case "1":
                    MonitoringIntegrationExamples.PerformanceMonitorIntegration();
                    break;
                case "2":
                    await MonitoringIntegrationExamples.CallStackIntegrationAsync();
                    break;
                case "3":
                    MonitoringIntegrationExamples.ComplexClassMonitorIntegration();
                    break;
                case "4":
                    exit = true;
                    continue;
                default:
                    Console.WriteLine("Invalid choice. Please try again.");
                    break;
            }

            Console.WriteLine("\nPress any key to continue...");
            Console.ReadKey();
        }

        Console.WriteLine("Thank you for exploring Orc.Monitoring examples!");
    }
}
