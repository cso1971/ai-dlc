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
  },
  { 
    path: 'customers', 
    loadComponent: () => import('./pages/customer-list/customer-list.component').then(m => m.CustomerListComponent)
  },
  { 
    path: 'customers/new', 
    loadComponent: () => import('./pages/customer-create/customer-create.component').then(m => m.CustomerCreateComponent)
  },
  { 
    path: 'customers/:id', 
    loadComponent: () => import('./pages/customer-detail/customer-detail.component').then(m => m.CustomerDetailComponent)
  }
];
