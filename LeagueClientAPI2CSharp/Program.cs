using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace LCAPI2CSharp
{
    public class Program
    {
        private static Process? ClientProcess;
        private static string LeagueClientPath = string.Empty;
        private static string RiotServicePath = string.Empty;
        private const string ConfigFile = "config.txt";

        private static readonly HttpClientHandler ComHandler = new HttpClientHandler()
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        };
        private static readonly HttpClient ComClient = new HttpClient(ComHandler);

        private static bool SelfLaunched = false;

        private static async Task Main(string[] args)
        {
            AppDomain.CurrentDomain.ProcessExit += new EventHandler(OnProcessExit);

            string programDataPath = Environment.GetEnvironmentVariable("programdata") ?? string.Empty;
            if(string.IsNullOrEmpty(programDataPath))
            {
                throw new Exception("Could not find program data. Please try again.");
            }

            string riotGamesDataFolder = Path.Combine(programDataPath, "Riot Games");
            if (Directory.Exists(riotGamesDataFolder))
            {
                string installsPath = Path.Combine(riotGamesDataFolder, "RiotClientInstalls.Json");
                if (File.Exists(installsPath))
                {
                    foreach(string line in File.ReadAllLines(installsPath))
                    {
                        if(line.Contains("rc_live"))
                        {
                            RiotServicePath = line.Substring(line.IndexOf(":") + 1).Replace("\"", "").Trim();
                        }
                    }
                }
            }

            try
            {
                File.Move(RiotServicePath, Path.Combine(Path.GetDirectoryName(RiotServicePath), "_RiotClientServices.exe"));
                RiotServicePath = Path.Combine(Path.GetDirectoryName(RiotServicePath), "_RiotClientServices.exe");
            }
            catch
            {
                Cleanup();
            }

            if (!HasClientPath())
            {
                Console.WriteLine("Please edit config.txt to include the install path to League of Legends and relaunch this application.\nPress enter to exit...");
                Console.ReadLine();
                return;
            }

            LeagueClientInfo lci = await GetLeagueClientInfo();

            ComClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"riot:{lci.Password}")));

            HttpResponseMessage response = await ComClient.GetAsync($"{lci.ComProtocol}://127.0.0.1:{lci.ProcessPort}/swagger/v2/swagger.json", HttpCompletionOption.ResponseContentRead);

            //TODO: Create swagger api generation from responses
            if (response.StatusCode == HttpStatusCode.OK)
            {
                string swaggerData = await response.Content.ReadAsStringAsync();
                Console.WriteLine(swaggerData);

                response = await ComClient.GetAsync($"{lci.ComProtocol}://127.0.0.1:{lci.ProcessPort}/Help?format=Full", HttpCompletionOption.ResponseContentRead);
                if(response.StatusCode == HttpStatusCode.OK)
                {
                    string helpData = await response.Content.ReadAsStringAsync();
                    Console.WriteLine(helpData);
                }
            }
            else if(response.StatusCode == HttpStatusCode.NotFound)
            {
                Console.WriteLine("Swagger not enabled. Please close all instances of the LeagueClient and RiotClient. Then launch this program again");
            }

            Console.WriteLine("Press enter to exit...");
            Console.ReadLine();
        }

        private static bool HasClientPath()
        {
            if (!File.Exists(ConfigFile))
            {
                using (StreamWriter sw = File.CreateText(ConfigFile))
                {
                    sw.WriteLine(@"installPath=C:\Riot Games\League of Legends");
                }
            }

            foreach (string line in File.ReadAllLines(ConfigFile))
            {
                string[] op = line.Split("=");
                switch (op[0])
                {
                    case "installPath":
                    {
                        LeagueClientPath = op[1];
                        break;
                    }
                }
            }

            Console.WriteLine($"Checking {LeagueClientPath} for league install...");
            return Directory.Exists(LeagueClientPath);
        }

        private static async Task<LeagueClientInfo> GetLeagueClientInfo()
        {
            LeagueClientInfo? lci = null;

            try
            {
                lci = LeagueClientInfo.FromFile(new FileInfo(Path.Combine(LeagueClientPath, "lockfile")));
                ClientProcess = Process.GetProcessById(lci.ProcessId);
            }
            catch (Exception ex) when (ex is ArgumentException || ex is FileNotFoundException)
            {
                ClientProcess = null;
            }
            catch (Exception ex)
            {
                throw ex;
            }

            if (ClientProcess is null)
            {
                string localYaml = Path.Combine(Environment.CurrentDirectory, "system_swagger.yaml");
                if (!File.Exists(localYaml))
                {
                    using (FileStream sys = File.Open(Path.Combine(LeagueClientPath, "system.yaml"), FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
                    using (TextReader trSys = new StreamReader(sys))
                    using (FileStream sysCopy = File.Create(localYaml))
                    using (TextWriter tw = new StreamWriter(sysCopy))
                    {
                        long length = sys.Length;
                        while (sys.Position < length)
                        {
                            string line = trSys.ReadLine() ?? string.Empty;

                            if (line.StartsWith("enable_swagger"))
                            {
                                continue;
                            }

                            if (line.StartsWith("riotclient"))
                            {
                                while ((trSys.ReadLine() ?? string.Empty).StartsWith("\t"))
                                {
                                    continue;
                                }
                                continue;
                            }

                            tw.WriteLine(line);
                        }
                        tw.WriteLine("enable_swagger: true");
                    }
                }

                ClientProcess = Process.Start(Path.Combine(LeagueClientPath, "LeagueClient.exe"),
                                              $"--disable-patching --headless --disable-self-update --system-yaml-override=\"{localYaml}\"");
                SelfLaunched = true;
                await Task.Delay(500);
                lci = LeagueClientInfo.FromFile(new FileInfo(Path.Combine(LeagueClientPath, "lockfile")));
            }

            if (lci is null)
            {
                Cleanup();
            }

            return lci;
        }

        private static void OnProcessExit(object? sender, EventArgs? e)
        {
            Cleanup(true);
        }

        private static void Cleanup(bool onExit = false)
        {
            if (SelfLaunched)
            {
                ClientProcess?.Kill();
            }

            File.Move(RiotServicePath, Path.Combine(Path.GetDirectoryName(RiotServicePath), "RiotClientServices.exe"));

            if (!onExit)
            {
                Environment.Exit(-1);
            }
        }
    }
}
