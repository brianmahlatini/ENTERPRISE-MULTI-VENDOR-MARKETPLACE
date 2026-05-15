import { CommonModule } from '@angular/common';
import { Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Cart, Product, Role, User } from '../../models/marketplace.models';
import { ApiService } from '../../services/api.service';

@Component({
  selector: 'app-dashboard-page',
  imports: [CommonModule, FormsModule],
  templateUrl: './dashboard.page.html',
  styleUrl: './dashboard.page.scss'
})
export class DashboardPage implements OnInit {
  private readonly api = inject(ApiService);

  user: User | null = null;
  products: Product[] = [];
  cart: Cart = { items: [], total: 0 };
  seller: any = null;
  admin: any = null;
  message = '';
  mode: 'login' | 'register' = 'register';
  category = '';
  search = '';

  credentials = {
    username: '',
    email: '',
    identifier: '',
    password: '',
    role: 'Buyer' as Role
  };

  productForm = {
    title: '',
    category: 'Electronics',
    price: 49,
    inventory: 10,
    imageUrl: 'https://images.unsplash.com/photo-1523275335684-37898b6baf30?auto=format&fit=crop&w=900&q=80',
    description: ''
  };

  categories = ['Electronics', 'Fashion', 'Home', 'Beauty', 'Sports'];

  get isAdmin() {
    return this.user?.role === 'Admin';
  }

  get isSeller() {
    return this.user?.role === 'Seller';
  }

  get isBuyer() {
    return this.user?.role === 'Buyer';
  }

  ngOnInit() {
    this.refresh();
  }

  refresh() {
    this.loadProducts();
    this.api.me().subscribe({
      next: ({ user }) => {
        this.user = user;
        this.loadRoleData();
      },
      error: () => this.user = null
    });
  }

  loadProducts() {
    this.api.products(this.category, this.search).subscribe(products => this.products = products);
  }

  authenticate() {
    const request = this.mode === 'register'
      ? this.api.register({
          username: this.credentials.username,
          email: this.credentials.email,
          password: this.credentials.password,
          role: this.credentials.role
        })
      : this.api.login({
          identifier: this.credentials.identifier,
          password: this.credentials.password
        });

    request.subscribe({
      next: ({ user }) => {
        this.user = user;
        this.message = `Signed in as ${user.role}`;
        this.loadRoleData();
      },
      error: () => this.message = 'Authentication failed. Check your details and try again.'
    });
  }

  logout() {
    this.api.logout().subscribe(() => {
      this.user = null;
      this.cart = { items: [], total: 0 };
      this.seller = null;
      this.admin = null;
      this.message = 'Signed out';
    });
  }

  add(product: Product) {
    this.api.addToCart(product.id, 1).subscribe({
      next: cart => {
        this.cart = cart;
        this.message = `${product.title} added to cart`;
      },
      error: () => this.message = 'Sign in as a buyer to use the cart.'
    });
  }

  updateLine(line: { productId: string }, quantity: number) {
    this.api.updateCart(line.productId, quantity).subscribe(cart => this.cart = cart);
  }

  checkout() {
    this.api.checkout().subscribe({
      next: result => this.message = `Checkout created in ${result.mode} mode. Order ${result.orderId}`,
      error: () => this.message = 'Checkout needs a buyer account with cart items.'
    });
  }

  createProduct() {
    this.api.createProduct(this.productForm).subscribe({
      next: product => {
        this.message = `${product.title} published`;
        this.productForm.title = '';
        this.productForm.description = '';
        this.loadProducts();
        this.loadSeller();
      },
      error: () => this.message = 'Only sellers can create products.'
    });
  }

  connectSeller() {
    this.api.sellerConnect().subscribe(result => this.message = `Seller onboarding: ${result.onboardingUrl}`);
  }

  subscribeSeller() {
    this.api.sellerSubscription().subscribe(result => this.message = `Seller subscription: ${result.checkoutUrl}`);
  }

  private loadRoleData() {
    if (!this.user) return;
    if (this.isBuyer) {
      this.api.cart().subscribe({ next: cart => this.cart = cart, error: () => undefined });
    }
    if (this.isSeller) {
      this.loadSeller();
    }
    if (this.isAdmin) {
      this.api.adminDashboard().subscribe(dashboard => this.admin = dashboard);
    }
  }

  private loadSeller() {
    this.api.sellerDashboard().subscribe(dashboard => this.seller = dashboard);
  }
}
