import { useCallback, useState } from 'react'
import { AlertTriangle, Archive, CircleCheck, DoorOpen, Plus, Save, Upload } from 'lucide-react'
import { Link, useBeforeUnload, useNavigate, useParams } from 'react-router'
import { toast } from 'sonner'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { Field, FieldGroup, FieldLabel, FieldLegend, FieldSet } from '@/components/ui/field'
import { Input } from '@/components/ui/input'
import { Spinner } from '@/components/ui/spinner'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { ApiError } from '@/shared/api/http'
import { ConfirmDialog } from '@/shared/ui/confirm-dialog'
import { OptionSelect } from '@/shared/ui/option-select'
import { PageHeader } from '@/shared/ui/page-header'
import { Panel } from '@/shared/ui/panel'
import { EmptyBlock, ErrorBlock, FormError, LoadingBlock } from '@/shared/ui/states'
import { useDepartmentCommands, useDepartmentDraft, useDepartmentOptions } from './api'
import { FloorPlan } from './components/floor/FloorPlan'
import { LayoutCanvas } from './components/editor/LayoutCanvas'
import { RevisionHistory } from './components/editor/RevisionHistory'
import { RoomInspector } from './components/editor/RoomInspector'
import { StationList } from './components/floor/StationList'
import { findFreeSpot, layoutIssues, nextSortOrder, type GridLimits } from './model/layout'
import { THEME_LABELS } from './model/presentation'
import { useDraftEditor } from './hooks/use-draft-editor'
import type { DepartmentDraft, DepartmentEditorOptions, DepartmentRoom, DepartmentTheme } from './types'

const NEW_ROOM_SIZE = { width: 4, height: 3 }

export default function OfficeSettingsPage() {
  const { projectId = '', departmentId = '' } = useParams()
  const draft = useDepartmentDraft(projectId, departmentId)
  const options = useDepartmentOptions(projectId)

  if (draft.isPending || options.isPending) return <LoadingBlock rows={8} />
  if (draft.isError || options.isError) {
    return (
      <div className="space-y-4">
        <PageHeader backTo={{ to: `/projects/${projectId}/office`, label: 'Phòng kỹ thuật' }} title="Không mở được trình chỉnh sửa" />
        <ErrorBlock error={draft.error ?? options.error} onRetry={() => void (draft.isError ? draft.refetch() : options.refetch())} />
      </div>
    )
  }

  return (
    <DepartmentEditor
      // Remount on a different department so local edits never leak between departments.
      key={departmentId}
      projectId={projectId}
      draft={draft.data}
      options={options.data}
      reload={async () => (await draft.refetch()).data}
    />
  )
}

interface DepartmentEditorProps {
  projectId: string
  draft: DepartmentDraft
  options: DepartmentEditorOptions
  reload: () => Promise<DepartmentDraft | undefined>
}

function DepartmentEditor({ projectId, draft, options, reload }: DepartmentEditorProps) {
  const navigate = useNavigate()
  const editor = useDraftEditor(draft)
  const commands = useDepartmentCommands(projectId, draft.id)
  const { config, dirty, editVersion } = editor
  const [selectedRoomId, setSelectedRoomId] = useState<string | null>(config.rooms[0]?.roomId ?? null)
  const [conflict, setConflict] = useState(false)
  const limits: GridLimits = { columns: options.columns, rows: options.rows, minRoomSize: options.minRoomSize, minRooms: options.minRooms, maxRooms: options.maxRooms }
  const selectedRoom = config.rooms.find((room) => room.roomId === selectedRoomId)
  const issues = layoutIssues(config, limits)
  // Server-only checks (such as repository ownership) apply to the saved draft, so show them only while in sync.
  const serverIssues = dirty ? [] : draft.publishIssues.filter((issue) => !issues.includes(issue.message)).map((issue) => issue.message)
  const allIssues = [...issues, ...serverIssues]
  const busy = commands.save.isPending || commands.publish.isPending || commands.restore.isPending || commands.archive.isPending
  const officeUrl = `/projects/${projectId}/office/${draft.id}`

  useBeforeUnload(
    useCallback(
      (event: BeforeUnloadEvent) => {
        if (dirty) event.preventDefault()
      },
      [dirty],
    ),
  )

  const onCommandError = (error: unknown) => {
    if (error instanceof ApiError && error.status === 409) setConflict(true)
  }

  const updateRoom = (room: DepartmentRoom) => editor.update((c) => ({ ...c, rooms: c.rooms.map((r) => (r.roomId === room.roomId ? room : r)) }))

  const removeRoom = (roomId: string) => {
    editor.update((c) => ({ ...c, rooms: c.rooms.filter((r) => r.roomId !== roomId) }))
    setSelectedRoomId(null)
  }

  const freeSpot = findFreeSpot(config.rooms, NEW_ROOM_SIZE.width, NEW_ROOM_SIZE.height, limits)
  const canAddRoom = config.rooms.length < limits.maxRooms && freeSpot !== null
  const addRoom = () => {
    if (!freeSpot) return
    const room: DepartmentRoom = {
      roomId: crypto.randomUUID(),
      label: 'Phòng mới',
      stage: null,
      ...freeSpot,
      ...NEW_ROOM_SIZE,
      sortOrder: nextSortOrder(config.rooms),
      assetPreset: 'lounge',
      action: null,
    }
    editor.update((c) => ({ ...c, rooms: [...c.rooms, room] }))
    setSelectedRoomId(room.roomId)
  }

  const save = () =>
    commands.save.mutateAsync({ editVersion, config }).then(
      (saved) => {
        setConflict(false)
        return saved
      },
      (error: unknown) => {
        onCommandError(error)
        throw error
      },
    )

  const onSave = () => void save().then(() => toast.success('Đã lưu bản nháp.'), () => undefined)

  const onPublish = async () => {
    try {
      const version = dirty ? (await save()).editVersion : editVersion
      const result = await commands.publish.mutateAsync(version)
      toast.success(`Đã xuất bản phiên bản ${result.publishedVersion}.`, {
        action: { label: 'Mở văn phòng', onClick: () => void navigate(officeUrl) },
      })
    } catch (error) {
      onCommandError(error)
    }
  }

  const onRestore = (version: number) =>
    commands.restore.mutate(
      { version, editVersion },
      {
        onSuccess: (restored) => {
          editor.replace(restored)
          setSelectedRoomId(restored.config.rooms[0]?.roomId ?? null)
          toast.success(`Đã khôi phục phiên bản ${version} vào bản nháp. Xuất bản để áp dụng.`)
        },
        onError: onCommandError,
      },
    )

  const onArchive = () =>
    commands.archive.mutate(editVersion, {
      onSuccess: () => {
        toast.success('Đã lưu trữ phòng ban.')
        void navigate(`/projects/${projectId}/office`)
      },
      onError: onCommandError,
    })

  const onReloadLatest = async () => {
    const latest = await reload()
    if (latest) editor.replace(latest)
    setConflict(false)
  }

  const commandError = [commands.save.error, commands.publish.error, commands.restore.error, commands.archive.error].find(
    (error) => error && !(error instanceof ApiError && error.status === 409),
  )

  return (
    <article>
      <PageHeader
        backTo={draft.publishedVersion ? { to: officeUrl, label: 'Về văn phòng' } : { to: `/projects/${projectId}/office`, label: 'Phòng kỹ thuật' }}
        eyebrow={
          <>
            <Badge variant={draft.publishedVersion ? 'secondary' : 'outline'} className="rounded-md">
              {draft.publishedVersion ? `Đang hiển thị v${draft.publishedVersion}` : 'Chưa xuất bản'}
            </Badge>
            <span className="text-muted-foreground text-xs" aria-live="polite">
              {dirty ? 'Có thay đổi chưa lưu' : draft.hasDraft ? 'Bản nháp đã lưu' : 'Giống phiên bản đang hiển thị'}
            </span>
          </>
        }
        title={`Cấu hình · ${config.name || 'Phòng ban'}`}
        description={<span className="font-mono">{draft.slug}</span>}
        actions={
          <>
            {draft.publishedVersion && (
              <Button asChild variant="ghost" size="sm">
                <Link to={officeUrl}>
                  <DoorOpen /> Xem văn phòng
                </Link>
              </Button>
            )}
            <Button variant="outline" size="sm" onClick={onSave} disabled={!dirty || busy}>
              {commands.save.isPending ? <Spinner /> : <Save />} Lưu bản nháp
            </Button>
            <Button size="sm" onClick={() => void onPublish()} disabled={busy || allIssues.length > 0 || (!dirty && !draft.hasDraft)}>
              {commands.publish.isPending ? <Spinner /> : <Upload />} Xuất bản
            </Button>
          </>
        }
      />

      {conflict && (
        <Alert className="border-warning/40 bg-warning/[0.06] mb-4">
          <AlertTriangle className="text-warning" />
          <AlertTitle>Phòng ban vừa được người khác cập nhật</AlertTitle>
          <AlertDescription className="flex flex-wrap items-center gap-3">
            <span>Thay đổi của bạn vẫn được giữ trên trang này. Tải bản mới nhất sẽ bỏ các thay đổi chưa lưu.</span>
            <Button size="xs" variant="outline" onClick={() => void onReloadLatest()}>
              Tải bản mới nhất
            </Button>
          </AlertDescription>
        </Alert>
      )}
      {commandError && (
        <div className="mb-4">
          <ValidationSummary error={commandError} />
        </div>
      )}

      <div className="grid gap-4 lg:grid-cols-[minmax(0,1fr)_22rem] lg:items-start">
        <div className="min-w-0 space-y-4">
          <Panel title="Sơ đồ" description={`${config.rooms.length}/${limits.maxRooms} phòng · lưới ${limits.columns}×${limits.rows}`}>
            <Tabs defaultValue="edit" className="gap-3">
              <TabsList>
                <TabsTrigger value="edit">Chỉnh sửa</TabsTrigger>
                <TabsTrigger value="preview">Xem trước 2D</TabsTrigger>
              </TabsList>
              <TabsContent value="edit">
                <LayoutCanvas
                  rooms={config.rooms}
                  theme={config.theme}
                  limits={limits}
                  selectedRoomId={selectedRoomId}
                  onSelect={setSelectedRoomId}
                  onChange={updateRoom}
                />
              </TabsContent>
              <TabsContent value="preview">
                <FloorPlan
                  rooms={config.rooms}
                  theme={config.theme}
                  label={config.name}
                  columns={limits.columns}
                  rows={limits.rows}
                  selectedRoomId={selectedRoomId}
                  onSelectRoom={(room) => setSelectedRoomId(room.roomId)}
                />
              </TabsContent>
            </Tabs>
          </Panel>

          <Panel
            title="Danh sách phòng"
            description="Thứ tự này là thứ tự bàn phím trên cả hai chế độ xem."
            actions={
              <Button size="xs" variant="outline" onClick={addRoom} disabled={!canAddRoom}>
                <Plus /> Thêm phòng
              </Button>
            }
            flush
          >
            <StationList className="rounded-none border-0" rooms={config.rooms} selectedRoomId={selectedRoomId} onSelectRoom={(room) => setSelectedRoomId(room.roomId)} />
          </Panel>
        </div>

        <aside className="min-w-0 space-y-4">
          <Panel title="Phòng đã chọn">
            {selectedRoom ? (
              <RoomInspector room={selectedRoom} rooms={config.rooms} options={options} limits={limits} onChange={updateRoom} onRemove={removeRoom} />
            ) : (
              <EmptyBlock className="border-0 py-4" title="Chưa chọn phòng" description="Chọn một phòng trên sơ đồ hoặc trong danh sách." />
            )}
          </Panel>

          <Panel title="Thông tin chung">
            <FieldGroup className="gap-4">
              <Field>
                <FieldLabel htmlFor="department-name">Tên phòng ban</FieldLabel>
                <Input
                  id="department-name"
                  value={config.name}
                  maxLength={options.nameMaxLength}
                  onChange={(event) => editor.update((c) => ({ ...c, name: event.target.value }))}
                />
              </Field>
              <Field>
                <FieldLabel htmlFor="department-theme">Giao diện</FieldLabel>
                <OptionSelect
                  id="department-theme"
                  value={config.theme}
                  onValueChange={(theme) => editor.update((c) => ({ ...c, theme: theme as DepartmentTheme }))}
                  options={options.themes.map((theme) => ({ value: theme, label: THEME_LABELS[theme] }))}
                />
              </Field>
              <FieldSet>
                <FieldLegend variant="label">Repository theo dõi</FieldLegend>
                {draft.availableRepositories.length === 0 && <p className="text-muted-foreground text-xs">Project chưa có repository.</p>}
                <ul className="space-y-2">
                  {draft.availableRepositories.map((repository) => (
                    <li key={repository.id} className="flex items-center gap-2">
                      <Checkbox
                        id={`dept-repo-${repository.id}`}
                        checked={config.repositoryIds.includes(repository.id)}
                        onCheckedChange={(checked) =>
                          editor.update((c) => ({
                            ...c,
                            repositoryIds: checked === true ? [...c.repositoryIds, repository.id] : c.repositoryIds.filter((id) => id !== repository.id),
                          }))
                        }
                      />
                      <label htmlFor={`dept-repo-${repository.id}`} className="min-w-0 truncate text-sm">
                        {repository.fullName}
                      </label>
                    </li>
                  ))}
                </ul>
              </FieldSet>
            </FieldGroup>
          </Panel>

          <Panel title="Kiểm tra trước khi xuất bản">
            {allIssues.length === 0 ? (
              <p className="text-success flex items-center gap-2 text-sm">
                <CircleCheck className="size-4" aria-hidden="true" /> Sẵn sàng xuất bản.
              </p>
            ) : (
              <ul className="space-y-1.5" aria-live="polite">
                {allIssues.map((issue) => (
                  <li key={issue} className="text-warning flex items-start gap-2 text-sm">
                    <AlertTriangle className="mt-0.5 size-3.5 shrink-0" aria-hidden="true" />
                    {issue}
                  </li>
                ))}
              </ul>
            )}
          </Panel>

          <Panel title="Lịch sử phiên bản">
            <RevisionHistory projectId={projectId} departmentId={draft.id} hasUnsavedChanges={dirty} restoring={busy} onRestore={onRestore} />
          </Panel>

          <Panel title="Lưu trữ">
            <p className="text-muted-foreground mb-3 text-xs">Phòng ban rời khỏi danh mục. Lịch sử phiên bản, task và lần chạy được giữ nguyên.</p>
            <ConfirmDialog
              trigger={
                <Button variant="outline" size="sm" className="text-danger" disabled={busy}>
                  <Archive /> Lưu trữ phòng ban
                </Button>
              }
              title="Lưu trữ phòng ban?"
              description="Thành viên sẽ không còn thấy văn phòng này. Slug được giữ lại và không thể dùng cho phòng ban khác."
              confirmLabel="Lưu trữ"
              destructive
              onConfirm={onArchive}
            />
          </Panel>
        </aside>
      </div>
    </article>
  )
}

/** Server validation lists every violation by field; show them all so each room can be fixed in one pass. */
function ValidationSummary({ error }: { error: unknown }) {
  const details = error instanceof ApiError ? Object.values(error.errors).flat() : []
  if (details.length === 0) return <FormError error={error} />
  return (
    <Alert variant="destructive" className="border-destructive/30 bg-destructive/5">
      <AlertTriangle />
      <AlertTitle>{error instanceof ApiError ? error.message : 'Cấu hình chưa hợp lệ.'}</AlertTitle>
      <AlertDescription>
        <ul className="list-disc space-y-0.5 pl-4">
          {[...new Set(details)].map((detail) => (
            <li key={detail}>{detail}</li>
          ))}
        </ul>
      </AlertDescription>
    </Alert>
  )
}
