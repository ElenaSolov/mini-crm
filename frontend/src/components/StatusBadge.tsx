// A colored label for a lead status or an event status.
export default function StatusBadge({ status }: Readonly<{ status: string }>) {
  return <span className={`badge badge-${status}`}>{status}</span>;
}
