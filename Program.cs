// Required dependencies for JSON processing, logging, and backend services
using Common.Utility;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Serilog;
using SpeedProcessing.BackEndService;
using SpeedProcessing.BackEndService.BLL;
using SpeedProcessing.BackEndService.Model;
using System;
using System.Diagnostics;

// Main program class for RDD SAP Service
class Program
{

    /// <summary>

    static async Task Main(string[] args)
    {



        Log.Logger = new LoggerConfiguration()
        .MinimumLevel.Debug()
        .WriteTo.Console()
        .WriteTo.File("logs.txt", rollingInterval: RollingInterval.Day)
        .CreateLogger();


        IConfiguration configuration = new ConfigurationBuilder()
       .SetBasePath(Directory.GetCurrentDirectory())
       .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
       .Build();

        // Create service collection
        var Servicescollection = new ServiceCollection();
        // Set up your dictionaries
        AppUtility.RabbitMQDict = configuration.GetSection("RabbitMQ").Get<Dictionary<string, string>>();
        AppUtility.DEQUEUEMAXCOUNT = Convert.ToInt16(AppUtility.RabbitMQDict["DEQUEUEMAXCOUNT"]);
       Servicescollection.AddTransient<IRabbitMQService, RabbitMQService>(provider =>
       new RabbitMQService(
           AppUtility.RabbitMQDict["STTaskQueueName"]
          ,
           provider.GetRequiredService<ILogger<RabbitMQService>>()
       )
    );

        Servicescollection.AddSingleton<IConfiguration>(configuration);

        Servicescollection.AddTransient<IService, SPService>(); // Register your Service class


        // Add logging to DI
        Servicescollection.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddSerilog(Log.Logger);
        });
        var serviceProvider = Servicescollection.BuildServiceProvider();


        var service = serviceProvider.GetRequiredService<IService>();

        Log.Information("SpeedProcessing.RDDPartService Start!");


        Stopwatch stopwatch = new Stopwatch();

      

        // Start the stopwatch before the code you want to measure
        stopwatch.Start();
        service.Process();
        // Initialize the Speed Processing service
        //SPService service = new SPService();




        

        stopwatch.Stop();

        // Get the elapsed time in milliseconds
        // Get the elapsed time in seconds
        double elapsedTimeSeconds = stopwatch.Elapsed.TotalSeconds;

        // Print the elapsed time
        Console.WriteLine($"Elapsed time: {elapsedTimeSeconds} seconds");
        Log.Information($"SpeedProcessing.RDDPartService Running time : {elapsedTimeSeconds} seconds");
        Log.CloseAndFlush();
    }

    private static void DownTipsAllDrawingPart(SPService service)
    {
        List<TipsPartDrawing> tippartlist = service.GetTipPartDraws();
        string vaulturl = "http://colwebprd01/stweb/pdmdata/Released/";
        var httpClient = new HttpClient();

        foreach (var tippart in tippartlist)
        {
            string filePath = Path.Combine(Utility.FolderPath, tippart.FileName); // Combine folder path and file name
            string apiurl = $"{vaulturl}{tippart.FileName}";
            Console.WriteLine($"Starting download {tippart.FileName}...");
            try
            {

                byte[] fileBytes = httpClient.GetByteArrayAsync(apiurl).GetAwaiter().GetResult();


                File.WriteAllBytes(filePath, fileBytes);

            }
            catch (Exception ex)
            {
                Log.Information("$\"Error downloading file {tippart.FileName}: {ex.Message}");
                //Console.WriteLine($"Error downloading file {tippart.FileName}: {ex.Message}");
            }
        }
    }

    // Performs daily ETSAC check by processing queries and checking for status changes
    private static void SPDailyETSACCheck(SPService service)
    {
        // Retrieve list of ETSAC daily queries
        List<Querystring> queries = service.GetETSACDailyList();
        string as400url = "https://linuxserviceas400.development.toyotatmh.io/AS400Api?servername=p&sql=";
        var httpClient = new HttpClient();

        // Process each query
        foreach (var item in queries)
        {
            // Construct URL for AS400 API call
            string url = $"{as400url}{item.querystring.Replace(";", "")}";

            // Execute HTTP request
            var response = httpClient.GetAsync(url).Result;

            if (response.IsSuccessStatusCode)
            {
                string responseContent = response.Content.ReadAsStringAsync().Result;

                var result = JsonConvert.DeserializeObject<List<Querystring>>(responseContent);

                // Check for status changes from 98 to 51
                if (result.Count > 0)
                {
                    Log.Information($"{responseContent} :  98 to 51 change found ");
                    Console.WriteLine($"{responseContent} ; {item.querystring} : 98 to 51 change found");
                }
            }
        }
    }

    // Test method for BOM (Bill of Materials) processing
    private static void TempBOMTest(SPService service)
    {
        // Create test entity with sample data
        ProcessObjectEntity testentity = new ProcessObjectEntity();
        testentity.tsdr = "E0NA";
        testentity.productCode = "G851";
        testentity.AddEntities = new List<SPPartData>();
        testentity.DelEntities = new List<SPDelPartData>();

        // Add test parts to the entity
        SPPartData addentity = new SPPartData();
        addentity.PartNumber = "57101-UCU1B-71";
        addentity.Qty = 1.ToString();
        testentity.AddEntities.Add(addentity);

        SPPartData addentity1 = new SPPartData();
        addentity1.PartNumber = "57420-UBTAR-71";
        addentity1.Qty = 1.ToString();
        testentity.AddEntities.Add(addentity1);

        //SPPartData addentity2 = new SPPartData();

        //addentity2.PartNumber = "82310-UNMJD-71";
        //addentity2.Qty = 1.ToString();
        //testentity.AddEntities.Add(addentity2);
        var resstr = service.ProcessSPSAP(testentity);
    }
}
