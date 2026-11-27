import { useEffect, useState } from 'react'
import { Search, X } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import type { Project, TaskStatus } from '@/shared/api/types'
import { TASK_STATUSES } from '@/shared/api/types'
import { statusLabel } from '@/shared/lib/labels'
import { OptionSelect } from '@/shared/ui/option-select'
import type { TaskListState } from '../use-task-filters'

const ALL = 'all'
const SEARCH_DEBOUNCE_MS = 300

interface TaskFiltersProps {
  value: TaskListState
  projects: Project[]
  hasFilters: boolean
  onChange: (changes: Partial<TaskListState>) => void
  onReset: () => void
}

export function TaskFilters({ value, projects, hasFilters, onChange, onReset }: TaskFiltersProps) {
  const [searchDraft, setSearchDraft] = useState(value.search)
  const [appliedSearch, setAppliedSearch] = useState(value.search)

  // Adopt external changes (reset, back/forward navigation) during render instead of in an effect.
  if (value.search !== appliedSearch) {
    setAppliedSearch(value.search)
    setSearchDraft(value.search)
  }

  useEffect(() => {
    if (searchDraft === value.search) return
    const timer = window.setTimeout(() => onChange({ search: searchDraft.trim() }), SEARCH_DEBOUNCE_MS)
    return () => window.clearTimeout(timer)
  }, [searchDraft, value.search, onChange])

  return (
    <div className="flex flex-col gap-2 sm:flex-row sm:flex-wrap sm:items-center">
      <div className="relative sm:w-72">
        <Search className="text-muted-foreground pointer-events-none absolute top-1/2 left-2.5 size-4 -translate-y-1/2" aria-hidden="true" />
        <Input
          type="search"
          value={searchDraft}
          onChange={(event) => setSearchDraft(event.target.value)}
          placeholder="Tìm theo tiêu đề…"
          aria-label="Tìm task"
          className="pl-8"
        />
      </div>
      <OptionSelect
        aria-label="Lọc theo project"
        className="sm:w-52"
        value={value.projectId || ALL}
        onValueChange={(projectId) => onChange({ projectId: projectId === ALL ? '' : projectId })}
        options={[{ value: ALL, label: 'Tất cả project' }, ...projects.map((project) => ({ value: project.id, label: project.name }))]}
      />
      <OptionSelect
        aria-label="Lọc theo trạng thái"
        className="sm:w-48"
        value={value.status || ALL}
        onValueChange={(status) => onChange({ status: status === ALL ? '' : (status as TaskStatus) })}
        options={[{ value: ALL, label: 'Mọi trạng thái' }, ...TASK_STATUSES.map((status) => ({ value: status, label: statusLabel(status) }))]}
      />
      {hasFilters && (
        <Button variant="ghost" size="sm" onClick={onReset} className="text-muted-foreground self-start sm:self-auto">
          <X /> Xóa bộ lọc
        </Button>
      )}
    </div>
  )
}
