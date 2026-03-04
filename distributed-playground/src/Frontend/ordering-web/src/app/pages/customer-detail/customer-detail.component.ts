import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { CustomerService } from '../../services/customer.service';
import { Customer, UpdateCustomerRequest, PostalAddressDto } from '../../models/customer.models';

@Component({
  selector: 'app-customer-detail',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule],
  templateUrl: './customer-detail.component.html',
  styleUrl: './customer-detail.component.scss'
})
export class CustomerDetailComponent implements OnInit {
  customer: Customer | null = null;
  loading = false;
  error: string | null = null;
  actionLoading = false;

  editMode = false;
  showCancelModal = false;
  cancellationReason = '';

  editForm: UpdateCustomerRequest = {};
  editBilling: PostalAddressDto | null = null;
  editShipping: PostalAddressDto | null = null;
  includeBilling = false;
  includeShipping = false;

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private customerService: CustomerService
  ) {}

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.loadCustomer(id);
    }
  }

  loadCustomer(id: string): void {
    this.loading = true;
    this.error = null;
    this.customerService.getCustomer(id).subscribe({
      next: (customer) => {
        this.customer = customer;
        this.loading = false;
        this.initEditForm();
      },
      error: (err) => {
        this.error = 'Failed to load customer: ' + (err.error?.message || err.message || 'Unknown error');
        this.loading = false;
      }
    });
  }

  initEditForm(): void {
    if (!this.customer) return;
    this.editForm = {
      companyName: this.customer.companyName,
      displayName: this.customer.displayName ?? undefined,
      email: this.customer.email,
      phone: this.customer.phone ?? undefined,
      taxId: this.customer.taxId ?? undefined,
      vatNumber: this.customer.vatNumber ?? undefined,
      preferredLanguage: this.customer.preferredLanguage,
      preferredCurrency: this.customer.preferredCurrency,
      notes: this.customer.notes ?? undefined
    };
    this.includeBilling = !!this.customer.billingAddress;
    this.editBilling = this.customer.billingAddress
      ? { ...this.customer.billingAddress }
      : null;
    this.includeShipping = !!this.customer.shippingAddress;
    this.editShipping = this.customer.shippingAddress
      ? { ...this.customer.shippingAddress }
      : null;
    if (!this.editBilling) {
      this.editBilling = {
        recipientName: '', addressLine1: '', city: '', postalCode: '', countryCode: 'IT'
      };
    }
    if (!this.editShipping) {
      this.editShipping = {
        recipientName: '', addressLine1: '', city: '', postalCode: '', countryCode: 'IT'
      };
    }
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

  toggleEdit(): void {
    this.editMode = !this.editMode;
    if (this.editMode) {
      this.initEditForm();
    }
  }

  saveUpdate(): void {
    if (!this.customer || !this.customer.isActive) return;

    const request: UpdateCustomerRequest = {
      ...this.editForm,
      billingAddress: this.includeBilling && this.editBilling ? this.editBilling : undefined,
      shippingAddress: this.includeShipping && this.editShipping ? this.editShipping : undefined
    };

    this.actionLoading = true;
    this.customerService.updateCustomer(this.customer.id, request).subscribe({
      next: (updated) => {
        this.customer = updated;
        this.actionLoading = false;
        this.editMode = false;
      },
      error: (err) => {
        alert('Error: ' + (err.error?.message || err.message));
        this.actionLoading = false;
      }
    });
  }

  openCancelModal(): void {
    this.cancellationReason = '';
    this.showCancelModal = true;
  }

  cancelCustomer(): void {
    if (!this.customer || !this.cancellationReason.trim()) return;

    this.actionLoading = true;
    this.customerService.cancelCustomer(this.customer.id, {
      cancellationReason: this.cancellationReason.trim()
    }).subscribe({
      next: (updated) => {
        this.customer = updated;
        this.actionLoading = false;
        this.showCancelModal = false;
        this.cancellationReason = '';
      },
      error: (err) => {
        alert('Error: ' + (err.error?.message || err.message));
        this.actionLoading = false;
      }
    });
  }

  closeCancelModal(): void {
    this.showCancelModal = false;
    this.cancellationReason = '';
  }

  canEdit(): boolean {
    return !!this.customer?.isActive;
  }

  canCancel(): boolean {
    return !!this.customer?.isActive;
  }
}
