# FuckLogs

A native Windows log viewer built with **.NET 8** and **Windows Forms**.

## Features

- Real-time log streaming and filtering
- Custom dark theme UI
- ULP log parsing engine
- Single-file portable build

## Requirements

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (or later)
- Windows

## Build

```bash
dotnet restore
dotnet build -c Release
```

## Publish (single-file portable executable)

```bash
dotnet publish FuckLogs/FuckLogs.csproj -c Release -r win-x64 --self-contained true
```

The output is written to `FuckLogs/bin/Release/net8.0-windows/win-x64/publish/`.