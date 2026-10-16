export function formatDate(value: string | null) {
  return value ? new Date(value).toLocaleString() : '';
}

export function formatAmount(amount: number | null, currency: string | null) {
  return amount === null ? '' : `${amount.toFixed(2)} ${currency ?? ''}`.trim();
}
