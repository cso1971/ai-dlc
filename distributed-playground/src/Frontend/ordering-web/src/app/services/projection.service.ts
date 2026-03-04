import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export interface DimensionStats {
  count: number;
  subtotal: number;
  grandTotal: number;
}

export interface ProjectionStats {
  totalOrders: number;
  lastUpdated: string | null;
  status: Record<string, DimensionStats>;
  currency: Record<string, DimensionStats>;
  'customer-ref': Record<string, DimensionStats>;
  'shipping-method': Record<string, DimensionStats>;
  'created-month': Record<string, DimensionStats>;
  'created-year': Record<string, DimensionStats>;
  'delivered-month': Record<string, DimensionStats>;
  'delivered-year': Record<string, DimensionStats>;
  product: Record<string, DimensionStats>;
}

@Injectable({
  providedIn: 'root'
})
export class ProjectionService {
  private apiUrl = `${environment.apiUrl}/api/projections`;

  constructor(private http: HttpClient) {}

  getStats(): Observable<ProjectionStats> {
    return this.http.get<ProjectionStats>(`${this.apiUrl}/stats`);
  }

  getDimension(dimension: string): Observable<Record<string, DimensionStats>> {
    return this.http.get<Record<string, DimensionStats>>(`${this.apiUrl}/stats/${dimension}`);
  }

  flush(): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.apiUrl}/flush`, {});
  }
}
