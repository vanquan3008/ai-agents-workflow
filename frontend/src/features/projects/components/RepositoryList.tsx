import { GitBranch, Settings2 } from 'lucide-react'
import { Link } from 'react-router'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import type { Repository } from '@/shared/api/types'
import { formatNumber } from '@/shared/lib/format'

export function RepositoryList({ repositories }: { repositories: Repository[] }) {
  return (
    <ul className="divide-y">
      {repositories.map((repository) => (
        <li key={repository.id} className="flex flex-col gap-3 px-4 py-3 sm:flex-row sm:items-center sm:justify-between">
          <div className="min-w-0 space-y-1">
            <div className="flex flex-wrap items-center gap-2">
              <span className="truncate font-mono text-sm font-medium">{repository.fullName}</span>
              {!repository.isActive && (
                <Badge variant="outline" className="rounded-md">
                  Đã vô hiệu hóa
                </Badge>
              )}
            </div>
            <p className="text-muted-foreground flex flex-wrap items-center gap-x-3 gap-y-1 text-xs">
              <span className="inline-flex items-center gap-1">
                <GitBranch className="size-3.5" /> {repository.defaultBranch}
              </span>
              <span>Chính sách v{repository.policy.version}</span>
              <span>{formatNumber(repository.policy.sensitivePaths.length)} vùng nhạy cảm</span>
              <span className="font-mono">{repository.policy.testCommand}</span>
            </p>
          </div>
          <Button asChild variant="outline" size="sm" className="shrink-0 self-start sm:self-auto">
            <Link to={`/repositories/${repository.id}/workflow`}>
              <Settings2 /> Quy trình v{repository.workflowVersion}
            </Link>
          </Button>
        </li>
      ))}
    </ul>
  )
}
