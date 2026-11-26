import { Compass } from 'lucide-react'
import { Link } from 'react-router'
import { Button } from '@/components/ui/button'
import { EmptyBlock } from '@/shared/ui/states'

export function NotFoundPage() {
  return (
    <EmptyBlock
      icon={<Compass />}
      title="Không tìm thấy trang"
      description="Đường dẫn không tồn tại hoặc đã bị đổi."
      action={
        <Button asChild variant="outline" size="sm">
          <Link to="/">Về tổng quan</Link>
        </Button>
      }
    />
  )
}
