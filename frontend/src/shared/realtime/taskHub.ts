import { HubConnection, HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr'
import { session } from '@/features/auth/session'
import { env } from '@/shared/config/env'
import type { TaskRealtimeEvent } from '@/shared/api/types'

type Listener = (event: TaskRealtimeEvent) => void
type ReconnectListener = () => void

/**
 * One SignalR connection shared by the whole dashboard.
 * After a reconnect, subscriptions are replayed and "reconnected" listeners fire so screens re-read the timeline
 * from the API: the database is the source of truth and realtime events are only refresh hints.
 */
class TaskHubClient {
  private connection: HubConnection | null = null
  private starting: Promise<void> | null = null
  private readonly listeners = new Set<Listener>()
  private readonly reconnectListeners = new Set<ReconnectListener>()
  private readonly taskSubscriptions = new Map<string, number>()

  private build(): HubConnection {
    const connection = new HubConnectionBuilder()
      .withUrl(env.hubUrl, {
        // Read the latest token on every (re)connect, after refreshes.
        accessTokenFactory: async () => {
          if (!session.accessToken) await session.refresh()
          return session.accessToken ?? ''
        },
      })
      .withAutomaticReconnect([0, 2_000, 5_000, 10_000, 30_000])
      .configureLogging(import.meta.env.DEV ? LogLevel.Information : LogLevel.Warning)
      .build()

    connection.on('taskEvent', (event: TaskRealtimeEvent) => {
      this.listeners.forEach((listener) => listener(event))
    })

    connection.onreconnected(async () => {
      await this.resubscribe()
      this.reconnectListeners.forEach((listener) => listener())
    })

    return connection
  }

  private async ensureStarted(): Promise<HubConnection> {
    this.connection ??= this.build()
    if (this.connection.state === HubConnectionState.Disconnected) {
      this.starting ??= this.connection.start().finally(() => {
        this.starting = null
      })
    }
    if (this.starting) await this.starting
    return this.connection
  }

  private async resubscribe() {
    if (!this.connection) return
    for (const taskId of this.taskSubscriptions.keys()) {
      await this.connection.invoke('SubscribeTask', taskId)
    }
  }

  onEvent(listener: Listener): () => void {
    this.listeners.add(listener)
    return () => this.listeners.delete(listener)
  }

  async disconnect(): Promise<void> {
    const connection = this.connection
    this.connection = null
    this.starting = null
    this.taskSubscriptions.clear()
    await connection?.stop()
  }

  onReconnected(listener: ReconnectListener): () => void {
    this.reconnectListeners.add(listener)
    return () => this.reconnectListeners.delete(listener)
  }

  /** Subscribes to one task's events; resolves false when the server refuses (no permission). */
  async subscribeTask(taskId: string): Promise<boolean> {
    const connection = await this.ensureStarted()
    this.taskSubscriptions.set(taskId, (this.taskSubscriptions.get(taskId) ?? 0) + 1)
    return connection.invoke<boolean>('SubscribeTask', taskId)
  }

  async unsubscribeTask(taskId: string): Promise<void> {
    const count = (this.taskSubscriptions.get(taskId) ?? 1) - 1
    if (count > 0) {
      this.taskSubscriptions.set(taskId, count)
      return
    }
    this.taskSubscriptions.delete(taskId)
    if (this.connection?.state === HubConnectionState.Connected) {
      await this.connection.invoke('UnsubscribeTask', taskId)
    }
  }
}

export const taskHub = new TaskHubClient()
