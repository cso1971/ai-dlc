import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { CustomerService } from '../../services/customer.service';
import { CreateCustomerRequest, PostalAddressDto } from '../../models/customer.models';

@Component({
  selector: 'app-customer-create',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './customer-create.component.html',
  styleUrl: './customer-create.component.scss'
})
export class CustomerCreateComponent {
  saving = false;
  error: string | null = null;
  returnToOrderNew = false;

  customer: CreateCustomerRequest = {
    companyName: '',
    displayName: '',
    email: '',
    phone: '',
    taxId: '',
    vatNumber: '',
    preferredLanguage: 'en',
    preferredCurrency: 'EUR',
    notes: ''
  };

  billingAddress: PostalAddressDto = {
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

  shippingAddress: PostalAddressDto = {
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

  includeBilling = false;
  includeShipping = false;

  constructor(
    private customerService: CustomerService,
    private router: Router,
    private route: ActivatedRoute
  ) {
    this.returnToOrderNew = this.route.snapshot.queryParamMap.get('returnTo') === 'orderNew';
  }

  submit(): void {
    if (!this.customer.companyName?.trim() || !this.customer.email?.trim()) {
      this.error = 'Company name and email are required';
      return;
    }

    this.saving = true;
    this.error = null;

    const request: CreateCustomerRequest = {
      ...this.customer,
      billingAddress: this.includeBilling ? { ...this.billingAddress } : undefined,
      shippingAddress: this.includeShipping ? { ...this.shippingAddress } : undefined
    };

    this.customerService.createCustomer(request).subscribe({
      next: (created) => {
        if (this.returnToOrderNew) {
          this.router.navigate(['/orders/new'], { queryParams: { customerId: created.id } });
        } else {
          this.router.navigate(['/customers', created.id]);
        }
      },
      error: (err) => {
        this.error = 'Failed to create customer: ' + (err.error?.message || err.message || 'Unknown error');
        this.saving = false;
      }
    });
  }
}
