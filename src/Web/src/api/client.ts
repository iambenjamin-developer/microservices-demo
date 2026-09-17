/**
 * The only place that knows how to talk to the backend: one base URL (the Gateway), one way of
 * sending the access token and one way of reading an error. Everything else works with typed
 * functions built on top of `request`.
 */

/**
 * The browser cannot use Aspire service discovery, so the Gateway address is injected as a build-time
 * variable (Aspire sets it from the gateway endpoint). The fallback is the local launch profile port,
 * so `npm run dev` on its own still reaches a Gateway started by hand.
 */
const gatewayUrl = (import.meta.env.VITE_GATEWAY_URL ?? 'http://localhost:5100').replace(/\/+$/, '')

/** RFC 9457 problem document — what every service returns for an error. */
interface ProblemDetails {
  title?: string
  detail?: string
  status?: number
  /** Stable error code, e.g. "Orders.NotFound". Set by the Result → ProblemDetails mapping. */
  code?: string
  /** Field name → messages, for validation failures. */
  errors?: Record<string, string[]>
  /** Correlates the failure with the distributed trace in the Aspire dashboard. */
  traceId?: string
}

/** A failed HTTP call, carrying what the ProblemDetails body said so the UI can show something useful. */
export class ApiError extends Error {
  readonly status: number
  readonly code?: string
  readonly errors?: Record<string, string[]>
  readonly traceId?: string

  constructor(
    status: number,
    message: string,
    code?: string,
    errors?: Record<string, string[]>,
    traceId?: string,
  ) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.code = code
    this.errors = errors
    this.traceId = traceId
  }

  /** A token that is missing, expired or rejected: the session cannot be recovered by retrying. */
  get isUnauthenticated(): boolean {
    return this.status === 401
  }
}

export interface RequestOptions {
  method?: 'GET' | 'POST' | 'PUT'
  body?: unknown
  accessToken?: string | null
  signal?: AbortSignal
}

export type ApiRequest = <T>(path: string, options?: RequestOptions) => Promise<T>

export const request: ApiRequest = async <T>(path: string, options: RequestOptions = {}): Promise<T> => {
  const { method = 'GET', body, accessToken, signal } = options

  let response: Response
  try {
    response = await fetch(`${gatewayUrl}${path}`, {
      method,
      signal,
      headers: {
        Accept: 'application/json',
        ...(body === undefined ? {} : { 'Content-Type': 'application/json' }),
        ...(accessToken ? { Authorization: `Bearer ${accessToken}` } : {}),
      },
      body: body === undefined ? undefined : JSON.stringify(body),
    })
  } catch (error) {
    // fetch only rejects when the request never got an answer (Gateway down, DNS, CORS, offline).
    if (error instanceof DOMException && error.name === 'AbortError') {
      throw error
    }

    throw new ApiError(0, 'The gateway could not be reached. Is the backend running?')
  }

  if (!response.ok) {
    throw await toApiError(response)
  }

  return response.status === 204 ? (undefined as T) : ((await response.json()) as T)
}

async function toApiError(response: Response): Promise<ApiError> {
  const problem = await readProblemDetails(response)

  // 401 from the JWT handler has an empty body, so fall back to the status text.
  const message = problem?.detail ?? problem?.title ?? response.statusText ?? 'The request failed.'

  return new ApiError(response.status, message, problem?.code, problem?.errors, problem?.traceId)
}

async function readProblemDetails(response: Response): Promise<ProblemDetails | null> {
  if (!response.headers.get('content-type')?.includes('json')) {
    return null
  }

  try {
    return (await response.json()) as ProblemDetails
  } catch {
    return null
  }
}

/** Flattens the validation errors of a 400 into the lines a form can show. */
export function validationMessages(error: ApiError): string[] {
  return Object.values(error.errors ?? {}).flat()
}
