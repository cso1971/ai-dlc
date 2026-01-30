import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { OrderService } from '../../services/order.service';
import { CreateOrderRequest, CreateOrderLineRequest, ShippingAddress } from '../../models/order.models';

@Component({
  selector: 'app-order-create',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './order-create.component.html',
  styleUrl: './order-create.component.scss'
})
export class OrderCreateComponent {
  saving = false;
  error: string | null = null;

  order: CreateOrderRequest = {
    customerId: '',
    customerReference: '',
    priority: 3,
    currencyCode: 'EUR',
    paymentTerms: 'NET30',
    shippingMethod: 'STANDARD',
    notes: '',
    lines: []
  };

  shippingAddress: ShippingAddress = {
    recipientName: '',
    addressLine1: '',
    addressLine2: '',
    city: '',
    stateOrProvince: '',
    postalCode: '',
    countryCode: 'IT',
    phoneNumber: '',
    notes: ''
  };

  includeShipping = false;

  newLine: CreateOrderLineRequest = {
    productCode: '',
    description: '',
    quantity: 1,
    unitOfMeasure: 'PCS',
    unitPrice: 0,
    discountPercent: 0,
    taxPercent: 22
  };

  constructor(
    private orderService: OrderService,
    private router: Router
  ) {
    // Generate a random customer ID for demo
    this.order.customerId = this.generateGuid();
  }

  addLine(): void {
    if (!this.newLine.productCode || this.newLine.quantity <= 0) {
      return;
    }

    this.order.lines.push({ ...this.newLine });
    this.newLine = {
      productCode: '',
      description: '',
      quantity: 1,
      unitOfMeasure: 'PCS',
      unitPrice: 0,
      discountPercent: 0,
      taxPercent: 22
    };
  }

  removeLine(index: number): void {
    this.order.lines.splice(index, 1);
  }

  calculateLineTotal(line: CreateOrderLineRequest): number {
    return line.quantity * line.unitPrice * (1 - line.discountPercent / 100);
  }

  calculateLineTax(line: CreateOrderLineRequest): number {
    return this.calculateLineTotal(line) * (line.taxPercent / 100);
  }

  get subtotal(): number {
    return this.order.lines.reduce((sum, line) => sum + this.calculateLineTotal(line), 0);
  }

  get totalTax(): number {
    return this.order.lines.reduce((sum, line) => sum + this.calculateLineTax(line), 0);
  }

  get grandTotal(): number {
    return this.subtotal + this.totalTax;
  }

  formatCurrency(amount: number): string {
    return new Intl.NumberFormat('it-IT', {
      style: 'currency',
      currency: 'EUR'
    }).format(amount);
  }

  submit(): void {
    if (this.order.lines.length === 0) {
      this.error = 'Please add at least one line item';
      return;
    }

    this.saving = true;
    this.error = null;

    const request: CreateOrderRequest = {
      ...this.order,
      shippingAddress: this.includeShipping ? this.shippingAddress : undefined
    };

    this.orderService.createOrder(request).subscribe({
      next: (order) => {
        this.router.navigate(['/orders', order.id]);
      },
      error: (err) => {
        this.error = 'Failed to create order: ' + (err.error?.message || err.message || 'Unknown error');
        this.saving = false;
      }
    });
  }

  generateGuid(): string {
    return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function(c) {
      const r = Math.random() * 16 | 0;
      const v = c === 'x' ? r : (r & 0x3 | 0x8);
      return v.toString(16);
    });
  }
}
