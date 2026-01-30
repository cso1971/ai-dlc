import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  Order,
  OrderSummary,
  CreateOrderRequest,
  ShipOrderRequest,
  DeliverOrderRequest,
  CancelOrderRequest,
  StartProcessingRequest,
  InvoiceOrderRequest
} from '../models/order.models';
import { environment } from '../../environments/environment';

@Injectable({
  providedIn: 'root'
})
export class OrderService {
  private apiUrl = `${environment.apiUrl}/api/orders`;

  constructor(private http: HttpClient) {}

  getOrders(): Observable<OrderSummary[]> {
    return this.http.get<OrderSummary[]>(this.apiUrl);
  }

  getOrder(id: string): Observable<Order> {
    return this.http.get<Order>(`${this.apiUrl}/${id}`);
  }

  createOrder(request: CreateOrderRequest): Observable<Order> {
    return this.http.post<Order>(this.apiUrl, request);
  }

  startProcessing(id: string, request?: StartProcessingRequest): Observable<Order> {
    return this.http.post<Order>(`${this.apiUrl}/${id}/start-processing`, request || {});
  }

  shipOrder(id: string, request: ShipOrderRequest): Observable<Order> {
    return this.http.post<Order>(`${this.apiUrl}/${id}/ship`, request);
  }

  deliverOrder(id: string, request?: DeliverOrderRequest): Observable<Order> {
    return this.http.post<Order>(`${this.apiUrl}/${id}/deliver`, request || {});
  }

  invoiceOrder(id: string, request?: InvoiceOrderRequest): Observable<Order> {
    return this.http.post<Order>(`${this.apiUrl}/${id}/invoice`, request || {});
  }

  cancelOrder(id: string, request: CancelOrderRequest): Observable<Order> {
    return this.http.post<Order>(`${this.apiUrl}/${id}/cancel`, request);
  }
}
