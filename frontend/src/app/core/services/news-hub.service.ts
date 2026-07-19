import { Injectable, signal } from '@angular/core';
import { HubConnection, HubConnectionBuilder, HubConnectionState } from '@microsoft/signalr';
import { Subject } from 'rxjs';

import { environment } from '../../../environments/environment.development';
import { NewsCreatedEvent } from '../models/news-item';

@Injectable({ providedIn: 'root' })
export class NewsHubService {
  private readonly eventSubject = new Subject<NewsCreatedEvent>();
  private connection: HubConnection | undefined;

  readonly newsCreated$ = this.eventSubject.asObservable();
  readonly connectionState = signal<HubConnectionState>(HubConnectionState.Disconnected);

  async connect(): Promise<void> {
    if (this.connection?.state === HubConnectionState.Connected) {
      return;
    }

    this.connection = new HubConnectionBuilder()
      .withUrl(`${environment.apiBaseUrl}/hubs/news`)
      .withAutomaticReconnect()
      .build();
    this.connection.on('news:new', (event: NewsCreatedEvent) => this.eventSubject.next(event));
    this.connection.onreconnecting(() => this.connectionState.set(HubConnectionState.Reconnecting));
    this.connection.onreconnected(() => this.connectionState.set(HubConnectionState.Connected));
    this.connection.onclose(() => this.connectionState.set(HubConnectionState.Disconnected));

    await this.connection.start();
    this.connectionState.set(HubConnectionState.Connected);
  }
}
