# quasi-vanilla-proxy

A simple, lightweight .NET proxy that aims to facilitate seamless API integration between applications. It supports listening on multiple protocols and can handle authentication on client's behalf on forwarding, without requiring any modification to the involved applications.

Supports running as a console app or as a background service.
 
## Use cases
Integration between business software systems can be challenging, especially when there are multiple vendors or legacy applications involved. APIs of the involved software systems may have strict requirements for what concerns authentication or data formatting, and often changes for these functionalities are either expensive or simply unfeasible.

The quasi-vanilla-proxy can be useful in such scenarios, enabling API integration without requiring additional modifications to involved software.
Some example of possible use-cases:

- **Providing authentication**: some APIs or web servers enforce a specific authentication method, which may not be supported by the client application. An example is OAuth2.0: although being an industry standard nowadays, many applications still do not support it and its implementation can be challenging or even unfeasible.
- **

## Features
This proxy can be useful, for example, for integrations between a legacy application and a web server or, more in general, between an application and a web server that enforces some kind of authentication requirements which the application cannot natively satisfy. A common example is OAuth2.0: although being an industry standard nowadays, it is still not supported by many legacy applications as its implementation can be challenging.

 - **Easy to configure**: can easily be configured via a single configuration file, that automatically encrypts sensible properties
 - **Multi-protocol**: currently supports TCP and HTTP clients
 - **Authentication**: can take care of various authentication methods on behalf of the client. Currently supports **Basic Auth** and **OAuth2.0 with Client Credentials Flow**.
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

The `config.json` file is created at the first start-up (or re-created if it does not exist in the executable folder). 

The `Proxy.CurrentProtocol` property defines if the proxy run as an HTTP proxy or as a TCP proxy. Allowed values are `Tcp` and `Http`. Depending on the value of such property, the properties in the `Proxy.Protocol.Tcp` section or in the `Proxy.Protocol.Http` section will be relevant.

The `Proxy.CurrentAuthentication` property defines if the proxy will handle authentication on the client's behalf, i.e. creating and adding an auth header on the data prior forwarding it, using the specified authentication method. Allowed values are `Basic` and `OAuth2.0`. Depending on the value of such property, the properties in the `Proxy.Authentucation.Basic` section or in the `Proxy.Authentication.OAuth2_0` section will be relevant.

Sensible properties such as `Proxy.Authentication.Basic.Password` and `Proxy.Authentication.OAuth2_0.ClientSecret` are automatically encrypted at the program start-up.
Before:

```
    "Authentication": {
      "Basic": {
        "UserName": "Test username",
        "Password": "Test password"
      },
      "OAuth2_0": {
        "Authority": "https://test.authority.com",
        "ClientId": "00000000-0000-0000-0000-000000000000",
        "ClientSecret": "00000000-0000-0000-0000-000000000000",
        "Scope": ".default"
      }
    }
```

After the program is run:

```
    "Authentication": {
      "Basic": {
        "UserName": "Test username",
        "Password": "CypherValue!AQAAANCMnd8BFdERjHoAwE/Cl+sBAAAAOPmkPT2FcECW3DwXMYJXwgAAAAACAAAAAAAQZ...="
      },
      "OAuth2_0": {
        "Authority": "https://test.authority.com",
        "ClientId": "00000000-0000-0000-0000-000000000000",
        "ClientSecret": "CypherValue!AQAAANCMnd8BFdERjHoAwE/Cl+sBAAAAOPmkPT2FcECW3DwXMYJXwgAAAAACAAAAAAAQZ...Q==",
        "Scope": ".default"
      }
    }
```

Any time those properties are manually changed in the configuration file, they will be re-encrypted at the program next run.

The `Serilog` section contains the Serilog basic configuration. It may be extended following [the related documentation](https://github.com/serilog/serilog-settings-configuration).
