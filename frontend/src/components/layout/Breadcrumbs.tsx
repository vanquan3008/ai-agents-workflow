import { Fragment } from 'react'
import { Link, useMatches } from 'react-router'
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb'
import type { RouteHandle } from './route-handle'

export function Breadcrumbs() {
  const crumbs = useMatches()
    .filter((match) => (match.handle as RouteHandle | undefined)?.crumb)
    .map((match) => ({ id: match.id, path: match.pathname, label: (match.handle as RouteHandle).crumb! }))

  return (
    <Breadcrumb className="min-w-0">
      <BreadcrumbList className="flex-nowrap">
        {crumbs.map((crumb, index) => {
          const isLast = index === crumbs.length - 1
          return (
            <Fragment key={crumb.id}>
              {index > 0 && <BreadcrumbSeparator className={index < crumbs.length - 1 ? 'hidden md:block' : undefined} />}
              <BreadcrumbItem className={isLast ? 'min-w-0' : 'hidden md:inline-flex'}>
                {isLast ? (
                  <BreadcrumbPage className="truncate font-medium">{crumb.label}</BreadcrumbPage>
                ) : (
                  <BreadcrumbLink asChild>
                    <Link to={crumb.path}>{crumb.label}</Link>
                  </BreadcrumbLink>
                )}
              </BreadcrumbItem>
            </Fragment>
          )
        })}
      </BreadcrumbList>
    </Breadcrumb>
  )
}
