export enum OrderStatus {
  Created = 0,
  InProgress = 1,
  Shipped = 2,
  Delivered = 3,
  Invoiced = 4,
  Cancelled = 99
}

export interface ShippingAddress {
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

export interface OrderLine {
  id: string;
  lineNumber: number;
  productCode: string;
  description: string;
  quantity: number;
  unitOfMeasure: string;
  unitPrice: number;
  discountPercent: number;
  taxPercent: number;
  lineTotal: number;
  taxAmount: number;
  lineTotalWithTax: number;
}

export interface Order {
  id: string;
  customerId: string;
  customerReference?: string;
  requestedDeliveryDate?: string;
  priority: number;
  currencyCode: string;
  paymentTerms?: string;
  shippingMethod?: string;
  shippingAddress?: ShippingAddress;
  notes?: string;
  status: OrderStatus;
  createdAt: string;
  updatedAt?: string;
  trackingNumber?: string;
  carrier?: string;
  estimatedDeliveryDate?: string;
  shippedAt?: string;
  deliveredAt?: string;
  receivedBy?: string;
  deliveryNotes?: string;
  invoiceId?: string;
  invoicedAt?: string;
  cancellationReason?: string;
  cancelledAt?: string;
  lines: OrderLine[];
  subtotal: number;
  totalTax: number;
  grandTotal: number;
}

export interface OrderSummary {
  id: string;
  customerId: string;
  customerReference?: string;
  status: OrderStatus;
  createdAt: string;
  grandTotal: number;
  lineCount: number;
}

// Request DTOs
export interface CreateOrderRequest {
  customerId: string;
  customerReference?: string;
  requestedDeliveryDate?: string;
  priority: number;
  currencyCode: string;
  paymentTerms?: string;
  shippingMethod?: string;
  shippingAddress?: ShippingAddress;
  notes?: string;
  lines: CreateOrderLineRequest[];
}

export interface CreateOrderLineRequest {
  productCode: string;
  description: string;
  quantity: number;
  unitOfMeasure: string;
  unitPrice: number;
  discountPercent: number;
  taxPercent: number;
}

export interface ShipOrderRequest {
  trackingNumber: string;
  carrier?: string;
  estimatedDeliveryDate?: string;
}

export interface DeliverOrderRequest {
  receivedBy?: string;
  deliveryNotes?: string;
}

export interface CancelOrderRequest {
  cancellationReason: string;
}

export interface StartProcessingRequest {
  notes?: string;
}

export interface InvoiceOrderRequest {
  invoiceId?: string;
}

// Helpers
export function getStatusLabel(status: OrderStatus): string {
  switch (status) {
    case OrderStatus.Created: return 'Created';
    case OrderStatus.InProgress: return 'In Progress';
    case OrderStatus.Shipped: return 'Shipped';
    case OrderStatus.Delivered: return 'Delivered';
    case OrderStatus.Invoiced: return 'Invoiced';
    case OrderStatus.Cancelled: return 'Cancelled';
    default: return 'Unknown';
  }
}

export function getStatusColor(status: OrderStatus): string {
  switch (status) {
    case OrderStatus.Created: return '#2196F3';
    case OrderStatus.InProgress: return '#FF9800';
    case OrderStatus.Shipped: return '#9C27B0';
    case OrderStatus.Delivered: return '#4CAF50';
    case OrderStatus.Invoiced: return '#607D8B';
    case OrderStatus.Cancelled: return '#F44336';
    default: return '#9E9E9E';
  }
}
