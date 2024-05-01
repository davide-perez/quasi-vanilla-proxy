using Serilog;
using DPE.QuasiVanillaProxy.Http;
using DPE.QuasiVanillaProxy.Tcp;
using DPE.QuasiVanillaProxy.Core;
using DPE.QuasiVanillaProxy.Auth;
using DPE.QuasiVanillaProxy.Security;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DPE.QuasiVanillaProxy.Service
{
    public class Program
    {
        static string CONFIG_PATH = "config\\config.json";
        static string BOOTSTRAP_LOG_PATH = "logs\\log.log";

        private static void Main(string[] args)
        {
            // Use executable folder as base when program is running as a Windows Service,
            // instead of %WinDir%\Sys32
            Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);

            InitBootstrapLogger();
            EnsureConfigFileExists();
            EnsureConfigFileEncrypted();

            try
            {
                IHostBuilder builder = CreateBuilder(args);
                IHost host = builder.Build();

                host.Run();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Exception during bootstrapping");
                throw;
            }
            finally
            {
                DisposeBootstrapLogger();
            }
        }


        private static IHostBuilder CreateBuilder(string[] args)
        {
            IHostBuilder builder = Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration(ConfigureConfiguration)
                .UseSerilog(ConfigureLogging)
                .ConfigureServices(ConfigureServices)
                .UseWindowsService();

            return builder;
        }


        private static void ConfigureConfiguration(HostBuilderContext hostingContext, IConfigurationBuilder config)
        {
            config.Sources.Clear();
            config.AddJsonFile(CONFIG_PATH, optional: false, reloadOnChange: true);
        }


        private static void ConfigureLogging(HostBuilderContext context, LoggerConfiguration loggerConfiguration)
        {
            loggerConfiguration.ReadFrom.Configuration(context.Configuration);
        }


        private static void ConfigureServices(HostBuilderContext hostingContext, IServiceCollection services)
        {
            services.AddHttpClient();

            var authType = hostingContext.Configuration.GetValue<string>("Proxy:CurrentAuthentication");
            if (!string.IsNullOrWhiteSpace(authType))
                authType = authType.ToLower();

            switch (authType)
            {
                case "basic":
                    services.AddSingleton<BasicAuthSettings>(sp =>
                    {
                        BasicAuthSettings basicAuthSettings = new BasicAuthSettings();
                        hostingContext.Configuration.GetSection(key: "Proxy:Authentication:Basic").Bind(basicAuthSettings);
                        return basicAuthSettings;
                    });
                    services.AddTransient<BasicAuthHandler>();
                    services.ConfigureHttpClientDefaults(builder =>
                    {
                        builder.AddHttpMessageHandler<BasicAuthHandler>();
                    });

                    break;
                case "oauth2_0":
                    services.AddSingleton<OAuth20ClientCredsFlowSettings>(sp =>
                    {
                        OAuth20ClientCredsFlowSettings oAuth20ClientCredentialsSettings = new OAuth20ClientCredsFlowSettings();
                        hostingContext.Configuration.GetSection(key: "Proxy:Authentication:OAuth2_0").Bind(oAuth20ClientCredentialsSettings);
                        return oAuth20ClientCredentialsSettings;
                    });
                    services.AddTransient<OAuth20ClientCredsFlowHandler>();
                    services.ConfigureHttpClientDefaults(builder =>
                    {
                        builder.AddHttpMessageHandler<OAuth20ClientCredsFlowHandler>();
                    });

                    break;
                case "":
                    break;
                default:
                    throw new NotSupportedException($"Auth method {authType} not supported");
            }


            var protocol = hostingContext.Configuration.GetValue<string>("Proxy:CurrentProtocol");
            if (!string.IsNullOrWhiteSpace(protocol))
                protocol = protocol.ToLower();

            switch (protocol)
            {
                case "tcp":
                    services.AddSingleton<TcpProxySettings>(sp =>
                    {
                        TcpProxySettings tcpSettings = new();
                        hostingContext.Configuration.GetSection(key: "Proxy:Protocol:Tcp").Bind(tcpSettings);
                        return tcpSettings;
                    });
                    services.AddSingleton<IProxy, TcpProxy>();

                    break;
                case "http":
                    services.AddSingleton<HttpProxySettings>(sp =>
                    {
                        HttpProxySettings httpSettings = new();
                        hostingContext.Configuration.GetSection(key: "Proxy:Protocol:Http").Bind(httpSettings);
                        return httpSettings;
                    });
                    services.AddSingleton<IProxy, HttpProxy>();

                    break;
                default:
                    throw new NotSupportedException($"Protocol {protocol} is not supported");

            }

            services.AddHostedService<ProxyService>();
        }


        private static void InitBootstrapLogger()
        {
            // To log errors during start-up. Will be replaced by the configured logger with the UseSerilog() call
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console().WriteTo
                .File(path: BOOTSTRAP_LOG_PATH, rollingInterval: RollingInterval.Day)
                .CreateBootstrapLogger();
            Log.Information("Bootstrapping hosting...");
        }


        private static void DisposeBootstrapLogger()
        {
            Log.CloseAndFlush();
        }


        private static void EnsureConfigFileExists()
        {
            Log.Information("Checking for config file");
            string filePath = Path.Combine(Directory.GetCurrentDirectory(), CONFIG_PATH);
            bool configExists = File.Exists(filePath);
            if (configExists)
            {
                Log.Information("Config file found");
                return;
            }
            Log.Information($"Config file not found at {filePath}. Creating it...");

            var config = new
            {
                Proxy = new
                {
                    CurrentProtocol = "Http",
                    CurrentAuthentication = "",
                    Protocol = new
                    {
                        Tcp = new
                        {
                            ProxyIPAddress = "127.0.0.1",
                            ProxyPort = "16000",
                            TargetUrl = "https://example.com/",
                            ContentTypeHeader = "text/plain"
                        },
                        Http = new
                        {
                            ProxyUrl = "http://localhost:16000",
                            TargetUrl = "https://example.com/"
                        }
                    },
                    Authentication = new
                    {
                        Basic = new
                        {
                            UserName = "",
                            Password = ""
                        },
                        OAuth2_0 = new
                        {
                            Authority = "",
                            ClientId = "",
                            ClientSecret = "",
                            Scope = ""
                        }
                    }
                },
                Serilog = new
                {
                    MinimumLevel = "Debug",
                    Enrich = new[] { "FromLogContext" },
                    WriteTo = new object[]
                    {
                        new
                        {
                            Name = "Console"
                        },
                        new
                        {
                            Name = "File",
                            Args = new
                            {
                                path = BOOTSTRAP_LOG_PATH,
                                rollingInterval = "Day"
                            }
                        }
                    }
                }
            };

            string jsonConfig = JsonConvert.SerializeObject(config, Formatting.Indented);
            File.WriteAllText(filePath, jsonConfig);
            Log.Information("Config file created");
        }


        private static void EnsureConfigFileEncrypted()
        {
            string filePath = Path.Combine(Directory.GetCurrentDirectory(), CONFIG_PATH);
            string jsonConfig = File.ReadAllText(filePath);
            if (string.IsNullOrWhiteSpace(jsonConfig))
            {
                return;
            }

            JObject config = JObject.Parse(jsonConfig);

            if (config == null)
            {
                return;
            }

            string clientSecret = config["Proxy"]["Authentication"]["OAuth2_0"]["ClientSecret"].Value<string>();
            string encClientSecret = "";
            string password = config["Proxy"]["Authentication"]["Basic"]["Password"].Value<string>();
            string encPassword = "";

            if (!string.IsNullOrEmpty(clientSecret) && !DataProtectionMgt.IsEncrypted(encClientSecret))
            {
                Log.Information("Encrypting client secret...");
                encClientSecret = DataProtectionMgt.EncryptSettingValue(clientSecret);
            }
            else
            {
                encClientSecret = clientSecret;
            }
            config["Proxy"]["Authentication"]["OAuth2_0"]["ClientSecret"] = encClientSecret;

            if (!string.IsNullOrEmpty(password) && !DataProtectionMgt.IsEncrypted(password))
            {
                Log.Information("Encrypting password...");
                encPassword = DataProtectionMgt.EncryptSettingValue(password);
            }
            else
            {
                encPassword = password;
            }
            config["Proxy"]["Authentication"]["Basic"]["Password"] = encPassword;

            File.WriteAllText(filePath, config.ToString());
        }
        
        private static void TestDecrypt()
        {
            string filePath = Directory.GetCurrentDirectory() + "\\config\\config.json";
            string jsonConfig = File.ReadAllText(filePath);
            if (string.IsNullOrWhiteSpace(jsonConfig))
            {
                return;
            }

            JObject config = JObject.Parse(jsonConfig);

            if (config == null)
            {
                return;
            }

            string encPassword = config["Proxy"]["Authentication"]["Basic"]["Password"].Value<string>();
            string password = DataProtectionMgt.DecryptSettingValue(encPassword);
            config["Proxy"]["Authentication"]["Basic"]["Password"] = password;
            File.WriteAllText(filePath, config.ToString());
        }
    }
}