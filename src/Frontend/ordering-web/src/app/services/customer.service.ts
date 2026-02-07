import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  Customer,
  CustomerSummary,
  CreateCustomerRequest,
  UpdateCustomerRequest,
  CancelCustomerRequest
} from '../models/customer.models';
import { environment } from '../../environments/environment';

@Injectable({
  providedIn: 'root'
})
export class CustomerService {
  private apiUrl = `${environment.customersApiUrl}/api/customers`;

  constructor(private http: HttpClient) {}

  getCustomers(): Observable<CustomerSummary[]> {
    return this.http.get<CustomerSummary[]>(this.apiUrl);
  }

  getCustomer(id: string): Observable<Customer> {
    return this.http.get<Customer>(`${this.apiUrl}/${id}`);
  }

  createCustomer(request: CreateCustomerRequest): Observable<Customer> {
    return this.http.post<Customer>(this.apiUrl, request);
  }

  updateCustomer(id: string, request: UpdateCustomerRequest): Observable<Customer> {
    return this.http.put<Customer>(`${this.apiUrl}/${id}`, request);
  }

  cancelCustomer(id: string, request: CancelCustomerRequest): Observable<Customer> {
    return this.http.post<Customer>(`${this.apiUrl}/${id}/cancel`, request);
  }
}
