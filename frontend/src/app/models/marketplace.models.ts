export type Role = 'Admin' | 'Seller' | 'Buyer';

export interface User {
  id: string;
  username: string;
  email: string;
  role: Role;
}

export interface Product {
  id: string;
  sellerId: string;
  title: string;
  category: string;
  price: number;
  inventory: number;
  imageUrl: string;
  description: string;
  active: boolean;
}

export interface CartLine {
  productId: string;
  title: string;
  imageUrl: string;
  price: number;
  quantity: number;
  lineTotal: number;
}

export interface Cart {
  items: CartLine[];
  total: number;
}
