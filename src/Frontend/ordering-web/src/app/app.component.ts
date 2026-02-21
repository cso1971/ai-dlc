import { Component } from '@angular/core';
import { NgIf } from '@angular/common';
import { RouterOutlet, RouterLink } from '@angular/router';
import { KeycloakService } from 'keycloak-angular';
import { AiChatComponent } from './components/ai-chat/ai-chat.component';
import { QueueStatsComponent } from './components/queue-stats/queue-stats.component';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [NgIf, RouterOutlet, RouterLink, AiChatComponent, QueueStatsComponent],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss'
})
export class AppComponent {
  title = 'Ordering Portal';
  username = '';

  constructor(private keycloak: KeycloakService) {}

  ngOnInit(): void {
    if (this.keycloak.isLoggedIn()) {
      this.keycloak.loadUserProfile().then((profile) => {
        this.username = (profile as { username?: string }).username ?? '';
      }).catch(() => {});
    }
  }

  logout(): void {
    this.keycloak.logout(window.location.origin);
  }
}
