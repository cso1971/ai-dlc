import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ProjectionService, ProjectionStats, DimensionStats } from '../../services/projection.service';

interface DimensionCard {
  key: string;
  label: string;
  icon: string;
  data: Record<string, DimensionStats>;
}

@Component({
  selector: 'app-projections-dashboard',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './projections-dashboard.component.html',
  styleUrl: './projections-dashboard.component.scss'
})
export class ProjectionsDashboardComponent implements OnInit, OnDestroy {
  stats: ProjectionStats | null = null;
  loading = false;
  error: string | null = null;
  autoRefreshInterval: ReturnType<typeof setInterval> | null = null;
  autoRefreshEnabled = true;
  lastRefresh: Date | null = null;

  dimensions: DimensionCard[] = [];

  private dimensionConfig: { key: string; label: string; icon: string }[] = [
    { key: 'status', label: 'Order Status', icon: 'tag' },
    { key: 'currency', label: 'Currency', icon: 'currency' },
    { key: 'customer-ref', label: 'Customer Reference', icon: 'people' },
    { key: 'shipping-method', label: 'Shipping Method', icon: 'truck' },
    { key: 'created-month', label: 'Created (Month)', icon: 'calendar' },
    { key: 'created-year', label: 'Created (Year)', icon: 'calendar' },
    { key: 'delivered-month', label: 'Delivered (Month)', icon: 'calendar-check' },
    { key: 'delivered-year', label: 'Delivered (Year)', icon: 'calendar-check' },
    { key: 'product', label: 'Product', icon: 'box' },
  ];

  constructor(private projectionService: ProjectionService) {}

  ngOnInit(): void {
    this.loadStats();
    this.startAutoRefresh();
  }

  ngOnDestroy(): void {
    this.stopAutoRefresh();
  }

  loadStats(): void {
    this.loading = !this.stats;
    this.error = null;
    this.projectionService.getStats().subscribe({
      next: (stats) => {
        this.stats = stats;
        this.loading = false;
        this.lastRefresh = new Date();
        this.buildDimensions();
      },
      error: (err) => {
        this.error = 'Failed to load projections: ' + (err?.error?.message || err?.message || 'Service unreachable');
        this.loading = false;
      }
    });
  }

  private buildDimensions(): void {
    if (!this.stats) return;
    this.dimensions = this.dimensionConfig
      .map(cfg => ({
        ...cfg,
        data: (this.stats as any)[cfg.key] || {}
      }))
      .filter(d => Object.keys(d.data).length > 0);
  }

  toggleAutoRefresh(): void {
    this.autoRefreshEnabled = !this.autoRefreshEnabled;
    if (this.autoRefreshEnabled) {
      this.startAutoRefresh();
    } else {
      this.stopAutoRefresh();
    }
  }

  private startAutoRefresh(): void {
    this.stopAutoRefresh();
    this.autoRefreshInterval = setInterval(() => this.loadStats(), 10000);
  }

  private stopAutoRefresh(): void {
    if (this.autoRefreshInterval) {
      clearInterval(this.autoRefreshInterval);
      this.autoRefreshInterval = null;
    }
  }

  sortedEntries(data: Record<string, DimensionStats>): { key: string; stats: DimensionStats }[] {
    return Object.entries(data)
      .map(([key, stats]) => ({ key, stats }))
      .sort((a, b) => b.stats.grandTotal - a.stats.grandTotal);
  }

  totalCount(data: Record<string, DimensionStats>): number {
    return Object.values(data).reduce((sum, s) => sum + s.count, 0);
  }

  totalGrand(data: Record<string, DimensionStats>): number {
    return Object.values(data).reduce((sum, s) => sum + s.grandTotal, 0);
  }

  formatCurrency(amount: number): string {
    return new Intl.NumberFormat('it-IT', { style: 'currency', currency: 'EUR' }).format(amount);
  }

  formatDate(date: string | null): string {
    if (!date) return '-';
    return new Date(date).toLocaleDateString('it-IT', {
      day: '2-digit', month: '2-digit', year: 'numeric',
      hour: '2-digit', minute: '2-digit', second: '2-digit'
    });
  }

  formatTime(date: Date | null): string {
    if (!date) return '-';
    return date.toLocaleTimeString('it-IT', { hour: '2-digit', minute: '2-digit', second: '2-digit' });
  }

  getIconForDimension(icon: string): string {
    const icons: Record<string, string> = {
      'tag': '🏷️',
      'currency': '💱',
      'people': '👥',
      'truck': '🚚',
      'calendar': '📅',
      'calendar-check': '✅',
      'box': '📦'
    };
    return icons[icon] || '📊';
  }

  getBarWidth(value: number, data: Record<string, DimensionStats>): number {
    const max = Math.max(...Object.values(data).map(s => s.grandTotal));
    return max > 0 ? (value / max) * 100 : 0;
  }
}
