import { Component } from '@angular/core';
import { RouterOutlet, RouterLink } from '@angular/router';
import { AiChatComponent } from './components/ai-chat/ai-chat.component';
import { QueueStatsComponent } from './components/queue-stats/queue-stats.component';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, RouterLink, AiChatComponent, QueueStatsComponent],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss'
})
export class AppComponent {
  title = 'Ordering Portal';
}
