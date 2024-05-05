using DPE.QuasiVanillaProxy.Security;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace DPE.QuasiVanillaProxy.Service
{
    public class SetupUtils
    {
        public static void InitBootstrapLogger(string bootstrapLogPath)
        {
            string directoryPath = new FileInfo(bootstrapLogPath).Directory.FullName;
            if(!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            // To log errors during start-up. Will be replaced by the configured logger with the UseSerilog() call
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console().WriteTo
                .File(path: bootstrapLogPath, rollingInterval: RollingInterval.Day)
                .CreateBootstrapLogger();
            Log.Debug("Starting bootstrap logger...");
        }


        public static void DisposeBootstrapLogger()
        {
            Log.Debug("Disposing bootstrap logger...");
            Log.CloseAndFlush();
        }


        public static void EnsureConfigFileExists(string configFilePath, string bootstrapLogFilePath)
        {
            Log.Information("Checking for config file");
            string directoryPath = new FileInfo(configFilePath).Directory.FullName;
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }
            bool configExists = File.Exists(configFilePath);
            if (configExists)
            {
                Log.Information("Config file found");
                return;
            }
            Log.Information($"Config file not found at {configFilePath}. Creating it...");

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
                        Udp = new
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
                                path = bootstrapLogFilePath,
                                rollingInterval = "Day"
                            }
                        }
                    }
                }
            };

            string jsonConfig = JsonConvert.SerializeObject(config, Formatting.Indented);
            File.WriteAllText(configFilePath, jsonConfig);
            Log.Information("Config file created");
        }


        public static void EnsureConfigFileEncrypted(string configFilePath)
        {
            string jsonConfig = File.ReadAllText(configFilePath);
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

            File.WriteAllText(configFilePath, config.ToString());
        }
    }
}
