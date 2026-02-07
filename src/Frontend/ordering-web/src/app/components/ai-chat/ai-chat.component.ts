import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AiService, ChatResponse, SemanticSearchResponse, SearchResult, ModelInfo } from '../../services/ai.service';

interface Message {
  role: 'user' | 'assistant' | 'system';
  content: string;
  timestamp: Date;
  duration?: string;
  searchResults?: SearchResult[];
}

type ChatMode = 'chat' | 'search' | 'analyze';
type ChatBackend = 'rag' | 'semantic-kernel';

@Component({
  selector: 'app-ai-chat',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './ai-chat.component.html',
  styleUrls: ['./ai-chat.component.scss']
})
export class AiChatComponent implements OnInit {
  isOpen = false;
  isLoading = false;
  messages: Message[] = [];
  userInput = '';
  mode: ChatMode = 'chat';
  /** Which backend to use for chat: RAG (AI.Processor) or Semantic Kernel (Orchestrator.Api). Search/Analyze always use RAG. */
  chatBackend: ChatBackend = 'rag';
  modelInfo: ModelInfo | null = null;
  isConnected = false;
  connectionError = '';

  constructor(private aiService: AiService) {}

  ngOnInit(): void {
    this.checkConnection();
  }

  togglePanel(): void {
    this.isOpen = !this.isOpen;
    if (this.isOpen && this.messages.length === 0) {
      this.addSystemMessage('Welcome! I\'m your AI assistant powered by Ollama. Ask me anything about orders or use search mode to find similar orders.');
    }
  }

  checkConnection(): void {
    this.aiService.getModelInfo().subscribe({
      next: (info) => {
        this.modelInfo = info;
        this.isConnected = true;
        this.connectionError = '';
      },
      error: (err) => {
        this.isConnected = false;
        this.connectionError = 'Cannot connect to AI service. Make sure AI.Processor is running.';
      }
    });
  }

  setMode(newMode: ChatMode): void {
    this.mode = newMode;
  }

  setChatBackend(backend: ChatBackend): void {
    this.chatBackend = backend;
    if (backend === 'semantic-kernel') {
      this.mode = 'chat'; // only chat is available for SK
    }
  }

  sendMessage(): void {
    if (!this.userInput.trim() || this.isLoading) return;

    const input = this.userInput.trim();
    this.userInput = '';

    // Add user message
    this.messages.push({
      role: 'user',
      content: input,
      timestamp: new Date()
    });

    this.isLoading = true;
    this.scrollToBottom();

    switch (this.mode) {
      case 'chat':
        this.sendChatMessage(input);
        break;
      case 'search':
        this.sendSearchQuery(input);
        break;
      case 'analyze':
        this.sendAnalyzeRequest(input);
        break;
    }
  }

  private sendChatMessage(prompt: string): void {
    const request = {
      prompt,
      systemPrompt: 'You are a helpful assistant for an order management system. Help users understand orders, provide insights, and answer questions.'
    };
    const obs = this.chatBackend === 'semantic-kernel'
      ? this.aiService.chatWithOrchestrator(request)
      : this.aiService.chat(request);
    obs.subscribe({
      next: (response) => {
        this.addAssistantMessage(response.response, response.duration);
      },
      error: (err) => {
        const backend = this.chatBackend === 'semantic-kernel' ? 'Orchestrator (Semantic Kernel)' : 'AI.Processor (RAG)';
        this.addErrorMessage(`Failed to get response from ${backend}: ` + (err.error?.message || err.message));
      }
    });
  }

  private sendSearchQuery(query: string): void {
    this.aiService.semanticSearch({ query, limit: 50 }).subscribe({
      next: (response) => {
        if (response.results.length === 0) {
          this.addAssistantMessage('No similar orders found.', response.duration);
        } else {
          this.messages.push({
            role: 'assistant',
            content: `Found ${response.results.length} similar orders:`,
            timestamp: new Date(),
            duration: response.duration,
            searchResults: response.results
          });
        }
        this.isLoading = false;
        this.scrollToBottom();
      },
      error: (err) => {
        this.addErrorMessage('Search failed: ' + (err.error?.message || err.message));
      }
    });
  }

  private sendAnalyzeRequest(text: string): void {
    this.aiService.analyze({ text, analysisType: 'business' }).subscribe({
      next: (response) => {
        this.addAssistantMessage(response.analysis, response.duration);
      },
      error: (err) => {
        this.addErrorMessage('Analysis failed: ' + (err.error?.message || err.message));
      }
    });
  }

  private addSystemMessage(content: string): void {
    this.messages.push({
      role: 'system',
      content,
      timestamp: new Date()
    });
  }

  private addAssistantMessage(content: string, duration?: string): void {
    this.messages.push({
      role: 'assistant',
      content,
      timestamp: new Date(),
      duration
    });
    this.isLoading = false;
    this.scrollToBottom();
  }

  private addErrorMessage(content: string): void {
    this.messages.push({
      role: 'assistant',
      content: `⚠️ ${content}`,
      timestamp: new Date()
    });
    this.isLoading = false;
    this.scrollToBottom();
  }

  clearChat(): void {
    this.messages = [];
    this.addSystemMessage('Chat cleared. How can I help you?');
  }

  private scrollToBottom(): void {
    setTimeout(() => {
      const container = document.querySelector('.chat-messages');
      if (container) {
        container.scrollTop = container.scrollHeight;
      }
    }, 100);
  }

  formatDuration(duration: string | undefined): string {
    if (!duration) return '';
    // Duration comes as "HH:MM:SS.fffffff" format
    const match = duration.match(/(\d+):(\d+):(\d+)\.(\d+)/);
    if (match) {
      const hours = parseInt(match[1]);
      const minutes = parseInt(match[2]);
      const seconds = parseInt(match[3]);
      const ms = parseInt(match[4].substring(0, 3));
      
      if (hours > 0) return `${hours}h ${minutes}m`;
      if (minutes > 0) return `${minutes}m ${seconds}s`;
      if (seconds > 0) return `${seconds}.${ms}s`;
      return `${ms}ms`;
    }
    return duration;
  }

  handleKeyPress(event: KeyboardEvent): void {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      this.sendMessage();
    }
  }
}
