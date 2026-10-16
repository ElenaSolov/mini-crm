export const DEFAULT_PAGE_SIZE = 10;
// The backend allows at most 100 (LeadListQuery.MaxPageSize).
const pageSizeOptions = [10, 20, 50, 100];

interface Props {
  page: number;
  pageCount: number;
  totalCount: number;
  onPageChange: (page: number) => void;
  pageSize: number;
  onPageSizeChange: (pageSize: number) => void;
}

export default function Pagination({ page, pageCount, totalCount, onPageChange, pageSize, onPageSizeChange }: Readonly<Props>) {
  return (
    <div className="pagination">
      <label className="page-size">
        Per page
        <select value={pageSize} onChange={(e) => onPageSizeChange(Number(e.target.value))}>
          {pageSizeOptions.map((size) => (
            <option key={size} value={size}>{size}</option>
          ))}
        </select>
      </label>
      <button disabled={page <= 1} onClick={() => onPageChange(page - 1)}>
        Previous
      </button>
      <span>
        Page {page} of {pageCount} ({totalCount} total)
      </span>
      <button disabled={page >= pageCount} onClick={() => onPageChange(page + 1)}>
        Next
      </button>
    </div>
  );
}
