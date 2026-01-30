import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', redirectTo: '/orders', pathMatch: 'full' },
  { 
    path: 'orders', 
    loadComponent: () => import('./pages/order-list/order-list.component').then(m => m.OrderListComponent)
  },
  { 
    path: 'orders/new', 
    loadComponent: () => import('./pages/order-create/order-create.component').then(m => m.OrderCreateComponent)
  },
  { 
    path: 'orders/:id', 
    loadComponent: () => import('./pages/order-detail/order-detail.component').then(m => m.OrderDetailComponent)
  }
];
