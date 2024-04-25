# quasi-vanilla-proxy

A simple, lightweight .NET proxy that forwards incoming traffic to a remote HTTP Url. It supports listening on multiple protocols, and it is able to handling authentication before forwarding the traffic to the target URL.

## Features
 - **Easy to configure**: can easily be configured via a single configuration file
 - **Multi-protocol**: currently supports listening on TCP and HTTP protocols
 - **Authentication**: can take care of various authentication methods on behalf of the client, when the client does not support an authentication method but must communicate with a server which requires it. Currently supports **Basic Auth** and **OAuth2.0 with Client Credentials Flow**.
 - **Extensibility**: aims to be easily customizable and extendible

## Configuration
Any setting of the proxy, such as protocol, authentication and logging setups, can be easily be configured using a config.json file. Such file has this structure: 

```
{
  "Proxy": {
    "CurrentProtocol": "Http",
    "CurrentAuthentication": "OAuth2_0",
    "Protocol": {
      "Tcp": {
        "ProxyIPAddress": "127.0.0.1",
        "ProxyPort": "16000",
        "TargetUrl": "https://example.com/"
      },
      "Http": {
        "ProxyUrl": "http://localhost:16000",
        "TargetUrl": "https://example.com/"
      }
    },
    "Authentication": {
      "Basic": {
        "UserName": "",
        "Password": ""
      },
      "OAuth2_0": {
        "Authority": "",
        "ClientId": "",
        "ClientSecret": "",
        "Scope": ""
      }
    }
  },
  "Serilog": {
    "MinimumLevel": "Debug",
    "Enrich": [
      "FromLogContext"
    ],
    "WriteTo": [
      {
        "Name": "Console"
      },
      {
        "Name": "File",
        "Args": {
          "path": "logs\\log.log",
          "rollingInterval": "Day"
        }
      }
    ]
  }
}
```

The *config.json* file is created at the first start-up (or re-created if it does not exist in the executable folder). 

The *CurrentProtocol* property defines if the proxy run as an HTTP proxy (listening on an HTTP address) or as a TCP proxy (listening on a port). Depending on the value of such property, the properties in the *Protocol.Tcp* block or in the *Protocol.Http* block will be relevant.
