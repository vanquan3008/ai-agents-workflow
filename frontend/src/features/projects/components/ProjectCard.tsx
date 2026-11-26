import { FolderGit2, GitBranch, Users } from 'lucide-react'
import { Link } from 'react-router'
import { Badge } from '@/components/ui/badge'
import type { Project } from '@/shared/api/types'
import { formatNumber } from '@/shared/lib/format'
import { roleLabel } from '@/shared/lib/labels'

export function ProjectCard({ project }: { project: Project }) {
  return (
    <Link
      to={`/projects/${project.id}`}
      className="bg-card hover:border-foreground/15 focus-visible:ring-ring/50 group flex flex-col gap-4 rounded-lg border p-4 shadow-xs transition-colors outline-none focus-visible:ring-3"
    >
      <div className="flex items-start justify-between gap-3">
        <div className="flex min-w-0 items-center gap-2.5">
          <span className="bg-primary/10 text-primary flex size-8 shrink-0 items-center justify-center rounded-md">
            <FolderGit2 className="size-4" />
          </span>
          <div className="min-w-0">
            <p className="truncate text-sm font-medium group-hover:underline group-hover:underline-offset-4">{project.name}</p>
            <p className="text-muted-foreground truncate font-mono text-xs">{project.slug}</p>
          </div>
        </div>
        <Badge variant="secondary" className="shrink-0 rounded-md">
          {roleLabel(project.myRole)}
        </Badge>
      </div>
      <div className="text-muted-foreground flex items-center gap-4 text-xs">
        <span className="inline-flex items-center gap-1.5">
          <GitBranch className="size-3.5" /> {formatNumber(project.repositoryCount)} repository
        </span>
        <span className="inline-flex items-center gap-1.5">
          <Users className="size-3.5" /> {formatNumber(project.memberCount)} thành viên
        </span>
      </div>
    </Link>
  )
}
