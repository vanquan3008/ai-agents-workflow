import { FolderGit2 } from 'lucide-react'
import { useAuth } from '@/features/auth/AuthProvider'
import { PageHeader } from '@/shared/ui/page-header'
import { EmptyBlock, ErrorBlock, LoadingBlock } from '@/shared/ui/states'
import { useProjects } from './api'
import { CreateProjectDialog } from './components/CreateProjectDialog'
import { ProjectCard } from './components/ProjectCard'

export default function ProjectsPage() {
  const query = useProjects()
  const { isSuperAdmin } = useAuth()

  return (
    <div>
      <PageHeader
        title="Project & quy trình"
        description="Mỗi project gom repository, thành viên và quy trình agent của chúng."
        actions={isSuperAdmin ? <CreateProjectDialog /> : undefined}
      />
      {query.isPending && <LoadingBlock rows={3} />}
      {query.isError && <ErrorBlock error={query.error} onRetry={() => void query.refetch()} />}
      {query.data?.length === 0 && (
        <EmptyBlock
          icon={<FolderGit2 />}
          title="Chưa có project"
          description={isSuperAdmin ? 'Tạo project đầu tiên rồi đăng ký repository cho agent.' : 'Liên hệ Super Admin để được thêm vào project.'}
          action={isSuperAdmin ? <CreateProjectDialog /> : undefined}
        />
      )}
      {query.data && query.data.length > 0 && (
        <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
          {query.data.map((project) => (
            <ProjectCard key={project.id} project={project} />
          ))}
        </div>
      )}
    </div>
  )
}
