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
    let secondConnectCompleted = false;
    void secondConnect.then(() => {
      secondConnectCompleted = true;
    });

    await Promise.resolve();

    expect(service.connectionState()).toBe(HubConnectionState.Connecting);
    expect(HubConnectionBuilder.prototype.build).toHaveBeenCalledTimes(1);
    expect(secondConnectCompleted).toBeFalse();

    resolveStart?.();
    await Promise.all([firstConnect, secondConnect]);

    expect(service.connectionState()).toBe(HubConnectionState.Connected);
  });

  it('does not create another hub while automatic reconnect is in progress', async () => {
    const service = TestBed.inject(NewsHubService);
    const connection = createConnection(Promise.resolve());
    spyOn(HubConnectionBuilder.prototype, 'build').and.returnValue(connection);

    await service.connect();
    getReconnectCallback(connection)();
    await service.connect();

    expect(service.connectionState()).toBe(HubConnectionState.Reconnecting);
    expect(HubConnectionBuilder.prototype.build).toHaveBeenCalledTimes(1);
    expect(connection.start).toHaveBeenCalledTimes(1);
  });

  it('reflects automatic reconnect callbacks in its connection state', async () => {
    const service = TestBed.inject(NewsHubService);
    const connection = createConnection(Promise.resolve());
    spyOn(HubConnectionBuilder.prototype, 'build').and.returnValue(connection);

    await service.connect();
    getReconnectCallback(connection)();
    expect(service.connectionState()).toBe(HubConnectionState.Reconnecting);

    getReconnectedCallback(connection)();
    expect(service.connectionState()).toBe(HubConnectionState.Connected);

    getCloseCallback(connection)();
    expect(service.connectionState()).toBe(HubConnectionState.Disconnected);
  });

  it('returns to the disconnected state when the initial connection fails', async () => {
    const service = TestBed.inject(NewsHubService);
    const connection = createConnection(Promise.reject(new Error('Connection failed.')));
    spyOn(HubConnectionBuilder.prototype, 'build').and.returnValue(connection);

    await expectAsync(service.connect()).toBeRejectedWithError('Connection failed.');

    expect(service.connectionState()).toBe(HubConnectionState.Disconnected);
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

function getReconnectCallback(connection: HubConnection): (error?: Error) => void {
  return (connection.onreconnecting as jasmine.Spy).calls.mostRecent().args[0] as (error?: Error) => void;
}

function getReconnectedCallback(connection: HubConnection): (connectionId?: string) => void {
  return (connection.onreconnected as jasmine.Spy).calls.mostRecent().args[0] as (connectionId?: string) => void;
}

function getCloseCallback(connection: HubConnection): (error?: Error) => void {
  return (connection.onclose as jasmine.Spy).calls.mostRecent().args[0] as (error?: Error) => void;
}
