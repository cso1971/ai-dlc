import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { interval, Subscription, switchMap, startWith } from 'rxjs';
import { MetricsService } from '../../services/metrics.service';

const POLL_INTERVAL_MS = 5000;
const HISTORY_MAX = 40;

@Component({
  selector: 'app-queue-stats',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './queue-stats.component.html',
  styleUrl: './queue-stats.component.scss'
})
export class QueueStatsComponent implements OnInit, OnDestroy {
  totalMessages: number | null = null;
  queues: { name: string; messages: number }[] = [];
  error: string | null = null;
  history: number[] = [];
  private sub?: Subscription;

  constructor(private metricsService: MetricsService) {}

  ngOnInit(): void {
    this.sub = interval(POLL_INTERVAL_MS)
      .pipe(
        startWith(0),
        switchMap(() => this.metricsService.getRabbitMQMetrics())
      )
      .subscribe({
        next: (res) => {
          this.error = null;
          this.totalMessages = res.totalMessages;
          this.queues = res.queues ?? [];
          this.history = [...this.history, res.totalMessages].slice(-HISTORY_MAX);
        },
        error: (err) => {
          this.error = err.error?.detail || err.message || 'Error';
          this.totalMessages = null;
        }
      });
  }

  ngOnDestroy(): void {
    this.sub?.unsubscribe();
  }

  get sparklinePoints(): string {
    if (this.history.length < 2) return '';
    const max = Math.max(...this.history, 1);
    const w = 80;
    const h = 24;
    const points = this.history.map((v, i) => {
      const x = (i / (this.history.length - 1)) * w;
      const y = h - (v / max) * h;
      return `${x},${y}`;
    });
    return points.join(' ');
  }
}
