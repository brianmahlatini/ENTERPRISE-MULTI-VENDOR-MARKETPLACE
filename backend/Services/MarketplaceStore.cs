using System.Collections.Concurrent;
using System.Security.Cryptography;
using MarketHub.Api.Contracts;
using MarketHub.Api.Models;

namespace MarketHub.Api.Services;

public sealed class MarketplaceStore
{
    private readonly ConcurrentDictionary<Guid, User> _users = new();
    private readonly ConcurrentDictionary<Guid, Product> _products = new();
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, int>> _carts = new();
    private readonly ConcurrentDictionary<Guid, Order> _orders = new();
    private readonly ConcurrentDictionary<string, Guid> _sessions = new();

    public MarketplaceStore()
    {
        SeedProducts();
    }

    public AuthResult Register(RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Email) || request.Password.Length < 8)
        {
            return new(false, "Username, email, and an 8 character password are required.", null);
        }

        if (_users.Values.Any(u => u.Email.Equals(request.Email, StringComparison.OrdinalIgnoreCase) || u.Username.Equals(request.Username, StringComparison.OrdinalIgnoreCase)))
        {
            return new(false, "Username or email is already registered.", null);
        }

        var role = _users.IsEmpty ? Role.Admin : request.Role == Role.Admin ? Role.Buyer : request.Role;
        var user = new User(Guid.NewGuid(), request.Username.Trim(), request.Email.Trim().ToLowerInvariant(), HashPassword(request.Password), role, DateTimeOffset.UtcNow);
        _users[user.Id] = user;
        return new(true, "Registered", user);
    }

    public User? ValidateLogin(string identifier, string password)
    {
        var user = _users.Values.FirstOrDefault(u =>
            u.Email.Equals(identifier, StringComparison.OrdinalIgnoreCase) ||
            u.Username.Equals(identifier, StringComparison.OrdinalIgnoreCase));

        return user is not null && VerifyPassword(password, user.PasswordHash) ? user : null;
    }

    public string CreateSession(Guid userId)
    {
        var sessionId = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        _sessions[sessionId] = userId;
        return sessionId;
    }

    public User? GetSessionUser(string sessionId)
    {
        return _sessions.TryGetValue(sessionId, out var userId) && _users.TryGetValue(userId, out var user) ? user : null;
    }

    public void RevokeSession(string sessionId) => _sessions.TryRemove(sessionId, out _);

    public IEnumerable<Product> Products(string? category, string? search)
    {
        return _products.Values
            .Where(p => p.Active)
            .Where(p => string.IsNullOrWhiteSpace(category) || p.Category.Equals(category, StringComparison.OrdinalIgnoreCase))
            .Where(p => string.IsNullOrWhiteSpace(search) || p.Title.Contains(search, StringComparison.OrdinalIgnoreCase) || p.Description.Contains(search, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(p => p.CreatedAt);
    }

    public Product? GetProduct(Guid id) => _products.TryGetValue(id, out var product) && product.Active ? product : null;

    public Product CreateProduct(User seller, ProductUpsertRequest request)
    {
        var product = new Product(Guid.NewGuid(), seller.Id, request.Title, request.Category, request.Price, request.Inventory, request.ImageUrl, request.Description, true, DateTimeOffset.UtcNow);
        _products[product.Id] = product;
        return product;
    }

    public Product? UpdateProduct(Guid id, User seller, ProductUpsertRequest request)
    {
        if (!_products.TryGetValue(id, out var existing) || (seller.Role != Role.Admin && existing.SellerId != seller.Id))
        {
            return null;
        }

        var updated = existing with
        {
            Title = request.Title,
            Category = request.Category,
            Price = request.Price,
            Inventory = request.Inventory,
            ImageUrl = request.ImageUrl,
            Description = request.Description
        };
        _products[id] = updated;
        return updated;
    }

    public bool DeleteProduct(Guid id, User seller)
    {
        if (!_products.TryGetValue(id, out var product) || (seller.Role != Role.Admin && product.SellerId != seller.Id))
        {
            return false;
        }

        _products[id] = product with { Active = false };
        return true;
    }

    public CartDto? AddToCart(Guid userId, Guid productId, int quantity)
    {
        if (!_products.ContainsKey(productId)) return null;

        var cart = _carts.GetOrAdd(userId, _ => new ConcurrentDictionary<Guid, int>());
        cart.AddOrUpdate(productId, Math.Max(1, quantity), (_, current) => current + Math.Max(1, quantity));
        return GetCart(userId);
    }

    public CartDto UpdateCartItem(Guid userId, Guid productId, int quantity)
    {
        var cart = _carts.GetOrAdd(userId, _ => new ConcurrentDictionary<Guid, int>());
        if (quantity <= 0) cart.TryRemove(productId, out _);
        else cart[productId] = quantity;
        return GetCart(userId);
    }

    public CartDto GetCart(Guid userId)
    {
        var cart = _carts.GetOrAdd(userId, _ => new ConcurrentDictionary<Guid, int>());
        var items = cart
            .Select(pair => _products.TryGetValue(pair.Key, out var product)
                ? new CartLine(product.Id, product.Title, product.ImageUrl, product.Price, pair.Value, product.Price * pair.Value)
                : null)
            .Where(line => line is not null)
            .Cast<CartLine>()
            .ToList();

        return new CartDto(items, items.Sum(i => i.LineTotal));
    }

    public Order? Checkout(Guid buyerId)
    {
        var cart = GetCart(buyerId);
        if (cart.Items.Count == 0) return null;

        var items = cart.Items.Select(line =>
        {
            var product = _products[line.ProductId];
            return new OrderItem(product.Id, product.SellerId, product.Title, line.Quantity, line.Price);
        }).ToList();

        var order = new Order(Guid.NewGuid(), buyerId, items, items.Sum(i => i.UnitPrice * i.Quantity), "Paid", DateTimeOffset.UtcNow);
        _orders[order.Id] = order;
        _carts.TryRemove(buyerId, out _);
        return order;
    }

    public IEnumerable<Order> BuyerOrders(Guid buyerId) => _orders.Values.Where(o => o.BuyerId == buyerId).OrderByDescending(o => o.CreatedAt);
    public IEnumerable<Order> SellerOrders(Guid sellerId) => _orders.Values.Where(o => o.Items.Any(i => i.SellerId == sellerId)).OrderByDescending(o => o.CreatedAt);

    public Order? GetOrder(Guid id, User user)
    {
        if (!_orders.TryGetValue(id, out var order)) return null;
        return user.Role == Role.Admin || order.BuyerId == user.Id || order.Items.Any(i => i.SellerId == user.Id) ? order : null;
    }

    public object SellerDashboard(Guid sellerId)
    {
        var products = _products.Values.Where(p => p.SellerId == sellerId && p.Active).ToList();
        var orders = SellerOrders(sellerId).ToList();
        var sellerItems = orders.SelectMany(o => o.Items.Where(i => i.SellerId == sellerId)).ToList();
        return new
        {
            productCount = products.Count,
            orderCount = orders.Count,
            unitsSold = sellerItems.Sum(i => i.Quantity),
            revenue = sellerItems.Sum(i => i.Quantity * i.UnitPrice),
            products = products.Select(ProductDto.From),
            orders
        };
    }

    public object AdminDashboard()
    {
        var orders = _orders.Values.ToList();
        return new
        {
            users = _users.Count,
            sellers = _users.Values.Count(u => u.Role == Role.Seller),
            buyers = _users.Values.Count(u => u.Role == Role.Buyer),
            products = _products.Values.Count(p => p.Active),
            orders = orders.Count,
            revenue = orders.Sum(o => o.Total),
            recentOrders = orders.OrderByDescending(o => o.CreatedAt).Take(10)
        };
    }

    private void SeedProducts()
    {
        var sellerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        Product[] products =
        [
            Seed(sellerId, "Studio Noise-Canceling Headphones", "Electronics", 189.99m, 42, "https://images.unsplash.com/photo-1505740420928-5e560c06d30e?auto=format&fit=crop&w=900&q=80", "Wireless over-ear headphones with deep battery life and clean active noise cancellation."),
            Seed(sellerId, "Smart Fitness Watch", "Electronics", 229.00m, 31, "https://images.unsplash.com/photo-1523275335684-37898b6baf30?auto=format&fit=crop&w=900&q=80", "Health tracking, notifications, and workout metrics in a polished aluminum case."),
            Seed(sellerId, "Leather Court Sneakers", "Fashion", 118.50m, 28, "https://images.unsplash.com/photo-1549298916-b41d501d3772?auto=format&fit=crop&w=900&q=80", "Low-profile leather sneakers built for everyday wear."),
            Seed(sellerId, "Ceramic Table Lamp", "Home", 84.00m, 17, "https://images.unsplash.com/photo-1507473885765-e6ed057f782c?auto=format&fit=crop&w=900&q=80", "Warm ceramic lamp with linen shade for bedrooms and reading corners."),
            Seed(sellerId, "Daily Skin Care Set", "Beauty", 64.99m, 54, "https://images.unsplash.com/photo-1556228720-195a672e8a03?auto=format&fit=crop&w=900&q=80", "A balanced cleanser, serum, and moisturizer set for daily routines."),
            Seed(sellerId, "Premium Yoga Mat", "Sports", 49.99m, 63, "https://images.unsplash.com/photo-1592432678016-e910b452f9a2?auto=format&fit=crop&w=900&q=80", "Non-slip mat with dense cushioning for studio and home practice.")
        ];

        foreach (var product in products)
        {
            _products[product.Id] = product;
        }
    }

    private static Product Seed(Guid sellerId, string title, string category, decimal price, int inventory, string imageUrl, string description)
        => new(Guid.NewGuid(), sellerId, title, category, price, inventory, imageUrl, description, true, DateTimeOffset.UtcNow);

    private static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 100_000, HashAlgorithmName.SHA256, 32);
        return $"{Convert.ToHexString(salt)}.{Convert.ToHexString(hash)}";
    }

    private static bool VerifyPassword(string password, string passwordHash)
    {
        var parts = passwordHash.Split('.', 2);
        if (parts.Length != 2) return false;

        var salt = Convert.FromHexString(parts[0]);
        var expected = Convert.FromHexString(parts[1]);
        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, 100_000, HashAlgorithmName.SHA256, 32);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
