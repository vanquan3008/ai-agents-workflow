import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { envResult } from '@/shared/config/env'
import './index.css'

const container = document.getElementById('root')
if (!container) throw new Error('Missing #root element')
const root = createRoot(container)

if (!envResult.ok) {
  // Invalid configuration: explain why instead of rendering a blank page.
  root.render(
    <main className="mx-auto max-w-2xl p-8">
      <div className="border-destructive/40 bg-destructive/5 text-destructive rounded-xl border p-6" role="alert">
        <p className="font-semibold">Cấu hình dashboard không hợp lệ</p>
        <pre className="mt-3 text-sm whitespace-pre-wrap">{envResult.error}</pre>
        <p className="text-muted-foreground mt-3 text-sm">Kiểm tra biến môi trường DEVAGENT_* của container dashboard hoặc file .env của Vite.</p>
      </div>
    </main>,
  )
} else {
  const { App } = await import('./app/App')
  root.render(
    <StrictMode>
      <App />
    </StrictMode>,
  )
}
