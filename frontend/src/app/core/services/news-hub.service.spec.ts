import { TestBed } from '@angular/core/testing';
import { HubConnection, HubConnectionBuilder, HubConnectionState } from '@microsoft/signalr';

import { NewsHubService } from './news-hub.service';

describe('NewsHubService', () => {
  it('creates a service', () => {
    expect(TestBed.inject(NewsHubService)).toBeTruthy();
  });

  it('shares an in-flight connection and exposes the connecting state', async () => {
    const service = TestBed.inject(NewsHubService);
    let resolveStart: (() => void) | undefined;
    const connection = createConnection(
      new Promise<void>((resolve) => {
        resolveStart = resolve;
      }),
    );
    spyOn(HubConnectionBuilder.prototype, 'build').and.returnValue(connection);

    const firstConnect = service.connect();
    const secondConnect = service.connect();

    expect(service.connectionState()).toBe(HubConnectionState.Connecting);
    expect(HubConnectionBuilder.prototype.build).toHaveBeenCalledTimes(1);

    resolveStart?.();
    await Promise.all([firstConnect, secondConnect]);

    expect(service.connectionState()).toBe(HubConnectionState.Connected);
  });
});

function createConnection(startResult: Promise<void>): HubConnection {
  return {
    state: HubConnectionState.Disconnected,
    start: jasmine.createSpy().and.returnValue(startResult),
    on: jasmine.createSpy(),
    onreconnecting: jasmine.createSpy(),
    onreconnected: jasmine.createSpy(),
    onclose: jasmine.createSpy(),
  } as unknown as HubConnection;
}
