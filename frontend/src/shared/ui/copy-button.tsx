import { useState } from 'react'
import { Check, Copy } from 'lucide-react'
import { Button } from '@/components/ui/button'

export function CopyButton({ value, label = 'Sao chép' }: { value: string; label?: string }) {
  const [copied, setCopied] = useState(false)

  async function copy() {
    try {
      await navigator.clipboard.writeText(value)
      setCopied(true)
      window.setTimeout(() => setCopied(false), 1500)
    } catch {
      setCopied(false)
    }
  }

  return (
    <Button type="button" variant="ghost" size="icon-xs" onClick={() => void copy()} aria-label={copied ? 'Đã sao chép' : label} title={label}>
      {copied ? <Check className="text-success" /> : <Copy />}
    </Button>
  )
}
