namespace OrderService.Domain;

public record PlaceOrderRequest(string Sku, int Quantity, string Name, decimal UnitPrice, string Currency);
