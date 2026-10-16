// Turns an RTK Query error into something to show. ASP.NET Core answers errors
// with "problem details": a 409 has the broken rule in "detail", a 400 has
// the messages per field in "errors".

interface ProblemDetails {
  title?: string;
  detail?: string;
  errors?: Record<string, string[]>;
}

function problemOf(error: unknown): ProblemDetails | undefined {
  if (error && typeof error === 'object' && 'data' in error && error.data && typeof error.data === 'object')
    return error.data as ProblemDetails;
  return undefined;
}

export function errorMessage(error: unknown): string {
  const problem = problemOf(error);
  const message = problem?.detail ?? problem?.title;
  if (message)
    return message;

  // No problem details: the server is down or answered with something else.
  const status = error && typeof error === 'object' && 'status' in error ? error.status : undefined;
  return typeof status === 'number' ? `Request failed (${status}).` : 'Could not reach the server.';
}

// Messages per field, e.g. { email: ["Email or phone is required."] }.
export function fieldErrors(error: unknown): Record<string, string[]> {
  return problemOf(error)?.errors ?? {};
}
