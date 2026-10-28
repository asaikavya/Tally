// When the server says no, show its own friendly sentence ({ problem } or { message }),
// otherwise "404 Not Found" style text (ReceiptPage relies on the "404" start).
async function fail(res: Response): Promise<never> {
  const body = await res.json().catch(() => null)
  throw new Error(body?.problem ?? body?.message ?? `${res.status} ${res.statusText}`)
}

export async function getJson<T>(path: string): Promise<T> {
  const res = await fetch(path)
  if (!res.ok) return fail(res)
  return (await res.json()) as T
}

export async function postJson(path: string, body: unknown): Promise<void> {
  const res = await fetch(path, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body),
  })
  if (!res.ok) return fail(res)
}

export async function postFile<T>(path: string, file: File): Promise<T | null> {
  const form = new FormData()
  form.append("file", file)
  const res = await fetch(path, { method: "POST", body: form })
  if (!res.ok) return fail(res)
  return res.status === 202 ? null : ((await res.json()) as T)
}

export async function putJson<T>(path: string, body: unknown): Promise<T> {
  const res = await fetch(path, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body),
  })
  if (!res.ok) return fail(res)
  return (await res.json()) as T
}