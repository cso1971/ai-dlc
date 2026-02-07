export interface PostalAddressDto {
  recipientName: string;
  addressLine1: string;
  addressLine2?: string;
  city: string;
  stateOrProvince?: string;
  postalCode: string;
  countryCode: string;
  phoneNumber?: string;
  notes?: string;
}

export interface CustomerSummary {
  id: string;
  companyName: string;
  displayName?: string;
  email: string;
  createdAt: string;
  isActive: boolean;
}

export interface Customer {
  id: string;
  companyName: string;
  displayName?: string;
  email: string;
  phone?: string;
  taxId?: string;
  vatNumber?: string;
  billingAddress?: PostalAddressDto;
  shippingAddress?: PostalAddressDto;
  preferredLanguage: string;
  preferredCurrency: string;
  notes?: string;
  createdAt: string;
  updatedAt?: string;
  cancelledAt?: string;
  cancellationReason?: string;
  isActive: boolean;
}

export interface CreateCustomerRequest {
  companyName: string;
  displayName?: string;
  email: string;
  phone?: string;
  taxId?: string;
  vatNumber?: string;
  billingAddress?: PostalAddressDto;
  shippingAddress?: PostalAddressDto;
  preferredLanguage: string;
  preferredCurrency: string;
  notes?: string;
}

export interface UpdateCustomerRequest {
  companyName?: string;
  displayName?: string;
  email?: string;
  phone?: string;
  taxId?: string;
  vatNumber?: string;
  billingAddress?: PostalAddressDto;
  shippingAddress?: PostalAddressDto;
  preferredLanguage?: string;
  preferredCurrency?: string;
  notes?: string;
}

export interface CancelCustomerRequest {
  cancellationReason: string;
}
