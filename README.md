# Quasi-Vanilla Proxy

A simple, lightweight multi-protocol web proxy written in C# that aims to facilitate API integration between applications, forwarding traffic to an HTTP/S URL and handling authentication and payload manipulation on behalf of the client application.
 
## Use cases
Integration between business software systems can be challenging, especially when there are multiple vendors or legacy applications involved. APIs of the involved software systems may have strict requirements for what concerns authentication or data formatting, and often changes for these functionalities are either expensive or simply unfeasible.

QV-proxy can be useful in such scenarios, helping to enable API integration by handling some of the functionalities without requiring additional modifications to the involved software systems.
Some example of possible use-cases:

- **Providing authentication**: some APIs or web servers enforce a specific authentication method, which may not be supported by the client application. An example is OAuth2.0: although being an industry standard nowadays, many applications still do not support it.
- **Manipulating data**: this involves transforming or reformatting data before forwarding it to its destination. For instance, an API may work only with JSON format, while the client only supports sending data with XML or plain text format. Or, another example, an API may require a specific date format, which is not supported by the client app.
- **Enabling HTTP**: there are legacy business applications still do not support HTTP (happens often than you believe!), but may only support transport-level protocols such as TCP. QV-proxy supports these protocols, receiving the data and then forwarding it to the target URL.

## Features
 - **Easy to configure**: can easily be configured via a single configuration file
 - **Secure**: sensible properties, such as credentials in the configuration file and the JWT access token, are automatically encrypted
 - **Multi-protocol**: currently supports TCP, UDP and HTTP clients
 - **Authentication**: can take care of various authentication methods on behalf of the client. Currently supports **Basic Auth** and **OAuth2.0 with Client Credentials Flow**.
 - **Extensibility**: aims to be easily customizable and extendible

## Installation
Currently, there is no installer for QV-proxy and it has to be built directly from source code.

1. Clone the QV-proxy repository from GitHub: `git clone https://github.com/davide-perez/quasi-vanilla-proxy.git`
2. Navigate to the cloned directory: `cd quasi-vanilla-proxy`
3. Build the project using the .NET SDK: `dotnet build`
4. Modify the `config.json` file as needed (see next section)
5. Run the built executable: `dotnet run --project DPE.QuasiVanillaProxy.Service`

This will run the program as a console app by default.
The program can also be deployed as a Windows Service starting from the executable: 

`sc.exe create <new_service_name> binPath= "<path_to_the_service_executable>"`

In both cases, the base directory will be set as the executable folder: the configuration file and the log files will be written and read from there.

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
        "TargetUrl": "https://example.com/",
        "ContentTypeHeader": "text/plain"
      },
      "Udp": {
        "ProxyIPAddress": "127.0.0.1",
        "ProxyPort": "16000",
        "TargetUrl": "https://example.com/"
        "ContentTypeHeader": "text/plain"
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

The `Proxy.CurrentProtocol` property defines if the proxy run as an HTTP proxy, as a TCP proxy or as an UDP proxy. Allowed values are `Tcp`, `Udp` and `Http`. Depending on the value of such property, the properties in the `Proxy.Protocol.Tcp` section, in the `Proxy.Protocol.Udp` section or in the `Proxy.Protocol.Http` section will be relevant.

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

## Limitations
QV-proxy began as a side-project to practice with the latest .NET functionalities and best practices, but turned out to be a valuable tool for tackling some of the common challenges posed by business software integrations. Various tailored, rebranded and closed-source versions of this software have been developed and deployed successfully in production environments.
However, if you are planning to use this software or forks of it for your own projects, please consider the following:

- **Performance considerations**: QV-proxy may not offer the same level of performance as more complex, specialized proxy solutions. Users should assess their performance requirements and consider whether QV-proxy meets their needs adequately.
- **Security considerations**: QV-proxy leverages [DPAPI](https://en.wikipedia.org/wiki/Data_Protection_API) to protect sensitive data. However, depending on your requirements, more secure and tailored solutions for data protection may be a better fit. Moreover, QV-proxy does not provide advanced security features such as rate limiting, access control lists, or content filtering.
- **Scalability considerations**: while QV-proxy is designed to be lightweight and easy to configure and deploy, it may not scale effectively to large deployments or high-traffic scenarios without additional optimization or infrastructure investments.


In short, QV-proxy aims to be lightweight, flexible, and easy to deploy. However it's worth noting that it might not be the ideal choice for every situation.

## Contributing
Thank you for considering contributing to the Quasi-Vanilla Proxy project! Here's how you can get involved:

- **Code Contributions**: help improve the proxy by fixing bugs or adding new features. Fork the repository, make your changes, and submit a pull request.
- **Documentation**: improve the project's documentation by clarifying existing information or adding examples. Documentation changes are also submitted via pull requests.
- **Bug Reporting**: if you encounter any bugs, please report them by opening an issue on GitHub. Provide detailed information about the issue and steps to reproduce it.
- **Feedback**: share your thoughts, ideas, and suggestions for improving the proxy. Your feedback is valuable and helps shape the future of the project.

### Guidelines:
- Fork the repository and create a new branch for your changes.
- Follow existing code style and formatting conventions.
- Test your changes thoroughly before submitting a pull request.
- Provide clear and descriptive commit messages.
- Be respectful and constructive in all interactions within the project.

Thank you for contributing to the QV-proxy project! Your efforts help make the proxy better for everyone. If you have any questions, feel free to reach out. Your support is appreciated!
