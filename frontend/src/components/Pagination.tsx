interface Props {
  page: number;
  pageCount: number;
  totalCount: number;
  onPageChange: (page: number) => void;
}

export default function Pagination({ page, pageCount, totalCount, onPageChange }: Readonly<Props>) {
  return (
    <div className="pagination">
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
