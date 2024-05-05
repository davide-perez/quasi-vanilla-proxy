using DPE.QuasiVanillaProxy.Auth;
using DPE.QuasiVanillaProxy.Core;
using DPE.QuasiVanillaProxy.Http;
using DPE.QuasiVanillaProxy.Security;
using DPE.QuasiVanillaProxy.Tcp;
using DPE.QuasiVanillaProxy.Udp;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;
using System.Xml.Linq;

namespace DPE.QuasiVanillaProxy.Service
{
    public class Program
    {
        static string CONFIG_FILE_FILENAME = "config\\config.json";
        static string BOOTSTRAP_LOG_FILENAME = "logs\\log.log";

        private static void Main(string[] args)
        {
            // Use executable folder as base when program is running as a Windows Service,
            // instead of %WinDir%\Sys32
            Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);

            string configFilePath = Path.Combine(Directory.GetCurrentDirectory(), CONFIG_FILE_FILENAME);
            string bootstrapLogFilePath = Path.Combine(Directory.GetCurrentDirectory(), BOOTSTRAP_LOG_FILENAME);

            SetupUtils.InitBootstrapLogger(BOOTSTRAP_LOG_FILENAME);
            SetupUtils.EnsureConfigFileExists(configFilePath, bootstrapLogFilePath);
            SetupUtils.EnsureConfigFileEncrypted(configFilePath);

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
                SetupUtils.DisposeBootstrapLogger();
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
            config.AddJsonFile(CONFIG_FILE_FILENAME, optional: false, reloadOnChange: true);
        }


        private static void ConfigureLogging(HostBuilderContext context, LoggerConfiguration loggerConfiguration)
        {
            loggerConfiguration.ReadFrom.Configuration(context.Configuration);
        }


        private static void ConfigureServices(HostBuilderContext hostingContext, IServiceCollection services)
        {
            // we use IHttpClientFactory for flexibility, but have to set the PooledConnectionLifetime to avoid
            // DNS issues since we will use long-lived http clients
            // (https://learn.microsoft.com/en-us/dotnet/core/extensions/httpclient-factory#avoid-typed-clients-in-singleton-services)
            services.AddHttpClient("proxy")
                .ConfigurePrimaryHttpMessageHandler(() =>
                {
                    return new SocketsHttpHandler()
                    {
                        PooledConnectionLifetime = TimeSpan.FromMinutes(2)
                    };
                })
                .SetHandlerLifetime(Timeout.InfiniteTimeSpan); // Disable rotation, as it is handled by PooledConnectionLifetime
            
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
                case "udp":
                    services.AddSingleton<UdpProxySettings>(sp =>
                    {
                        UdpProxySettings udpSettings = new();
                        hostingContext.Configuration.GetSection(key: "Proxy:Protocol:Udp").Bind(udpSettings);
                        return udpSettings;
                    });
                    services.AddSingleton<IProxy, UdpProxy>();

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
    }
}