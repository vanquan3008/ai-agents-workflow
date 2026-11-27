/** Types mirroring /api/v1/projects/{projectId}/departments. Enum values use UPPER_SNAKE like the API contract. */

export type WorkflowStation = 'PLANNING' | 'DEVELOPMENT' | 'TESTING' | 'REVIEW' | 'APPROVAL' | 'PUBLISHING'

export const WORKFLOW_STATIONS: readonly WorkflowStation[] = ['PLANNING', 'DEVELOPMENT', 'TESTING', 'REVIEW', 'APPROVAL', 'PUBLISHING']

export type DepartmentTheme = 'INDIGO' | 'EMERALD' | 'AMBER' | 'SLATE'

export type RoomAction = 'OPEN_TASKS' | 'OPEN_APPROVALS' | 'OPEN_PROJECT' | 'OPEN_WORKFLOW'

export interface DepartmentRoom {
  roomId: string
  label: string
  /** Pipeline station shown by the room; null for an informational room. */
  stage: WorkflowStation | null
  x: number
  y: number
  width: number
  height: number
  sortOrder: number
  assetPreset: string
  action: RoomAction | null
}

export interface DepartmentConfig {
  name: string
  theme: DepartmentTheme
  repositoryIds: string[]
  rooms: DepartmentRoom[]
}

export interface DepartmentSummary {
  id: string
  projectId: string
  slug: string
  name: string
  theme: DepartmentTheme | null
  publishedVersion: number | null
  publishedAt: string | null
  hasDraft: boolean
  repositoryCount: number
  roomCount: number
  isArchived: boolean
  updatedAt: string
}

export interface DepartmentDirectory {
  items: DepartmentSummary[]
  canManage: boolean
}

export interface DepartmentRepository {
  id: string
  fullName: string
  isActive: boolean
}

export interface PublishedDepartment {
  id: string
  projectId: string
  slug: string
  version: number
  name: string
  theme: DepartmentTheme
  rooms: DepartmentRoom[]
  repositories: DepartmentRepository[]
  publishedAt: string
  canManage: boolean
}

export interface RuleViolation {
  code: string
  field: string
  message: string
}

export interface DepartmentDraft {
  id: string
  projectId: string
  slug: string
  editVersion: number
  publishedVersion: number | null
  /** False when `config` is a copy of the published revision rather than saved work. */
  hasDraft: boolean
  config: DepartmentConfig
  publishIssues: RuleViolation[]
  availableRepositories: DepartmentRepository[]
  updatedAt: string
}

export interface DepartmentEditResult {
  id: string
  slug: string
  editVersion: number
  publishedVersion: number | null
  hasDraft: boolean
}

export interface DepartmentRevision {
  version: number
  name: string
  theme: DepartmentTheme
  roomCount: number
  repositoryCount: number
  publishedBy: string
  publishedByName: string
  publishedAt: string
  isCurrent: boolean
}

export interface DepartmentTemplate {
  key: string
  name: string
  description: string
  config: DepartmentConfig
}

export interface DepartmentEditorOptions {
  columns: number
  rows: number
  minRooms: number
  maxRooms: number
  minRoomSize: number
  nameMaxLength: number
  roomLabelMaxLength: number
  stages: WorkflowStation[]
  themes: DepartmentTheme[]
  assetPresets: string[]
  actions: RoomAction[]
  templates: DepartmentTemplate[]
}
