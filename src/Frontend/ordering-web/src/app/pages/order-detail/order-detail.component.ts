import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { OrderService } from '../../services/order.service';
import { CustomerService } from '../../services/customer.service';
import { Order, OrderStatus, getStatusLabel, getStatusColor } from '../../models/order.models';

@Component({
  selector: 'app-order-detail',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule],
  templateUrl: './order-detail.component.html',
  styleUrl: './order-detail.component.scss'
})
export class OrderDetailComponent implements OnInit {
  order: Order | null = null;
  customerDisplayName: string | null = null;
  loading = false;
  error: string | null = null;
  actionLoading = false;

  // Modal state
  showShipModal = false;
  showCancelModal = false;
  showDeliverModal = false;

  // Form data
  trackingNumber = '';
  carrier = '';
  estimatedDeliveryDate = '';
  cancellationReason = '';
  receivedBy = '';
  deliveryNotes = '';

  OrderStatus = OrderStatus;

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private orderService: OrderService,
    private customerService: CustomerService
  ) {}

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.loadOrder(id);
    }
  }

  loadOrder(id: string): void {
    this.loading = true;
    this.error = null;
    this.orderService.getOrder(id).subscribe({
      next: (order) => {
        this.order = order;
        this.loading = false;
        this.customerDisplayName = null;
        if (order.customerId) {
          this.customerService.getCustomer(order.customerId).subscribe({
            next: (c) => { this.customerDisplayName = c.displayName || c.companyName; },
            error: () => { /* keep null: template will show customerId */ }
          });
        }
      },
      error: (err) => {
        this.error = 'Failed to load order: ' + (err.error?.message || err.message || 'Unknown error');
        this.loading = false;
      }
    });
  }

  getStatusLabel(status: OrderStatus): string {
    return getStatusLabel(status);
  }

  getStatusColor(status: OrderStatus): string {
    return getStatusColor(status);
  }

  formatDate(date: string | undefined): string {
    if (!date) return '-';
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

  // Workflow actions
  startProcessing(): void {
    if (!this.order) return;
    this.actionLoading = true;
    this.orderService.startProcessing(this.order.id).subscribe({
      next: (order) => {
        this.order = order;
        this.actionLoading = false;
      },
      error: (err) => {
        alert('Error: ' + (err.error?.message || err.message));
        this.actionLoading = false;
      }
    });
  }

  openShipModal(): void {
    this.showShipModal = true;
  }

  shipOrder(): void {
    if (!this.order || !this.trackingNumber) return;
    this.actionLoading = true;
    this.orderService.shipOrder(this.order.id, {
      trackingNumber: this.trackingNumber,
      carrier: this.carrier || undefined,
      estimatedDeliveryDate: this.estimatedDeliveryDate || undefined
    }).subscribe({
      next: (order) => {
        this.order = order;
        this.actionLoading = false;
        this.showShipModal = false;
        this.resetForms();
      },
      error: (err) => {
        alert('Error: ' + (err.error?.message || err.message));
        this.actionLoading = false;
      }
    });
  }

  openDeliverModal(): void {
    this.showDeliverModal = true;
  }

  deliverOrder(): void {
    if (!this.order) return;
    this.actionLoading = true;
    this.orderService.deliverOrder(this.order.id, {
      receivedBy: this.receivedBy || undefined,
      deliveryNotes: this.deliveryNotes || undefined
    }).subscribe({
      next: (order) => {
        this.order = order;
        this.actionLoading = false;
        this.showDeliverModal = false;
        this.resetForms();
      },
      error: (err) => {
        alert('Error: ' + (err.error?.message || err.message));
        this.actionLoading = false;
      }
    });
  }

  invoiceOrder(): void {
    if (!this.order) return;
    this.actionLoading = true;
    this.orderService.invoiceOrder(this.order.id).subscribe({
      next: (order) => {
        this.order = order;
        this.actionLoading = false;
      },
      error: (err) => {
        alert('Error: ' + (err.error?.message || err.message));
        this.actionLoading = false;
      }
    });
  }

  openCancelModal(): void {
    this.showCancelModal = true;
  }

  cancelOrder(): void {
    if (!this.order || !this.cancellationReason) return;
    this.actionLoading = true;
    this.orderService.cancelOrder(this.order.id, {
      cancellationReason: this.cancellationReason
    }).subscribe({
      next: (order) => {
        this.order = order;
        this.actionLoading = false;
        this.showCancelModal = false;
        this.resetForms();
      },
      error: (err) => {
        alert('Error: ' + (err.error?.message || err.message));
        this.actionLoading = false;
      }
    });
  }

  closeModals(): void {
    this.showShipModal = false;
    this.showCancelModal = false;
    this.showDeliverModal = false;
    this.resetForms();
  }

  resetForms(): void {
    this.trackingNumber = '';
    this.carrier = '';
    this.estimatedDeliveryDate = '';
    this.cancellationReason = '';
    this.receivedBy = '';
    this.deliveryNotes = '';
  }

  canStartProcessing(): boolean {
    return this.order?.status === OrderStatus.Created;
  }

  canShip(): boolean {
    return this.order?.status === OrderStatus.InProgress;
  }

  canDeliver(): boolean {
    return this.order?.status === OrderStatus.Shipped;
  }

  canInvoice(): boolean {
    return this.order?.status === OrderStatus.Delivered;
  }

  canCancel(): boolean {
    return this.order?.status !== OrderStatus.Invoiced && 
           this.order?.status !== OrderStatus.Cancelled;
  }
}
