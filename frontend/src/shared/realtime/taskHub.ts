import { HubConnection, HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr'
import { env } from '../config/env'
import type { TaskRealtimeEvent } from '../api/types'

type Listener = (event: TaskRealtimeEvent) => void
type ReconnectListener = () => void

/**
 * Một kết nối SignalR dùng chung cho toàn dashboard.
 * Sau khi kết nối lại, các đăng ký được gửi lại và listener "reconnected" được gọi để
 * màn hình đọc bù timeline từ API (nguồn sự thật là DB, sự kiện realtime chỉ là gợi ý cập nhật).
 */
class TaskHubClient {
  private connection: HubConnection | null = null
  private starting: Promise<void> | null = null
  private readonly listeners = new Set<Listener>()
  private readonly reconnectListeners = new Set<ReconnectListener>()
  private readonly taskSubscriptions = new Map<string, number>()

  private build(): HubConnection {
    const devUser = env.authMode === 'development' && env.devUserId ? `?dev_user=${env.devUserId}` : ''
    const connection = new HubConnectionBuilder()
      .withUrl(`${env.hubUrl}${devUser}`, {
        accessTokenFactory: () => sessionStorage.getItem('devagent.access_token') ?? '',
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

  onReconnected(listener: ReconnectListener): () => void {
    this.reconnectListeners.add(listener)
    return () => this.reconnectListeners.delete(listener)
  }

  /** Đăng ký nhận sự kiện của một task. Trả false nếu server từ chối (không có quyền). */
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
