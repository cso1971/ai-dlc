import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { OrderService } from '../../services/order.service';
import { CustomerService } from '../../services/customer.service';
import { OrderSummary, OrderStatus, getStatusLabel, getStatusColor } from '../../models/order.models';
import { CustomerSummary } from '../../models/customer.models';

@Component({
  selector: 'app-order-list',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './order-list.component.html',
  styleUrl: './order-list.component.scss'
})
export class OrderListComponent implements OnInit {
  orders: OrderSummary[] = [];
  customers: CustomerSummary[] = [];
  loading = false;
  error: string | null = null;

  constructor(
    private orderService: OrderService,
    private customerService: CustomerService
  ) {}

  ngOnInit(): void {
    this.loadCustomers();
    this.loadOrders();
  }

  loadCustomers(): void {
    this.customerService.getCustomers().subscribe({
      next: (list) => {
        this.customers = list.filter(c => c.isActive);
      },
      error: () => { this.customers = []; }
    });
  }

  loadOrders(): void {
    this.loading = true;
    this.error = null;
    this.orderService.getOrders().subscribe({
      next: (orders) => {
        this.orders = orders;
        this.loading = false;
      },
      error: (err) => {
        this.error = 'Failed to load orders: ' + (err.message || 'Unknown error');
        this.loading = false;
      }
    });
  }

  getCustomerDisplayName(customerId: string): string {
    const c = this.customers.find(x => x.id === customerId);
    return c ? (c.displayName || c.companyName) : customerId.substring(0, 8) + '…';
  }

  getStatusLabel(status: OrderStatus): string {
    return getStatusLabel(status);
  }

  getStatusColor(status: OrderStatus): string {
    return getStatusColor(status);
  }

  formatDate(date: string): string {
    return new Date(date).toLocaleDateString('it-IT', {
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit'
    });
  }

  formatCurrency(amount: number): string {
    return new Intl.NumberFormat('it-IT', {
      style: 'currency',
      currency: 'EUR'
    }).format(amount);
  }
}
