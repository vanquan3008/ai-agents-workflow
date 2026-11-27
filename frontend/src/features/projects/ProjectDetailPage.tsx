import { Building2, GitBranch, Users } from 'lucide-react'
import { Link, useParams } from 'react-router'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { useAuth } from '@/features/auth/AuthProvider'
import { formatNumber } from '@/shared/lib/format'
import { roleLabel } from '@/shared/lib/labels'
import { PageHeader } from '@/shared/ui/page-header'
import { Panel } from '@/shared/ui/panel'
import { EmptyBlock, ErrorBlock, LoadingBlock } from '@/shared/ui/states'
import { useMembers, useProject, useRepositories } from './api'
import { AddMemberForm } from './components/AddMemberForm'
import { MemberTable } from './components/MemberTable'
import { RegisterRepositoryDialog } from './components/RegisterRepositoryDialog'
import { RepositoryList } from './components/RepositoryList'

export default function ProjectDetailPage() {
  const { projectId = '' } = useParams()
  const project = useProject(projectId)
  const members = useMembers(projectId)
  const repositories = useRepositories(projectId)
  const { isSuperAdmin } = useAuth()

  if (project.isPending) return <LoadingBlock rows={6} />
  if (project.isError) return <ErrorBlock error={project.error} onRetry={() => void project.refetch()} />
  const canManage = isSuperAdmin || project.data.myRole === 'OWNER'

  return (
    <article>
      <PageHeader
        backTo={{ to: '/projects', label: 'Tất cả project' }}
        eyebrow={
          <Badge variant="secondary" className="rounded-md">
            {roleLabel(project.data.myRole)}
          </Badge>
        }
        title={project.data.name}
        description={<span className="font-mono">{project.data.slug}</span>}
        actions={
          <Button asChild variant="outline" size="sm">
            <Link to={`/projects/${projectId}/office`}>
              <Building2 /> Phòng kỹ thuật
            </Link>
          </Button>
        }
      />

      <Tabs defaultValue="repositories" className="gap-4">
        <TabsList>
          <TabsTrigger value="repositories">
            <GitBranch /> Repository
            <span className="text-muted-foreground tabular-nums">{formatNumber(project.data.repositoryCount)}</span>
          </TabsTrigger>
          <TabsTrigger value="members">
            <Users /> Thành viên
            <span className="text-muted-foreground tabular-nums">{formatNumber(project.data.memberCount)}</span>
          </TabsTrigger>
        </TabsList>

        <TabsContent value="repositories">
          <Panel
            title="Repository"
            description="Mỗi repository có chính sách an toàn và quy trình agent riêng."
            actions={canManage ? <RegisterRepositoryDialog projectId={projectId} /> : undefined}
            flush
          >
            {repositories.isPending && <LoadingBlock rows={3} className="p-4" />}
            {repositories.isError && <ErrorBlock error={repositories.error} onRetry={() => void repositories.refetch()} className="m-4 w-auto" />}
            {repositories.data?.length === 0 && (
              <EmptyBlock
                className="m-4 w-auto"
                icon={<GitBranch />}
                title="Chưa có repository"
                description={canManage ? 'Đăng ký repository để agent có thể nhận task.' : 'Owner của project cần đăng ký repository.'}
              />
            )}
            {repositories.data && repositories.data.length > 0 && <RepositoryList repositories={repositories.data} />}
          </Panel>
        </TabsContent>

        <TabsContent value="members" className="space-y-4">
          {canManage && (
            <Panel title="Thêm thành viên" description="Người dùng phải có tài khoản DevAgent trước.">
              <AddMemberForm projectId={projectId} />
            </Panel>
          )}
          <Panel title="Thành viên" flush>
            {members.isPending && <LoadingBlock rows={3} className="p-4" />}
            {members.isError && <ErrorBlock error={members.error} onRetry={() => void members.refetch()} className="m-4 w-auto" />}
            {members.data && <MemberTable projectId={projectId} members={members.data} canManage={canManage} />}
          </Panel>
        </TabsContent>
      </Tabs>
    </article>
  )
}
