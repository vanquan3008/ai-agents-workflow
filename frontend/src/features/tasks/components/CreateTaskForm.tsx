import { useId, useState, type FormEvent } from 'react'
import { ApiError, newIdempotencyKey } from '../../../shared/api/http'
import type { Repository, TaskCreated } from '../../../shared/api/types'

export interface CreateTaskFormProps {
  repositories: Repository[]
  onSubmit: (input: { repositoryId: string; title: string; description: string; idempotencyKey: string }) => Promise<TaskCreated>
  onCreated?: (result: TaskCreated) => void
}

const TITLE_MAX = 200
const DESCRIPTION_MAX = 20_000

/**
 * Form giao việc. Khóa Idempotency-Key sinh một lần cho mỗi bản nháp: bấm gửi lại (hoặc mạng thử lại)
 * không tạo task thứ hai. Khóa chỉ đổi sau khi tạo thành công.
 */
export function CreateTaskForm({ repositories, onSubmit, onCreated }: CreateTaskFormProps) {
  const id = useId()
  const [repositoryId, setRepositoryId] = useState(repositories[0]?.id ?? '')
  const [title, setTitle] = useState('')
  const [description, setDescription] = useState('')
  const [idempotencyKey, setIdempotencyKey] = useState(() => newIdempotencyKey())
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [submitting, setSubmitting] = useState(false)
  const [notice, setNotice] = useState<string | null>(null)

  const validate = () => {
    const next: Record<string, string> = {}
    if (!repositoryId) next.repositoryId = 'Chọn repo.'
    if (!title.trim()) next.title = 'Nhập tiêu đề.'
    else if (title.length > TITLE_MAX) next.title = `Tối đa ${TITLE_MAX} ký tự.`
    if (!description.trim()) next.description = 'Mô tả lỗi, cách tái hiện và kết quả mong đợi.'
    else if (description.length > DESCRIPTION_MAX) next.description = `Tối đa ${DESCRIPTION_MAX} ký tự.`
    return next
  }

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    setNotice(null)
    const validation = validate()
    setErrors(validation)
    if (Object.keys(validation).length > 0) return

    setSubmitting(true)
    try {
      const result = await onSubmit({ repositoryId, title: title.trim(), description: description.trim(), idempotencyKey })
      setNotice(result.created ? 'Đã tạo task.' : 'Yêu cầu này đã được gửi trước đó; mở lại task hiện có.')
      setTitle('')
      setDescription('')
      setIdempotencyKey(newIdempotencyKey())
      onCreated?.(result)
    } catch (error) {
      if (error instanceof ApiError && Object.keys(error.errors).length > 0) {
        setErrors(Object.fromEntries(Object.entries(error.errors).map(([k, v]) => [k, v.join(' ')])))
      } else {
        setErrors({ form: error instanceof ApiError ? error.message : 'Không gửi được yêu cầu. Thử lại.' })
      }
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <form className="form" onSubmit={handleSubmit} noValidate aria-labelledby={`${id}-heading`}>
      <h2 id={`${id}-heading`}>Giao việc mới</h2>

      <label htmlFor={`${id}-repo`}>Repo</label>
      <select
        id={`${id}-repo`}
        value={repositoryId}
        onChange={(e) => setRepositoryId(e.target.value)}
        aria-invalid={Boolean(errors.repositoryId)}
        aria-describedby={errors.repositoryId ? `${id}-repo-error` : undefined}
      >
        {repositories.length === 0 && <option value="">Chưa có repo nào</option>}
        {repositories.map((repo) => (
          <option key={repo.id} value={repo.id}>
            {repo.fullName}
          </option>
        ))}
      </select>
      {errors.repositoryId && <p id={`${id}-repo-error`} className="field-error">{errors.repositoryId}</p>}

      <label htmlFor={`${id}-title`}>Tiêu đề</label>
      <input
        id={`${id}-title`}
        value={title}
        maxLength={TITLE_MAX}
        onChange={(e) => setTitle(e.target.value)}
        aria-invalid={Boolean(errors.title)}
        aria-describedby={errors.title ? `${id}-title-error` : undefined}
        placeholder="Bộ lọc trả về task sai trạng thái"
      />
      {errors.title && <p id={`${id}-title-error`} className="field-error">{errors.title}</p>}

      <label htmlFor={`${id}-description`}>Mô tả lỗi</label>
      <textarea
        id={`${id}-description`}
        value={description}
        rows={6}
        onChange={(e) => setDescription(e.target.value)}
        aria-invalid={Boolean(errors.description)}
        aria-describedby={errors.description ? `${id}-description-error` : undefined}
        placeholder="Các bước tái hiện, kết quả thực tế, kết quả mong đợi"
      />
      {errors.description && <p id={`${id}-description-error`} className="field-error">{errors.description}</p>}

      {errors.form && <p className="field-error" role="alert">{errors.form}</p>}
      {notice && <p className="notice" role="status">{notice}</p>}

      <button type="submit" className="button button--primary" disabled={submitting || repositories.length === 0}>
        {submitting ? 'Đang gửi…' : 'Tạo task'}
      </button>
    </form>
  )
}
