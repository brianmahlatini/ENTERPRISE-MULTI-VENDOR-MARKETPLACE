import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { environment } from '../../environments/environment';
import { Cart, Product, Role, User } from '../models/marketplace.models';

@Injectable({ providedIn: 'root' })
export class ApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiUrl;

  me() {
    return this.http.get<{ user: User | null }>(`${this.baseUrl}/auth/me`, { withCredentials: true });
  }

  register(payload: { username: string; email: string; password: string; role: Role }) {
    return this.http.post<{ user: User }>(`${this.baseUrl}/auth/register`, payload, { withCredentials: true });
  }

  login(payload: { identifier: string; password: string }) {
    return this.http.post<{ user: User }>(`${this.baseUrl}/auth/login`, payload, { withCredentials: true });
  }

  logout() {
    return this.http.post<void>(`${this.baseUrl}/auth/logout`, {}, { withCredentials: true });
  }

  products(category = '', search = '') {
    return this.http.get<Product[]>(`${this.baseUrl}/products`, {
      params: { category, search },
      withCredentials: true
    });
  }

  createProduct(payload: Omit<Product, 'id' | 'sellerId' | 'active'>) {
    return this.http.post<Product>(`${this.baseUrl}/products`, payload, { withCredentials: true });
  }

  cart() {
    return this.http.get<Cart>(`${this.baseUrl}/cart`, { withCredentials: true });
  }

  addToCart(productId: string, quantity = 1) {
    return this.http.post<Cart>(`${this.baseUrl}/cart/items`, { productId, quantity }, { withCredentials: true });
  }

  updateCart(productId: string, quantity: number) {
    return this.http.patch<Cart>(`${this.baseUrl}/cart/items/${productId}`, { productId, quantity }, { withCredentials: true });
  }

  checkout() {
    return this.http.post<{ orderId: string; checkoutUrl: string; mode: string }>(`${this.baseUrl}/checkout`, {}, { withCredentials: true });
  }

  sellerDashboard() {
    return this.http.get<any>(`${this.baseUrl}/seller/dashboard`, { withCredentials: true });
  }

  sellerConnect() {
    return this.http.post<{ onboardingUrl: string }>(`${this.baseUrl}/seller/connect-account`, {}, { withCredentials: true });
  }

  sellerSubscription() {
    return this.http.post<{ checkoutUrl: string }>(`${this.baseUrl}/seller/subscription`, {}, { withCredentials: true });
  }

  adminDashboard() {
    return this.http.get<any>(`${this.baseUrl}/admin/dashboard`, { withCredentials: true });
  }
}
