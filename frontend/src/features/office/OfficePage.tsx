import { useEffect, useState } from 'react'
import { Building2, MousePointerClick, Plus, Settings2 } from 'lucide-react'
import { Link, useNavigate, useParams } from 'react-router'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Sheet, SheetContent, SheetDescription, SheetHeader, SheetTitle } from '@/components/ui/sheet'
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip'
import { CreateTaskDialog } from '@/features/tasks/components/CreateTaskDialog'
import { TaskTimeline } from '@/features/tasks/components/TaskTimeline'
import { useTaskTimeline } from '@/features/tasks/api'
import { ApiError } from '@/shared/api/http'
import { formatDateTime } from '@/shared/lib/format'
import { useMediaQuery } from '@/shared/lib/use-media-query'
import { PageHeader } from '@/shared/ui/page-header'
import { Panel } from '@/shared/ui/panel'
import { EmptyBlock, ErrorBlock, LoadingBlock } from '@/shared/ui/states'
import { useDepartmentTask, usePublishedDepartment } from './api'
import { FloorPlan } from './components/floor/FloorPlan'
import { StationStateBadge } from './components/floor/RoomVisuals'
import { StationList } from './components/floor/StationList'
import { OfficeTaskList } from './components/task/OfficeTaskList'
import { SelectedTaskSummary, StationEvidence } from './components/task/TaskEvidence'
import { useOfficeRealtime } from './hooks/use-office-realtime'
import { useOfficeSelection } from './hooks/use-office-selection'
import { ACTION_LABELS, actionTarget, presetLabel, themeStyle } from './model/presentation'
import { STATION_DESCRIPTIONS, STATION_LABELS, stationStates, taskPosition, type StationState } from './model/stations'
import type { TaskDetail } from '@/shared/api/types'
import type { DepartmentRoom, PublishedDepartment, WorkflowStation } from './types'

const LEGEND: StationState[] = ['active', 'waiting', 'failed', 'done', 'passed']

export default function OfficePage() {
  const { projectId = '', departmentId = '' } = useParams()
  const department = usePublishedDepartment(projectId, departmentId)

  if (department.isPending) return <LoadingBlock rows={8} />
  if (department.isError) {
    return (
      <div className="space-y-4">
        <PageHeader backTo={{ to: `/projects/${projectId}/office`, label: 'Phòng kỹ thuật' }} title="Văn phòng không khả dụng" />
        <ErrorBlock error={department.error} onRetry={() => void department.refetch()} />
      </div>
    )
  }
  return <Office department={department.data} />
}

function Office({ department }: { department: PublishedDepartment }) {
  const { projectId, id: departmentId } = department
  const navigate = useNavigate()
  const isDesktop = useMediaQuery('(min-width: 1024px)')
  const [createOpen, setCreateOpen] = useState(false)
  const { taskId, roomId, selectTask, selectRoom, clearTask } = useOfficeSelection()
  useOfficeRealtime(projectId, departmentId, taskId)

  const task = useDepartmentTask(projectId, departmentId, taskId)
  const timeline = useTaskTimeline(task.data ? task.data.id : '')
  const accessRevoked = task.error instanceof ApiError && task.error.isForbidden

  // A task whose repository the current revision no longer follows disappears from this office.
  useEffect(() => {
    if (task.error instanceof ApiError && task.error.isNotFound) clearTask()
  }, [task.error, clearTask])

  // Protected detail is dropped as soon as access is revoked, even though the query cache still holds it.
  const current = accessRevoked ? undefined : task.data
  const states: Partial<Record<WorkflowStation, StationState>> = current ? stationStates(current.status, timeline.data) : {}
  const position = current ? taskPosition(current.status, timeline.data) : null
  const selectedRoom =
    department.rooms.find((room) => room.roomId === roomId) ??
    (position ? department.rooms.find((room) => room.stage === position.station) : undefined)

  const onSelectRoom = (room: DepartmentRoom) => {
    if (!room.stage && room.action) {
      void navigate(actionTarget(room.action, projectId, department.repositories[0]?.id))
      return
    }
    selectRoom(room.roomId)
  }

  const roomDetail = selectedRoom && <RoomDetail room={selectedRoom} task={current} states={states} currentStation={position?.station} />

  return (
    <article style={themeStyle(department.theme)}>
      <PageHeader
        backTo={{ to: `/projects/${projectId}/office`, label: 'Phòng kỹ thuật' }}
        eyebrow={
          <>
            <Badge variant="secondary" className="rounded-md">
              Phiên bản {department.version}
            </Badge>
            <span className="text-muted-foreground text-xs">xuất bản {formatDateTime(department.publishedAt)}</span>
          </>
        }
        title={department.name}
        description={department.repositories.map((repository) => repository.fullName).join(' · ') || 'Chưa theo dõi repository nào'}
        actions={
          <>
            <ViewToggle />
            {department.canManage && (
              <Button asChild variant="outline" size="sm">
                <Link to={`/projects/${projectId}/office/${departmentId}/settings`}>
                  <Settings2 /> Cấu hình
                </Link>
              </Button>
            )}
            <Button size="sm" onClick={() => setCreateOpen(true)}>
              <Plus /> Tạo task
            </Button>
          </>
        }
      />

      <div className="grid gap-4 lg:grid-cols-[15rem_minmax(0,1fr)_20rem] 2xl:grid-cols-[17rem_minmax(0,1fr)_22rem] lg:items-start">
        {/* Phones lead with the selected task and its next action. */}
        {current && position && (
          <Panel className="lg:hidden">
            <SelectedTaskSummary task={current} station={position.station} />
          </Panel>
        )}

        <OfficeTaskList
          projectId={projectId}
          departmentId={departmentId}
          selectedTaskId={taskId}
          onSelectTask={selectTask}
          className="order-last max-h-[28rem] lg:sticky lg:top-16 lg:order-none lg:max-h-[calc(100svh-6rem)]"
        />

        <Panel
          title="Sơ đồ phòng ban"
          description={current ? `Đang theo dõi: ${current.title}` : 'Chọn một task để theo dõi nó qua các phòng.'}
          contentClassName="space-y-3"
        >
          <FloorPlan
            rooms={department.rooms}
            theme={department.theme}
            label={department.name}
            states={states}
            selectedRoomId={selectedRoom?.roomId}
            markerStation={position?.station}
            onSelectRoom={onSelectRoom}
          />
          <ul className="flex flex-wrap gap-1.5" aria-label="Chú giải trạng thái">
            {LEGEND.map((state) => (
              <li key={state}>
                <StationStateBadge state={state} />
              </li>
            ))}
          </ul>
          <StationList className="lg:hidden" rooms={department.rooms} states={states} selectedRoomId={selectedRoom?.roomId} onSelectRoom={onSelectRoom} />
        </Panel>

        <aside className="hidden min-w-0 space-y-4 lg:sticky lg:top-16 lg:block">
          <Panel title="Chi tiết công việc">
            {!taskId && <EmptyBlock className="border-0 py-6" icon={<MousePointerClick />} title="Chọn một task" description="Chọn task ở danh sách bên trái để xem bằng chứng từng bước." />}
            {taskId && task.isPending && <LoadingBlock rows={4} />}
            {accessRevoked && <ErrorBlock error={task.error} />}
            {task.isError && !accessRevoked && !(task.error instanceof ApiError && task.error.isNotFound) && (
              <ErrorBlock error={task.error} onRetry={() => void task.refetch()} />
            )}
            {current && position && (
              <div className="space-y-4">
                <SelectedTaskSummary task={current} station={position.station} />
                {roomDetail}
              </div>
            )}
          </Panel>
        </aside>
      </div>

      {current && (
        <Panel title="Timeline của task đã chọn" className="mt-4">
          {timeline.isPending && <LoadingBlock rows={3} />}
          {timeline.isError && <ErrorBlock error={timeline.error} onRetry={() => void timeline.refetch()} />}
          {timeline.data && <TaskTimeline entries={timeline.data} />}
        </Panel>
      )}

      <Sheet open={!isDesktop && Boolean(roomId) && Boolean(selectedRoom)} onOpenChange={(open) => !open && selectRoom(null)}>
        <SheetContent side="bottom" className="max-h-[85svh] overflow-y-auto p-4">
          <SheetHeader className="p-0">
            <SheetTitle>{selectedRoom?.label}</SheetTitle>
            <SheetDescription className="sr-only">{selectedRoom?.stage ? STATION_LABELS[selectedRoom.stage] : 'Phòng thông tin'}</SheetDescription>
          </SheetHeader>
          {roomDetail}
        </SheetContent>
      </Sheet>

      <CreateTaskDialog open={createOpen} onOpenChange={setCreateOpen} defaultProjectId={projectId} />
    </article>
  )
}

interface RoomDetailProps {
  room: DepartmentRoom
  task: TaskDetail | undefined
  states: Partial<Record<WorkflowStation, StationState>>
  currentStation: WorkflowStation | undefined
}

function RoomDetail({ room, task, states, currentStation }: RoomDetailProps) {
  if (!room.stage) {
    return (
      <div className="space-y-1 border-t pt-3">
        <p className="text-sm font-medium">{room.label}</p>
        <p className="text-muted-foreground text-xs">{presetLabel(room.assetPreset)}</p>
        {room.action && <p className="text-muted-foreground text-xs">{ACTION_LABELS[room.action]}</p>}
      </div>
    )
  }
  if (!task) {
    return (
      <div className="space-y-1 border-t pt-3">
        <p className="text-sm font-medium">{STATION_LABELS[room.stage]}</p>
        <p className="text-muted-foreground text-xs leading-relaxed">{STATION_DESCRIPTIONS[room.stage]}</p>
      </div>
    )
  }
  const state = states[room.stage] ?? 'idle'
  return (
    <div className="border-t pt-3">
      <StationEvidence task={task} station={room.stage} state={state} isCurrent={room.stage === currentStation} />
    </div>
  )
}

function ViewToggle() {
  return (
    <fieldset className="bg-muted inline-flex rounded-md p-0.5">
      <legend className="sr-only">Chế độ xem</legend>
      <Button size="xs" variant="secondary" aria-pressed="true" className="bg-background shadow-xs">
        <Building2 /> 2D
      </Button>
      <Tooltip>
        <TooltipTrigger asChild>
          {/* aria-disabled keeps the button focusable so keyboard users can read why 3D is unavailable. */}
          <Button size="xs" variant="ghost" aria-disabled="true" aria-pressed="false" className="opacity-50" onClick={(event) => event.preventDefault()}>
            3D
          </Button>
        </TooltipTrigger>
        <TooltipContent>Văn phòng 3D sẽ có ở bản phát hành tiếp theo.</TooltipContent>
      </Tooltip>
    </fieldset>
  )
}
