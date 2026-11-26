import { useState, type FormEvent } from 'react'
import { useParams } from 'react-router'
import { useWorkflow, useWorkflowRevisions, useUpdateWorkflow } from './api'
import type { ApprovalRequirement, FindingSeverity, NotificationEvent, RepositoryWorkflow, WorkflowDefinition } from '@/shared/api/types'
import { formatDateTime } from '@/shared/lib/format'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Textarea } from '@/components/ui/textarea'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { ErrorBlock, FormError, LoadingBlock, PageHeader } from '@/shared/ui/page'

const splitList = (value: string) =>
  value
    .split(',')
    .map((item) => item.trim())
    .filter(Boolean)

function WorkflowForm({ workflow }: { workflow: RepositoryWorkflow }) {
  const update = useUpdateWorkflow(workflow.repositoryId)
  const [definition, setDefinition] = useState<WorkflowDefinition>(() => structuredClone(workflow.definition))
  const [comment, setComment] = useState('')
  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    update.mutate({ expectedVersion: workflow.version, definition, comment })
  }
  function notify(event: NotificationEvent, enabled: boolean) {
    setDefinition((current) => ({
      ...current,
      notifyOn: enabled ? [...new Set([...current.notifyOn, event])] : current.notifyOn.filter((value) => value !== event),
    }))
  }
  return (
    <form className="space-y-6" onSubmit={submit}>
      <fieldset className="grid gap-4 sm:grid-cols-2" disabled={update.isPending}>
        <legend className="mb-4 font-medium">Phê duyệt và kiểm tra</legend>
        <label className="grid gap-2">
          Duyệt kế hoạch
          <select
            className="h-9 rounded-md border bg-background px-3"
            value={definition.planApproval}
            onChange={(event) => setDefinition({ ...definition, planApproval: event.target.value as ApprovalRequirement })}
          >
            {workflow.limits.approvalOptions.map((option) => (
              <option key={option} value={option}>
                {option === 'ALWAYS' ? 'Luôn duyệt' : 'Duyệt vùng nhạy cảm'}
              </option>
            ))}
          </select>
        </label>
        <label className="grid gap-2">
          Duyệt diff
          <select
            className="h-9 rounded-md border bg-background px-3"
            value={definition.diffApproval}
            onChange={(event) => setDefinition({ ...definition, diffApproval: event.target.value as ApprovalRequirement })}
          >
            {workflow.limits.approvalOptions.map((option) => (
              <option key={option} value={option}>
                {option === 'ALWAYS' ? 'Luôn duyệt' : 'Duyệt vùng nhạy cảm'}
              </option>
            ))}
          </select>
        </label>
        <label className="grid gap-2">
          Số lần thử Planner
          <Input
            type="number"
            min={1}
            max={workflow.limits.plannerMaxAttemptsCap}
            required
            value={definition.plannerMaxAttempts}
            onChange={(event) => setDefinition({ ...definition, plannerMaxAttempts: Number(event.target.value) })}
          />
        </label>
        <label className="grid gap-2">
          Số vòng sửa và test
          <Input
            type="number"
            min={0}
            max={workflow.limits.maxFixIterationsCap}
            required
            value={definition.maxFixIterations}
            onChange={(event) => setDefinition({ ...definition, maxFixIterations: Number(event.target.value) })}
          />
        </label>
        <label className="grid gap-2">
          Mức phát hiện chặn xuất bản
          <select
            className="h-9 rounded-md border bg-background px-3"
            value={definition.reviewerBlockingSeverity}
            onChange={(event) => setDefinition({ ...definition, reviewerBlockingSeverity: event.target.value as FindingSeverity })}
          >
            {workflow.limits.severityOptions.map((option) => (
              <option key={option}>{option}</option>
            ))}
          </select>
        </label>
        <label className="flex items-center gap-2">
          <input
            type="checkbox"
            checked={definition.requireRegressionTest}
            onChange={(event) => setDefinition({ ...definition, requireRegressionTest: event.target.checked })}
          />
          Yêu cầu test hồi quy
        </label>
      </fieldset>
      <fieldset className="grid gap-4 sm:grid-cols-2" disabled={update.isPending}>
        <legend className="mb-4 font-medium">Pull request</legend>
        <label className="grid gap-2">
          Tiền tố tiêu đề
          <Input
            value={definition.pullRequest.titlePrefix}
            onChange={(event) =>
              setDefinition({ ...definition, pullRequest: { ...definition.pullRequest, titlePrefix: event.target.value } })
            }
          />
        </label>
        <label className="grid gap-2">
          Nhãn, cách nhau bằng dấu phẩy
          <Input
            defaultValue={definition.pullRequest.labels.join(', ')}
            onChange={(event) =>
              setDefinition({ ...definition, pullRequest: { ...definition.pullRequest, labels: splitList(event.target.value) } })
            }
          />
        </label>
        <label className="grid gap-2">
          Người review, cách nhau bằng dấu phẩy
          <Input
            defaultValue={definition.pullRequest.reviewers.join(', ')}
            onChange={(event) =>
              setDefinition({ ...definition, pullRequest: { ...definition.pullRequest, reviewers: splitList(event.target.value) } })
            }
          />
        </label>
        <label className="flex items-center gap-2">
          <input
            type="checkbox"
            checked={definition.pullRequest.draft}
            onChange={(event) => setDefinition({ ...definition, pullRequest: { ...definition.pullRequest, draft: event.target.checked } })}
          />
          Tạo PR dạng draft
        </label>
      </fieldset>
      <fieldset className="grid gap-4 sm:grid-cols-3" disabled={update.isPending}>
        <legend className="mb-4 font-medium">Model theo bước, để trống để dùng mặc định</legend>
        {(['planner', 'coding', 'reviewer'] as const).map((step) => (
          <label key={step} className="grid gap-2">
            {step}
            <Input
              value={definition.models[step] ?? ''}
              onChange={(event) =>
                setDefinition({ ...definition, models: { ...definition.models, [step]: event.target.value.trim() || null } })
              }
            />
          </label>
        ))}
      </fieldset>
      <fieldset className="grid gap-3 sm:grid-cols-2" disabled={update.isPending}>
        <legend className="mb-4 font-medium">Thông báo</legend>
        {workflow.limits.notificationOptions.map((event) => (
          <label key={event} className="flex items-center gap-2 text-sm">
            <input
              type="checkbox"
              checked={definition.notifyOn.includes(event)}
              disabled={workflow.limits.mandatoryNotifications.includes(event)}
              onChange={(change) => notify(event, change.target.checked)}
            />
            {event}
            {workflow.limits.mandatoryNotifications.includes(event) && ' (bắt buộc)'}
          </label>
        ))}
      </fieldset>
      <label className="grid gap-2">
        Lý do thay đổi
        <Textarea value={comment} onChange={(event) => setComment(event.target.value)} required maxLength={2000} />
      </label>
      <FormError error={update.error} />
      <Button type="submit" disabled={update.isPending || !comment.trim()}>
        Lưu phiên bản mới
      </Button>
    </form>
  )
}

export default function WorkflowPage() {
  const { repositoryId = '' } = useParams()
  const query = useWorkflow(repositoryId)
  const revisions = useWorkflowRevisions(repositoryId)
  if (query.isPending) return <LoadingBlock />
  if (query.isError) return <ErrorBlock error={query.error} onRetry={() => void query.refetch()} />
  return (
    <article className="max-w-4xl space-y-6">
      <PageHeader title="Quy trình agent" description={`${query.data.repositoryFullName} · phiên bản ${query.data.version}`} />
      <p className="text-sm text-muted-foreground">
        Task giữ phiên bản quy trình khi bắt đầu. Các tùy chỉnh Coding, Reviewer và PR có hiệu lực khi những bước này được triển khai.
      </p>
      <Card>
        <CardContent className="pt-6">
          <WorkflowForm key={query.data.version} workflow={query.data} />
        </CardContent>
      </Card>
      <Card>
        <CardHeader>
          <CardTitle>Lịch sử thay đổi</CardTitle>
        </CardHeader>
        <CardContent>
          {revisions.isPending && <LoadingBlock />}
          {revisions.isError && <ErrorBlock error={revisions.error} />}
          {revisions.data?.length === 0 && <p className="text-sm text-muted-foreground">Đang dùng quy trình mặc định.</p>}
          <ul className="space-y-4">
            {revisions.data?.map((revision) => (
              <li key={revision.version}>
                <p className="font-medium">
                  v{revision.version} · {formatDateTime(revision.changedAt)}
                </p>
                <p className="text-sm text-muted-foreground">{revision.comment}</p>
                <details>
                  <summary className="text-sm">Nội dung</summary>
                  <pre className="overflow-auto text-xs">{JSON.stringify(revision.definition, null, 2)}</pre>
                </details>
              </li>
            ))}
          </ul>
        </CardContent>
      </Card>
    </article>
  )
}
