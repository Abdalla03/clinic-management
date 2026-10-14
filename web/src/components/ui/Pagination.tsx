import { useI18n } from "../../lib/i18n/useI18n";
import { Button } from "./Button";

export interface PaginationProps {
  page: number;
  pageSize: number;
  totalCount: number;
  onChange: (page: number) => void;
}

export function Pagination({ page, pageSize, totalCount, onChange }: PaginationProps) {
  const { t } = useI18n();

  const totalPages = pageSize > 0 ? Math.ceil(totalCount / pageSize) : 0;
  const start = totalCount === 0 ? 0 : (page - 1) * pageSize + 1;
  const end = Math.min(page * pageSize, totalCount);
  const hasPrevious = page > 1;
  const hasNext = page < totalPages;

  return (
    <div className="flex flex-wrap items-center justify-between gap-3 px-5 py-3">
      <p className="text-sm text-slate-500">
        {t("common.showing", { start, end, total: totalCount })}
      </p>
      <div className="flex items-center gap-2">
        <Button
          variant="secondary"
          size="sm"
          disabled={!hasPrevious}
          onClick={() => onChange(page - 1)}
        >
          <svg
            viewBox="0 0 20 20"
            fill="currentColor"
            aria-hidden="true"
            className="-scale-x-100 rtl:scale-x-100"
          >
            <path
              fillRule="evenodd"
              d="M7.21 14.77a.75.75 0 0 1 .02-1.06L11.168 10 7.23 6.29a.75.75 0 1 1 1.04-1.08l4.5 4.25a.75.75 0 0 1 0 1.08l-4.5 4.25a.75.75 0 0 1-1.06-.02Z"
              clipRule="evenodd"
            />
          </svg>
          <svg
            viewBox="0 0 20 20"
            fill="currentColor"
            aria-hidden="true"
            className="-scale-x-100 rtl:scale-x-100"
          >
            <path
              fillRule="evenodd"
              d="M12.79 5.23a.75.75 0 0 1-.02 1.06L8.832 10l3.938 3.71a.75.75 0 1 1-1.04 1.08l-4.5-4.25a.75.75 0 0 1 0-1.08l4.5-4.25a.75.75 0 0 1 1.06.02Z"
              clipRule="evenodd"
            />
          </svg>
          {t("common.previous")}
        </Button>
        <span className="text-sm text-slate-600">
          {t("common.page")} {page} {t("common.of")} {totalPages || 1}
        </span>
        <Button
          variant="secondary"
          size="sm"
          disabled={!hasNext}
          onClick={() => onChange(page + 1)}
        >
          {t("common.next")}
          <svg
            viewBox="0 0 20 20"
            fill="currentColor"
            aria-hidden="true"
            className="-scale-x-100 rtl:scale-x-100"
          >
            <path
              fillRule="evenodd"
              d="M7.21 14.77a.75.75 0 0 1 .02-1.06L11.168 10 7.23 6.29a.75.75 0 1 1 1.04-1.08l4.5 4.25a.75.75 0 0 1 0 1.08l-4.5 4.25a.75.75 0 0 1-1.06-.02Z"
              clipRule="evenodd"
            />
          </svg>
          <svg
            viewBox="0 0 20 20"
            fill="currentColor"
            aria-hidden="true"
            className="-scale-x-100 rtl:scale-x-100"
          >
            <path
              fillRule="evenodd"
              d="M12.79 5.23a.75.75 0 0 1-.02 1.06L8.832 10l3.938 3.71a.75.75 0 1 1-1.04 1.08l-4.5-4.25a.75.75 0 0 1 0-1.08l4.5-4.25a.75.75 0 0 1 1.06.02Z"
              clipRule="evenodd"
            />
          </svg>
        </Button>
      </div>
    </div>
  );
}