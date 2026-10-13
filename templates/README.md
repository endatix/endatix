# Endatix.Templates

`dotnet new` templates for the [Endatix Platform](https://endatix.com).

## Install

```bash
dotnet new install Endatix.Templates
```

From this repository (local development):

```bash
dotnet new install ./templates/endatix-api
```

## Create an app

```bash
dotnet new endatix -n My.Endatix.ApiHost
dotnet new endatix -n My.Endatix.ApiHost --database postgresql
dotnet new endatix -n My.Endatix.ApiHost --database postgresql --saasManagement
```

`appsettings.Development.json` ships with localhost connection strings, migrations and sample seed on, and initial user `admin@localhost` / `P@ssw0rd`. Match the database password, replace the JWT signing key outside your machine, and change the user password after the first login.

`--saasManagement` adds the commercial `Endatix.SaaS.Management` package. Leave it off unless you have that package and PostgreSQL.

The `--EndatixVersion` parameter defaults to the version this package was packed with (`scripts/release-prepare.sh` passes `-p:Version`). The `0.7.8` in `template.json` is only what a folder install (`dotnet new install ./templates/endatix-api`) uses until the next edit.

## Uninstall

```bash
dotnet new uninstall Endatix.Templates
# or, for a folder install:
dotnet new uninstall ./templates/endatix-api
```
