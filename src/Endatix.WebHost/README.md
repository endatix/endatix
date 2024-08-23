# Endatix.WebHost

This is a shell project, which has no custom code and is merely an empty Web host for the Endatix Platform. It's purpose is to allow developers to run the Endatix solution locally for feature development, testing or just fun. This is why it has references to all other projects within the solution.

## Instructions

Click `Ctr+F5` and this is the project like any ASP.NET web application. This will host the entire Endatix set of projects under [https://localhost:5001](https://localhost:5001).

Live long and prosper! :vulcan_salute:

> [!NOTE]
> For running and hosting the Endatix Platform via dotnet and NuGet packages, the **Endatix.Api.Host** is the recommended main package as it simplifies the installation and setup process.

```bash
dotnet add package Endatix.Api.Host
```

## More Information:

For detailed installation instructions, please visit [Endatix Installation Guide](https://docs.endatix.com/docs/getting-started/installation).
