using MarketHub.Api.Contracts;
using MarketHub.Api.Models;
using MarketHub.Api.Services;
using static MarketHub.Api.Endpoints.EndpointSecurity;

namespace MarketHub.Api.Endpoints;

public static class MarketplaceEndpoints
{
    public static IEndpointRouteBuilder MapMarketplaceApi(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api");

        MapAuth(api);
        MapProducts(api);
        MapCart(api);
        MapCheckout(api);
        MapOrders(api);
        MapSeller(api);
        MapAdmin(api);

        return app;
    }

    private static void MapAuth(RouteGroupBuilder api)
    {
        var auth = api.MapGroup("/auth");

        auth.MapGet("/me", (HttpContext http, MarketplaceStore store) =>
        {
            var user = CurrentUser(http, store);
            return user is null ? Results.Ok(new { user = (object?)null }) : Results.Ok(new { user = UserDto.From(user) });
        });

        auth.MapPost("/register", (RegisterRequest request, HttpContext http, MarketplaceStore store) =>
        {
            var result = store.Register(request);
            if (!result.Success)
            {
                return Results.BadRequest(new { message = result.Message });
            }

            SignIn(http, store, result.User!);
            return Results.Created("/api/auth/me", new { user = UserDto.From(result.User!) });
        });

        auth.MapPost("/login", (LoginRequest request, HttpContext http, MarketplaceStore store) =>
        {
            var user = store.ValidateLogin(request.Identifier, request.Password);
            if (user is null)
            {
                return Results.Unauthorized();
            }

            SignIn(http, store, user);
            return Results.Ok(new { user = UserDto.From(user) });
        });

        auth.MapPost("/logout", (HttpContext http, MarketplaceStore store) =>
        {
            if (http.Request.Cookies.TryGetValue(SessionCookie.Name, out var sessionId))
            {
                store.RevokeSession(sessionId);
            }

            http.Response.Cookies.Delete(SessionCookie.Name);
            return Results.NoContent();
        });
    }

    private static void MapProducts(RouteGroupBuilder api)
    {
        var products = api.MapGroup("/products");

        products.MapGet("/", (MarketplaceStore store, string? category, string? search) =>
            Results.Ok(store.Products(category, search).Select(ProductDto.From)));

        products.MapGet("/{id:guid}", (Guid id, MarketplaceStore store) =>
        {
            var product = store.GetProduct(id);
            return product is null ? Results.NotFound() : Results.Ok(ProductDto.From(product));
        });

        products.MapPost("/", (ProductUpsertRequest request, HttpContext http, MarketplaceStore store) =>
        {
            var user = RequireRole(http, store, Role.Seller, Role.Admin);
            if (user is null) return Results.Forbid();

            var product = store.CreateProduct(user, request);
            return Results.Created($"/api/products/{product.Id}", ProductDto.From(product));
        });

        products.MapPatch("/{id:guid}", (Guid id, ProductUpsertRequest request, HttpContext http, MarketplaceStore store) =>
        {
            var user = RequireRole(http, store, Role.Seller, Role.Admin);
            if (user is null) return Results.Forbid();

            var product = store.UpdateProduct(id, user, request);
            return product is null ? Results.NotFound() : Results.Ok(ProductDto.From(product));
        });

        products.MapDelete("/{id:guid}", (Guid id, HttpContext http, MarketplaceStore store) =>
        {
            var user = RequireRole(http, store, Role.Seller, Role.Admin);
            if (user is null) return Results.Forbid();

            return store.DeleteProduct(id, user) ? Results.NoContent() : Results.NotFound();
        });
    }

    private static void MapCart(RouteGroupBuilder api)
    {
        var cart = api.MapGroup("/cart");

        cart.MapGet("/", (HttpContext http, MarketplaceStore store) =>
        {
            var user = RequireRole(http, store, Role.Buyer);
            return user is null ? Results.Forbid() : Results.Ok(store.GetCart(user.Id));
        });

        cart.MapPost("/items", (CartItemRequest request, HttpContext http, MarketplaceStore store) =>
        {
            var user = RequireRole(http, store, Role.Buyer);
            if (user is null) return Results.Forbid();

            var updatedCart = store.AddToCart(user.Id, request.ProductId, request.Quantity);
            return updatedCart is null ? Results.NotFound(new { message = "Product not found" }) : Results.Ok(updatedCart);
        });

        cart.MapPatch("/items/{productId:guid}", (Guid productId, CartItemRequest request, HttpContext http, MarketplaceStore store) =>
        {
            var user = RequireRole(http, store, Role.Buyer);
            if (user is null) return Results.Forbid();

            return Results.Ok(store.UpdateCartItem(user.Id, productId, request.Quantity));
        });

        cart.MapDelete("/items/{productId:guid}", (Guid productId, HttpContext http, MarketplaceStore store) =>
        {
            var user = RequireRole(http, store, Role.Buyer);
            if (user is null) return Results.Forbid();

            return Results.Ok(store.UpdateCartItem(user.Id, productId, 0));
        });
    }

    private static void MapCheckout(RouteGroupBuilder api)
    {
        api.MapPost("/checkout", (HttpContext http, MarketplaceStore store, IConfiguration config) =>
        {
            var user = RequireRole(http, store, Role.Buyer);
            if (user is null) return Results.Forbid();

            var order = store.Checkout(user.Id);
            if (order is null) return Results.BadRequest(new { message = "Cart is empty" });

            var hasStripe = !string.IsNullOrWhiteSpace(config["STRIPE_SECRET_KEY"]);
            return Results.Ok(new
            {
                orderId = order.Id,
                checkoutUrl = hasStripe ? "https://checkout.stripe.com/replace-with-session" : "/checkout/success",
                mode = hasStripe ? "stripe-ready" : "local-demo"
            });
        });
    }

    private static void MapOrders(RouteGroupBuilder api)
    {
        var orders = api.MapGroup("/orders");

        orders.MapGet("/mine", (HttpContext http, MarketplaceStore store) =>
        {
            var user = RequireRole(http, store, Role.Buyer);
            return user is null ? Results.Forbid() : Results.Ok(store.BuyerOrders(user.Id));
        });

        orders.MapGet("/seller", (HttpContext http, MarketplaceStore store) =>
        {
            var user = RequireRole(http, store, Role.Seller);
            return user is null ? Results.Forbid() : Results.Ok(store.SellerOrders(user.Id));
        });

        orders.MapGet("/{id:guid}", (Guid id, HttpContext http, MarketplaceStore store) =>
        {
            var user = CurrentUser(http, store);
            if (user is null) return Results.Unauthorized();

            var order = store.GetOrder(id, user);
            return order is null ? Results.NotFound() : Results.Ok(order);
        });
    }

    private static void MapSeller(RouteGroupBuilder api)
    {
        var seller = api.MapGroup("/seller");

        seller.MapGet("/dashboard", (HttpContext http, MarketplaceStore store) =>
        {
            var user = RequireRole(http, store, Role.Seller);
            return user is null ? Results.Forbid() : Results.Ok(store.SellerDashboard(user.Id));
        });

        seller.MapPost("/connect-account", (HttpContext http, MarketplaceStore store, IConfiguration config) =>
        {
            var user = RequireRole(http, store, Role.Seller);
            if (user is null) return Results.Forbid();

            return Results.Ok(new
            {
                onboardingUrl = string.IsNullOrWhiteSpace(config["STRIPE_SECRET_KEY"])
                    ? "/seller?stripe=demo"
                    : "https://connect.stripe.com/setup/replace-with-account-link"
            });
        });

        seller.MapPost("/subscription", (HttpContext http, MarketplaceStore store, IConfiguration config) =>
        {
            var user = RequireRole(http, store, Role.Seller);
            if (user is null) return Results.Forbid();

            return Results.Ok(new
            {
                checkoutUrl = string.IsNullOrWhiteSpace(config["STRIPE_SELLER_SUBSCRIPTION_PRICE_ID"])
                    ? "/seller?subscription=demo"
                    : "https://checkout.stripe.com/replace-with-subscription-session"
            });
        });
    }

    private static void MapAdmin(RouteGroupBuilder api)
    {
        api.MapGet("/admin/dashboard", (HttpContext http, MarketplaceStore store) =>
        {
            var user = RequireRole(http, store, Role.Admin);
            return user is null ? Results.Forbid() : Results.Ok(store.AdminDashboard());
        });
    }
}
