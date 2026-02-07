import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export interface QueueSummaryDto {
  name: string;
  messages: number;
}

export interface RabbitMQMetricsResponse {
  totalMessages: number;
  queues: QueueSummaryDto[];
}

@Injectable({
  providedIn: 'root'
})
export class MetricsService {
  private apiUrl = `${environment.apiUrl}/api/metrics`;

  constructor(private http: HttpClient) {}

  getRabbitMQMetrics(): Observable<RabbitMQMetricsResponse> {
    return this.http.get<RabbitMQMetricsResponse>(`${this.apiUrl}/rabbitmq`);
  }
}
