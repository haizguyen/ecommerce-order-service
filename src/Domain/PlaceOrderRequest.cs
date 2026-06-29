namespace OrderService.Domain;

public record PlaceOrderRequest(string Sku, int Quantity);
