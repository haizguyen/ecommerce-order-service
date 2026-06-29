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
