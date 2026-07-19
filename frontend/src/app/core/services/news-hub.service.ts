import { Injectable, signal } from '@angular/core';
import { HubConnection, HubConnectionBuilder, HubConnectionState } from '@microsoft/signalr';
import { Subject } from 'rxjs';

import { environment } from '../../../environments/environment.development';
import { NewsCreatedEvent } from '../models/news-item';

@Injectable({ providedIn: 'root' })
export class NewsHubService {
  private readonly eventSubject = new Subject<NewsCreatedEvent>();
  private connection: HubConnection | undefined;
  private connectionPromise: Promise<void> | undefined;

  readonly newsCreated$ = this.eventSubject.asObservable();
  readonly connectionState = signal<HubConnectionState>(HubConnectionState.Disconnected);

  connect(): Promise<void> {
    const currentState = this.connectionState();
    if (
      currentState === HubConnectionState.Connected ||
      currentState === HubConnectionState.Connecting ||
      currentState === HubConnectionState.Reconnecting
    ) {
      return Promise.resolve();
    }

    if (this.connectionPromise) {
      return this.connectionPromise;
    }

    this.connectionState.set(HubConnectionState.Connecting);
    this.connection = new HubConnectionBuilder()
      .withUrl(`${environment.apiBaseUrl}/hubs/news`)
      .withAutomaticReconnect()
      .build();
    this.connection.on('news:new', (event: NewsCreatedEvent) => this.eventSubject.next(event));
    this.connection.onreconnecting(() => this.connectionState.set(HubConnectionState.Reconnecting));
    this.connection.onreconnected(() => this.connectionState.set(HubConnectionState.Connected));
    this.connection.onclose(() => this.connectionState.set(HubConnectionState.Disconnected));

    this.connectionPromise = this.connection.start()
      .then(() => this.connectionState.set(HubConnectionState.Connected))
      .catch((error: unknown) => {
        this.connectionState.set(HubConnectionState.Disconnected);
        throw error;
      })
      .finally(() => {
        this.connectionPromise = undefined;
      });

    return this.connectionPromise;
  }
}
