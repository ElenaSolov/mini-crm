export function formatDate(value: string | null) {
  return value ? new Date(value).toLocaleString() : '';
}

// Statuses are stored in lower case ("converted", the name the API and rules use);
// only the screen shows them with a capital letter.
export function formatStatus(status: string) {
  return status.charAt(0).toUpperCase() + status.slice(1);
}

export function formatAmount(amount: number | null, currency: string | null) {
  return amount === null ? '' : `${amount.toFixed(2)} ${currency ?? ''}`.trim();
}
