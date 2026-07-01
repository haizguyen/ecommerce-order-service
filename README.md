# Ecommerce Order Service

This is the Order Service for the ecommerce demo walking skeleton. It is a .NET 8 service that persists orders and publishes `OrderPlaced` events to Azure Service Bus.

## Prerequisites

- .NET 8 SDK
- SQL Server (or LocalDB)
- Azure Service Bus (emulator or cloud instance)

## Building

To build the service:

```bash
dotnet build OrderService.slnx
```

## Running Tests

To run the unit tests:

```bash
dotnet test OrderService.slnx
```

## Running Locally

To run the service locally:

```bash
dotnet run --project src/OrderService.csproj
```

## Configuration

### VNPay

Non-secret VNPay settings (payment URL, version, command, currency, locale, return/IPN
URLs) live in the `VNPay` section of `src/appsettings.json` and are bound to the
`VNPayOptions` type in DI.

Secrets must **not** be committed. `TmnCode` and `HashSecret` are left empty in
`appsettings.json` and supplied at runtime via environment variables or user-secrets,
following the same override convention used for other secrets in this service:

```bash
# Environment variables (double underscore maps to the config section separator)
export VNPay__TmnCode="your-merchant-code"
export VNPay__HashSecret="your-hash-secret"
```

```bash
# Or user-secrets during local development
dotnet user-secrets set "VNPay:TmnCode" "your-merchant-code" --project src/OrderService.csproj
dotnet user-secrets set "VNPay:HashSecret" "your-hash-secret" --project src/OrderService.csproj
```
